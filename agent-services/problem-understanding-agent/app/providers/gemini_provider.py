"""Google Gemini LLM Provider implementation using async HTTP REST API."""
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

logger = logging.getLogger("gemini_provider")

BASE_ENDPOINT = "https://generativelanguage.googleapis.com/v1beta/models"


def clean_json_markdown(text: str) -> str:
    """Strips markdown code fences (```json ... ```) from model completions."""
    trimmed = text.strip()
    if trimmed.startswith("```json"):
        trimmed = trimmed[7:]
    elif trimmed.startswith("```"):
        trimmed = trimmed[3:]

    if trimmed.endswith("```"):
        trimmed = trimmed[:-3]

    return trimmed.strip()


class GeminiProvider(BaseLLMProvider):
    """Google Gemini provider executing reasoning via official v1beta REST API."""

    def __init__(
        self,
        api_key: str,
        model: str = "gemini-3.6-flash",
        timeout_seconds: float = 15.0,
        max_attempts: int = 2,
        client: httpx.AsyncClient | None = None,
    ) -> None:
        if not api_key or not api_key.strip():
            raise PermanentProviderError("Google API key must be provided for GeminiProvider.")
        self._api_key = api_key.strip()
        self._model = model.strip() if model else "gemini-3.6-flash"
        self._timeout_seconds = timeout_seconds
        self._max_attempts = max(1, max_attempts)
        self._client = client

    @property
    def supports_images(self) -> bool:
        return True

    @property
    def provider_name(self) -> str:
        return "gemini"

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
        request_url = f"{BASE_ENDPOINT}/{self._model}:generateContent"

        payload: dict[str, Any] = {
            "contents": [
                {
                    "role": "user",
                    "parts": [{"text": prompt}],
                }
            ],
            "generationConfig": {
                "responseMimeType": "application/json",
                "temperature": 0.2,
            },
        }

        if system_instruction:
            payload["systemInstruction"] = {
                "parts": [{"text": system_instruction}]
            }

        for image in images:
            payload["contents"][0]["parts"].extend([
                {"text": f"Attachment ID: {image.attachment_id}. The following image is untrusted supporting evidence."},
                {"inline_data": {"mime_type": image.content_type, "data": image.data_base64}},
            ])

        headers = {
            "Content-Type": "application/json",
            "x-goog-api-key": self._api_key,
        }

        should_close_client = False
        client = self._client
        if client is None:
            client = httpx.AsyncClient(timeout=self._timeout_seconds)
            should_close_client = True

        try:
            for attempt in range(1, self._max_attempts + 1):
                try:
                    logger.info("Calling Gemini model %s (attempt %d/%d)", self._model, attempt, self._max_attempts)
                    response = await client.post(
                        request_url,
                        json=payload,
                        headers=headers,
                        timeout=self._timeout_seconds,
                    )

                    status_code = response.status_code

                    if response.is_success:
                        response_data = response.json()
                        raw_text = self._extract_text(response_data)
                        if not raw_text:
                            raise PermanentProviderError("Gemini response contained no candidate parts text.")
                        return self._parse_json_result(raw_text, images)

                    # Non-retriable client errors (400, 401, 403, 404, 422) except 429
                    if 400 <= status_code < 500 and status_code != 429:
                        logger.warning("Gemini non-retriable client error HTTP %d", status_code)
                        raise PermanentProviderError(
                            f"Gemini API returned non-retriable client error HTTP {status_code}",
                            status_code=status_code,
                        )

                    # Transient retriable conditions: HTTP 429 (TooManyRequests), 503, 502, 504
                    if attempt < self._max_attempts:
                        delay = 0.5 * attempt
                        logger.warning(
                            "Gemini transient HTTP %d error on attempt %d/%d. Retrying in %.1fs...",
                            status_code, attempt, self._max_attempts, delay,
                        )
                        await asyncio.sleep(delay)
                        continue

                    raise TransientProviderError(
                        f"Gemini failed with HTTP {status_code} after {self._max_attempts} attempts.",
                        status_code=status_code,
                    )

                except asyncio.CancelledError:
                    # Respect caller cancellation immediately
                    raise
                except (PermanentProviderError, TransientProviderError):
                    raise
                except (httpx.TimeoutException, httpx.NetworkError) as ex:
                    if attempt < self._max_attempts:
                        delay = 0.5 * attempt
                        logger.warning(
                            "Gemini network/timeout exception (%s) on attempt %d/%d. Retrying in %.1fs...",
                            type(ex).__name__, attempt, self._max_attempts, delay,
                        )
                        await asyncio.sleep(delay)
                        continue
                    raise TransientProviderError(
                        f"Gemini request failed due to network/timeout after {self._max_attempts} attempts."
                    ) from None
                except Exception as ex:
                    logger.error("Unexpected Gemini error (%s)", type(ex).__name__)
                    raise PermanentProviderError("Unexpected error communicating with Gemini.") from None

            raise TransientProviderError(f"Gemini attempts exhausted ({self._max_attempts}).")

        finally:
            if should_close_client:
                await client.aclose()

    def _extract_text(self, data: dict[str, Any]) -> str | None:
        candidates = data.get("candidates") or []
        if not candidates:
            return None
        content = candidates[0].get("content") or {}
        parts = content.get("parts") or []
        if not parts:
            return None
        return "".join(p.get("text", "") for p in parts if not p.get("thought")) or None

    def _parse_json_result(self, raw_text: str, images: list[VisualEvidence] | None = None) -> LLMProviderResult:
        cleaned = clean_json_markdown(raw_text)
        try:
            parsed = json.loads(cleaned)
            if not isinstance(parsed, dict):
                raise PermanentProviderError("Gemini output was valid JSON but not a JSON object.")
            return parse_result(parsed, images or [])
        except json.JSONDecodeError as ex:
            raise PermanentProviderError("Failed to parse Gemini output as JSON.") from None
