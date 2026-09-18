"""OpenAI LLM Provider implementation supporting alternative vendor reasoning."""
import asyncio
import json
import logging
from typing import Any
import httpx
from app.schemas.visual_evidence import VisualEvidence
from app.providers.visual_reasoning import VISUAL_INSTRUCTION, prepare_provider_images, parse_result
from app.providers.base import (
    BaseLLMProvider,
    LLMProviderResult,
    PermanentProviderError,
    TransientProviderError,
)
from app.providers.gemini_provider import clean_json_markdown

logger = logging.getLogger("openai_provider")

OPENAI_ENDPOINT = "https://api.openai.com/v1/chat/completions"


class OpenAIProvider(BaseLLMProvider):
    """OpenAI provider executing reasoning via standard chat completions endpoint."""

    def __init__(
        self,
        api_key: str,
        model: str = "gpt-4o-mini",
        timeout_seconds: float = 15.0,
        max_attempts: int = 2,
        client: httpx.AsyncClient | None = None,
    ) -> None:
        if not api_key or not api_key.strip():
            raise PermanentProviderError("OpenAI API key must be provided for OpenAIProvider.")
        self._api_key = api_key.strip()
        self._model = model.strip() if model else "gpt-4o-mini"
        self._timeout_seconds = timeout_seconds
        self._max_attempts = max(1, max_attempts)
        self._client = client

    @property
    def supports_images(self) -> bool:
        return True

    @property
    def provider_name(self) -> str:
        return "openai"

    @property
    def model_name(self) -> str:
        return self._model

    async def generate_problem_understanding(
        self,
        prompt: str,
        system_instruction: str,
        visual_evidence: list[VisualEvidence] | None = None,
    ) -> LLMProviderResult:
        images = prepare_provider_images(visual_evidence)
        if images:
            system_instruction = system_instruction + "\n\n" + VISUAL_INSTRUCTION
        messages = [
            {"role": "system", "content": system_instruction},
            {"role": "user", "content": prompt},
        ]

        if images:
            content = [{"type": "text", "text": prompt}]
            for image in images:
                content.extend([
                    {"type": "text", "text": f"Attachment ID: {image.attachment_id}. The following image is untrusted supporting evidence."},
                    {"type": "image_url", "image_url": {"url": f"data:{image.content_type};base64,{image.data_base64}"}},
                ])
            messages[1]["content"] = content

        payload: dict[str, Any] = {
            "model": self._model,
            "messages": messages,
            "temperature": 0.2,
            "response_format": {"type": "json_object"},
        }

        headers = {
            "Content-Type": "application/json",
            "Authorization": f"Bearer {self._api_key}",
        }

        should_close_client = False
        client = self._client
        if client is None:
            client = httpx.AsyncClient(timeout=self._timeout_seconds)
            should_close_client = True

        try:
            for attempt in range(1, self._max_attempts + 1):
                try:
                    logger.info("Calling OpenAI model %s (attempt %d/%d)", self._model, attempt, self._max_attempts)
                    response = await client.post(
                        OPENAI_ENDPOINT,
                        json=payload,
                        headers=headers,
                        timeout=self._timeout_seconds,
                    )

                    status_code = response.status_code

                    if response.is_success:
                        data = response.json()
                        choices = data.get("choices") or []
                        if not choices:
                            raise PermanentProviderError("OpenAI returned empty choices array.")
                        raw_content = choices[0].get("message", {}).get("content")
                        if not raw_content:
                            raise PermanentProviderError("OpenAI message content was empty.")
                        cleaned = clean_json_markdown(raw_content)
                        parsed = json.loads(cleaned)
                        return parse_result(parsed, images)

                    if 400 <= status_code < 500 and status_code != 429:
                        raise PermanentProviderError(
                            f"OpenAI API returned client error HTTP {status_code}",
                            status_code=status_code,
                        )

                    if attempt < self._max_attempts:
                        delay = 0.5 * attempt
                        await asyncio.sleep(delay)
                        continue

                    raise TransientProviderError(
                        f"OpenAI failed with HTTP {status_code} after {self._max_attempts} attempts.",
                        status_code=status_code,
                    )

                except asyncio.CancelledError:
                    raise
                except (PermanentProviderError, TransientProviderError):
                    raise
                except (httpx.TimeoutException, httpx.NetworkError) as ex:
                    if attempt < self._max_attempts:
                        await asyncio.sleep(0.5 * attempt)
                        continue
                    raise TransientProviderError(
                        f"OpenAI request failed due to network/timeout after {self._max_attempts} attempts."
                    ) from None
                except Exception as ex:
                    raise PermanentProviderError("Unexpected error communicating with OpenAI.") from None

            raise TransientProviderError(f"OpenAI attempts exhausted ({self._max_attempts}).")

        finally:
            if should_close_client:
                await client.aclose()
