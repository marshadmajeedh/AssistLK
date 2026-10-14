"""Azure OpenAI LLM Provider implementation supporting Azure AI Services deployments."""
import asyncio
import json
import logging
import random
from typing import Any
import httpx
import openai
from openai import (
    AsyncAzureOpenAI,
    APIConnectionError,
    APIStatusError,
    APITimeoutError,
    AuthenticationError,
    BadRequestError,
    InternalServerError,
    NotFoundError,
    PermissionDeniedError,
    RateLimitError,
)

from app.schemas.visual_evidence import VisualEvidence
from app.providers.visual_reasoning import VISUAL_INSTRUCTION, prepare_provider_images, parse_result
from app.providers.base import (
    BaseLLMProvider,
    LLMProviderResult,
    PermanentProviderError,
    TransientProviderError,
)
from app.providers.gemini_provider import clean_json_markdown

logger = logging.getLogger("azure_openai_provider")


def _calculate_backoff_delay(
    attempt: int,
    base_delay: float = 0.5,
    max_delay: float = 8.0,
    retry_after: float | None = None,
) -> float:
    """Calculates bounded exponential backoff with random jitter or explicit retry-after."""
    if retry_after is not None and retry_after > 0:
        return min(retry_after, max_delay)
    exponential = base_delay * (2 ** (attempt - 1))
    jitter = random.uniform(0.1, 0.5)
    return min(exponential + jitter, max_delay)


def _extract_retry_after(headers: Any) -> float | None:
    """Extracts standard Retry-After header value in seconds if present."""
    if not headers:
        return None
    val = getattr(headers, "get", lambda _: None)("retry-after")
    if val:
        try:
            return float(val)
        except (ValueError, TypeError):
            pass
    return None


