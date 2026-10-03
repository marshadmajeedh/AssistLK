"""
LLM provider factory for the Quotation & Booking Agent.

Supports the same provider configuration used across AssistLK:
- Google Gemini via GOOGLE_API_KEY / GEMINI_MODEL  (preferred)
- OpenAI       via OPENAI_API_KEY / OPENAI_MODEL   (fallback)

If no supported credentials are configured, the workflow degrades to
non-LLM deterministic behaviour instead of crashing.
"""

import os
from typing import Any, Optional

try:
    from langchain_google_genai import ChatGoogleGenerativeAI
except ImportError:  # pragma: no cover - optional dependency in some envs
    ChatGoogleGenerativeAI = None  # type: ignore[assignment]

try:
    from langchain_openai import ChatOpenAI
except ImportError:  # pragma: no cover - optional dependency in some envs
    ChatOpenAI = None  # type: ignore[assignment]


# Default models per provider
_DEFAULT_GEMINI_MODEL = "gemini-3.8-flash"
_DEFAULT_OPENAI_MODEL = "gpt-4o-mini"

# Mapping of retired/incompatible Gemini models to the current default
_GEMINI_MODEL_REWRITES = {
    "gemini-2.5-flash": _DEFAULT_GEMINI_MODEL,
    "gemini-2.5-flash-lite": _DEFAULT_GEMINI_MODEL,
    "gemini-2.5-pro": _DEFAULT_GEMINI_MODEL,
    "gemini-2.0-flash": _DEFAULT_GEMINI_MODEL,
    "gemini-1.5-flash": _DEFAULT_GEMINI_MODEL,
    "gemini-1.5-pro": _DEFAULT_GEMINI_MODEL,
}


def _normalize_model_name(provider_name: Optional[str], model_name: str) -> str:
    """Map legacy/stale model names to supported defaults."""
    cleaned = (model_name or "").strip()

    if provider_name == "google":
        if not cleaned:
            return _DEFAULT_GEMINI_MODEL
        return _GEMINI_MODEL_REWRITES.get(cleaned.lower(), cleaned)

    if provider_name == "openai":
        return cleaned or _DEFAULT_OPENAI_MODEL

    return cleaned


def _resolve_llm_provider() -> tuple[Optional[str], str, str]:
    """Return (provider_name, api_key, model_name) for the configured backend."""
    google_key = os.environ.get("GOOGLE_API_KEY", "").strip()
    if google_key:
        model = _normalize_model_name(
            "google",
            os.environ.get("GEMINI_MODEL", _DEFAULT_GEMINI_MODEL),
        )
        return "google", google_key, model

    openai_key = os.environ.get("OPENAI_API_KEY", "").strip()
    if openai_key:
        model = _normalize_model_name(
            "openai",
            os.environ.get("OPENAI_MODEL", _DEFAULT_OPENAI_MODEL),
        )
        return "openai", openai_key, model

    return None, "", ""


def get_chat_model() -> Optional[Any]:
    """Build the configured chat model instance, or None if no provider is available."""
    provider_name, api_key, model_name = _resolve_llm_provider()
    if not api_key:
        return None

    if provider_name == "google" and ChatGoogleGenerativeAI is not None:
        return ChatGoogleGenerativeAI(
            model=model_name,
            google_api_key=api_key,
            temperature=0.2,
            timeout=30.0,
            max_retries=1,
        )

    if provider_name == "openai" and ChatOpenAI is not None:
        return ChatOpenAI(
            model=model_name,
            api_key=api_key,
            temperature=0.2,
            timeout=30.0,
            max_retries=1,
        )

    return None


def is_llm_available() -> bool:
    provider_name, api_key, _ = _resolve_llm_provider()
    if not api_key:
        return False

    if provider_name == "google":
        return ChatGoogleGenerativeAI is not None
    if provider_name == "openai":
        return ChatOpenAI is not None

    return False