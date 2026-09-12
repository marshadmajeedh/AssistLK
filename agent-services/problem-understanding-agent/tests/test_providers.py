"""Unit tests for multi-provider abstraction, Gemini/OpenAI implementations, retries, and offline heuristics."""
import json
import httpx
import pytest
import respx
from app.config import Settings
from app.providers.base import (
    PermanentProviderError,
    TransientProviderError,
)
from app.providers.factory import ProviderFactory
from app.providers.gemini_provider import GeminiProvider
from app.providers.offline_provider import OfflineSimulationProvider
from app.providers.openai_provider import OpenAIProvider


# --- Provider Factory Tests ---


def test_factory_returns_offline_when_configured():
    """Verifies that LLM_PROVIDER=offline instantiates OfflineSimulationProvider."""
    settings = Settings(LLM_PROVIDER="offline")
    provider = ProviderFactory.create_provider(settings)
    assert isinstance(provider, OfflineSimulationProvider)
    assert provider.provider_name == "offline"


def test_factory_falls_back_to_offline_when_gemini_key_missing():
    """Verifies that missing GOOGLE_API_KEY triggers OfflineSimulationProvider if simulation allowed."""
    settings = Settings(
        LLM_PROVIDER="gemini",
        GOOGLE_API_KEY=None,
        ALLOW_OFFLINE_SIMULATION=True,
    )
    provider = ProviderFactory.create_provider(settings)
    assert isinstance(provider, OfflineSimulationProvider)


def test_factory_raises_permanent_error_when_offline_disabled_and_no_key():
    """Verifies that missing GOOGLE_API_KEY raises PermanentProviderError if offline simulation is disallowed."""
    settings = Settings(
        LLM_PROVIDER="gemini",
        GOOGLE_API_KEY=None,
        ALLOW_OFFLINE_SIMULATION=False,
    )
    with pytest.raises(PermanentProviderError):
        ProviderFactory.create_provider(settings)


def test_factory_creates_gemini_provider():
    """Verifies that valid GOOGLE_API_KEY instantiates GeminiProvider."""
    settings = Settings(
        LLM_PROVIDER="gemini",
        GOOGLE_API_KEY="AIzaSyDummyTestKey",
        GEMINI_MODEL="gemini-3.6-flash",
    )
    provider = ProviderFactory.create_provider(settings)
    assert isinstance(provider, GeminiProvider)
    assert provider.provider_name == "gemini"
    assert provider.model_name == "gemini-3.6-flash"


def test_factory_creates_openai_provider():
    """Verifies that valid OPENAI_API_KEY instantiates OpenAIProvider."""
    settings = Settings(
        LLM_PROVIDER="openai",
        OPENAI_API_KEY="sk-dummy-test-key",
        OPENAI_MODEL="gpt-4o-mini",
    )
    provider = ProviderFactory.create_provider(settings)
    assert isinstance(provider, OpenAIProvider)
    assert provider.provider_name == "openai"
    assert provider.model_name == "gpt-4o-mini"


# --- Offline Simulation Provider Tests ---


@pytest.mark.asyncio
async def test_offline_provider_classifies_burst_pipe():
    """Verifies that offline simulator correctly categorizes urgent plumbing requests."""
    provider = OfflineSimulationProvider()
    prompt = "Customer Description:\n<customer_description>Pipe burst under the sink flooding water</customer_description>"
    result = await provider.generate_problem_understanding(prompt, "instruction")

    assert result.category == "Plumbing"
    assert result.urgency == "High"
    assert result.confidence >= 0.85
    assert result.needs_more_information is False


@pytest.mark.asyncio
async def test_offline_provider_classifies_sparking_socket():
    """Verifies that offline simulator detects electrical issues."""
    provider = OfflineSimulationProvider()
    prompt = "<customer_description>Wall socket is sparking and smoke coming out</customer_description>"
    result = await provider.generate_problem_understanding(prompt, "instruction")

    assert result.category == "Electrical"
    assert result.urgency == "High"
    assert result.confidence >= 0.85