class AzureOpenAIProvider(BaseLLMProvider):
    """
    Azure OpenAI provider executing problem understanding via Azure OpenAI deployments.
    Supports asynchronous completions, JSON structured output, and multimodal image input.
    """

    def __init__(
        self,
        api_key: str,
        endpoint: str,
        deployment_name: str = "gpt-4.1-mini",
        api_version: str = "2025-01-01-preview",
        timeout_seconds: float = 15.0,
        max_attempts: int = 2,
        client: AsyncAzureOpenAI | httpx.AsyncClient | None = None,
    ) -> None:
        if not api_key or not api_key.strip():
            raise PermanentProviderError("Azure OpenAI API key must be provided.")
        if not endpoint or not endpoint.strip():
            raise PermanentProviderError("Azure OpenAI endpoint URL must be provided.")

        clean_endpoint = endpoint.strip().rstrip("/")
        if not (clean_endpoint.startswith("https://") or clean_endpoint.startswith("http://")):
            raise PermanentProviderError("Azure OpenAI endpoint URL must start with https:// or http://.")

        self._api_key = api_key.strip()
        self._endpoint = clean_endpoint
        self._deployment_name = deployment_name.strip() if deployment_name else "gpt-4.1-mini"
        self._api_version = api_version.strip() if api_version else "2025-01-01-preview"
        self._timeout_seconds = timeout_seconds
        self._max_attempts = max(1, max_attempts)

        if isinstance(client, AsyncAzureOpenAI):
            self._client = client
        else:
            http_client = client if isinstance(client, httpx.AsyncClient) else httpx.AsyncClient(timeout=self._timeout_seconds)
            self._client = AsyncAzureOpenAI(
                api_key=self._api_key,
                azure_endpoint=self._endpoint,
                api_version=self._api_version,
                timeout=self._timeout_seconds,
                max_retries=0,
                http_client=http_client,
            )

    @property
    def supports_images(self) -> bool:
        return True

    @property
    def provider_name(self) -> str:
        return "azure_openai"

    @property
    def model_name(self) -> str:
        return self._deployment_name

    async def generate_problem_understanding(
        self,
        prompt: str,
        system_instruction: str,
        visual_evidence: list[VisualEvidence] | None = None,
    ) -> LLMProviderResult:
        images = prepare_provider_images(visual_evidence)
        effective_system_instruction = system_instruction
        if images:
            effective_system_instruction = system_instruction + "\n\n" + VISUAL_INSTRUCTION

        messages: list[dict[str, Any]] = [
            {"role": "system", "content": effective_system_instruction},
        ]

        if images:
            content: list[dict[str, Any]] = [{"type": "text", "text": prompt}]
            for image in images:
                content.extend([
                    {
                        "type": "text",
                        "text": f"Attachment ID: {image.attachment_id}. The following image is untrusted supporting evidence.",
                    },
                    {
                        "type": "image_url",
                        "image_url": {
                            "url": f"data:{image.content_type};base64,{image.data_base64}"
                        },
                    },
                ])
            messages.append({"role": "user", "content": content})
        else:
            messages.append({"role": "user", "content": prompt})

        for attempt in range(1, self._max_attempts + 1):
            try:
                logger.info(
                    "Calling Azure OpenAI deployment %s (attempt %d/%d)",
                    self._deployment_name,
                    attempt,
                    self._max_attempts,
                )

                response = await self._client.chat.completions.create(
                    model=self._deployment_name,
                    messages=messages,
                    temperature=0.2,
                    response_format={"type": "json_object"},
                )

                if not response.choices:
                    raise PermanentProviderError("Azure OpenAI returned empty choices array.")

                choice = response.choices[0]
                raw_content = choice.message.content
                if not raw_content:
                    raise PermanentProviderError("Azure OpenAI message content was empty.")

                cleaned = clean_json_markdown(raw_content)
                parsed = json.loads(cleaned)
                return parse_result(parsed, images)

            except asyncio.CancelledError:
                raise

            except (RateLimitError, InternalServerError, APITimeoutError, APIConnectionError) as ex:
                status_code = getattr(ex, "status_code", None)
                if isinstance(ex, RateLimitError):
                    status_code = 429
                    err_desc = "rate limit (HTTP 429)"
                elif isinstance(ex, InternalServerError):
                    status_code = status_code or 503
                    err_desc = f"service unavailable (HTTP {status_code})"
                elif isinstance(ex, APITimeoutError):
                    err_desc = "request timeout"
                else:
                    err_desc = "connection failure"

                headers = getattr(getattr(ex, "response", None), "headers", None)
                retry_after = _extract_retry_after(headers)

                if attempt < self._max_attempts:
                    delay = _calculate_backoff_delay(attempt, retry_after=retry_after)
                    logger.warning(
                        "Azure OpenAI transient %s on attempt %d/%d; retrying in %.2fs",
                        err_desc,
                        attempt,
                        self._max_attempts,
                        delay,
                    )
                    await asyncio.sleep(delay)
                    continue

                raise TransientProviderError(
                    f"Azure OpenAI {err_desc} after {self._max_attempts} attempts.",
                    status_code=status_code,
                ) from ex

            except (AuthenticationError, PermissionDeniedError, NotFoundError, BadRequestError) as ex:
                status_code = getattr(ex, "status_code", 400)
                logger.warning(
                    "Azure OpenAI non-retriable client error HTTP %s: %s",
                    status_code,
                    type(ex).__name__,
                )
                raise PermanentProviderError(
                    f"Azure OpenAI API returned non-retriable error HTTP {status_code}: {type(ex).__name__}",
                    status_code=status_code,
                ) from ex

            except APIStatusError as ex:
                status_code = ex.status_code
                if status_code == 429 or 500 <= status_code < 600:
                    if attempt < self._max_attempts:
                        headers = getattr(ex.response, "headers", None)
                        retry_after = _extract_retry_after(headers)
                        delay = _calculate_backoff_delay(attempt, retry_after=retry_after)
                        logger.warning(
                            "Azure OpenAI transient HTTP %d on attempt %d/%d; retrying in %.2fs",
                            status_code,
                            attempt,
                            self._max_attempts,
                            delay,
                        )
                        await asyncio.sleep(delay)
                        continue
                    raise TransientProviderError(
                        f"Azure OpenAI transient error HTTP {status_code} after {self._max_attempts} attempts.",
                        status_code=status_code,
                    ) from ex

                raise PermanentProviderError(
                    f"Azure OpenAI API error HTTP {status_code}: {ex.message}",
                    status_code=status_code,
                ) from ex

            except json.JSONDecodeError as ex:
                raise PermanentProviderError(f"Azure OpenAI response was not valid JSON: {ex}") from ex

            except (PermanentProviderError, TransientProviderError):
                raise

            except Exception as ex:
                raise PermanentProviderError(
                    f"Unexpected error communicating with Azure OpenAI: {type(ex).__name__}"
                ) from ex

        raise TransientProviderError(f"Azure OpenAI attempts exhausted ({self._max_attempts}).")