@pytest.mark.asyncio
async def test_offline_provider_vague_input():
    """Verifies that offline simulator marks vague input as Unclassified with Unknown urgency."""
    provider = OfflineSimulationProvider()
    prompt = "<customer_description>It is broken please fix</customer_description>"
    result = await provider.generate_problem_understanding(prompt, "instruction")

    assert result.category == "Unclassified"
    assert result.urgency == "Unknown"
    assert result.needs_more_information is True
    assert result.confidence <= 0.4
    assert len(result.follow_up_questions) >= 1


# --- Mocked Gemini Provider Tests (HTTP Mocking with respx) ---


@pytest.mark.asyncio
@respx.mock
async def test_gemini_provider_successful_reasoning():
    """Verifies that GeminiProvider parses a standard successful Google Gemini API response."""
    endpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent"

    mock_gemini_body = {
        "candidates": [
            {
                "content": {
                    "parts": [
                        {
                            "text": json.dumps({
                                "category": "Plumbing",
                                "problemSummary": "Possible leaking pipe under kitchen sink.",
                                "urgency": "Medium",
                                "needsMoreInformation": False,
                                "followUpQuestions": [],
                                "confidence": 0.88,
                            })
                        }
                    ]
                }
            }
        ]
    }

    respx.post(endpoint).mock(return_value=httpx.Response(200, json=mock_gemini_body))

    provider = GeminiProvider(
        api_key="AIzaSyDummyKey",
        model="gemini-3.6-flash",
        timeout_seconds=2.0,
        max_attempts=2,
    )

    result = await provider.generate_problem_understanding("test prompt", "instruction")
    assert result.category == "Plumbing"
    assert result.problem_summary == "Possible leaking pipe under kitchen sink."
    assert result.urgency == "Medium"
    assert result.confidence == 0.88


@pytest.mark.asyncio
@respx.mock
async def test_gemini_provider_transient_retry_success():
    """Verifies that a 429 rate-limit on attempt 1 triggers retry and succeeds on attempt 2."""
    endpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent"

    success_body = {
        "candidates": [
            {
                "content": {
                    "parts": [
                        {
                            "text": json.dumps({
                                "category": "Electrical",
                                "problemSummary": "Possible breaker fault.",
                                "urgency": "High",
                                "needsMoreInformation": False,
                                "followUpQuestions": [],
                                "confidence": 0.9,
                            })
                        }
                    ]
                }
            }
        ]
    }

    # Attempt 1 -> 429 Too Many Requests; Attempt 2 -> 200 OK
    respx.post(endpoint).mock(
        side_effect=[
            httpx.Response(429, json={"error": "Rate limit reached"}),
            httpx.Response(200, json=success_body),
        ]
    )

    provider = GeminiProvider(
        api_key="AIzaSyDummyKey",
        model="gemini-3.6-flash",
        timeout_seconds=2.0,
        max_attempts=2,
    )

    result = await provider.generate_problem_understanding("test prompt", "instruction")
    assert result.category == "Electrical"
    assert result.confidence == 0.9


@pytest.mark.asyncio
@respx.mock
async def test_gemini_provider_permanent_error_fail_fast():
    """Verifies that HTTP 401 (Invalid API Key) fails fast immediately without retry."""
    endpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent"

    route = respx.post(endpoint).mock(
        return_value=httpx.Response(401, json={"error": "API key not valid"})
    )

    provider = GeminiProvider(
        api_key="AIzaSyInvalidKey",
        model="gemini-3.6-flash",
        timeout_seconds=2.0,
        max_attempts=2,
    )

    with pytest.raises(PermanentProviderError) as exc:
        await provider.generate_problem_understanding("test prompt", "instruction")

    assert exc.value.status_code == 401
    assert route.call_count == 1  # Did NOT retry!


@pytest.mark.asyncio
@respx.mock
async def test_gemini_provider_exhausted_timeout_raises_transient():
    """Verifies that exhausting attempts on timeout raises TransientProviderError."""
    endpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent"

    respx.post(endpoint).mock(side_effect=httpx.ReadTimeout("Socket timed out"))

    provider = GeminiProvider(
        api_key="AIzaSyDummyKey",
        model="gemini-3.6-flash",
        timeout_seconds=0.1,
        max_attempts=2,
    )

    with pytest.raises(TransientProviderError):
        await provider.generate_problem_understanding("test prompt", "instruction")
