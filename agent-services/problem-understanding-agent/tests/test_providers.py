# Purpose: Tests multi-provider abstractions, provider factory logic, offline simulation, and HTTP retry/error handling for Google Gemini.
# Why: Ensures the AI agent service can switch between LLM backends (Gemini, OpenAI, offline simulator) and robustly handles HTTP rate limits, auth errors, and timeouts.
# Real vs Mocked: Factory and offline provider tests run real local Python logic. Gemini provider tests use `respx` to mock external Google Gemini REST HTTP calls (no live model calls or keys required).
# Prerequisites: Python virtual environment with dependencies installed in agent-services/problem-understanding-agent/.venv.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_providers.py

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


# Purpose: Verifies that configuring LLM_PROVIDER="offline" returns the OfflineSimulationProvider.
# Why: Enables fully offline local testing and development without requiring any external LLM credentials.
# Input/Condition: Settings with LLM_PROVIDER="offline".
# Pass condition: Instantiated provider is an instance of OfflineSimulationProvider with provider_name "offline".
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_providers.py::test_factory_returns_offline_when_configured
def test_factory_returns_offline_when_configured():
    """Verifies that LLM_PROVIDER=offline instantiates OfflineSimulationProvider."""
    settings = Settings(LLM_PROVIDER="offline")
    provider = ProviderFactory.create_provider(settings)
    assert isinstance(provider, OfflineSimulationProvider)
    assert provider.provider_name == "offline"


# Purpose: Verifies fallback to offline simulation when GOOGLE_API_KEY is missing but simulation is allowed.
# Why: Prevents startup crash in environments where live API keys have not been configured.
# Input/Condition: Settings with LLM_PROVIDER="gemini", GOOGLE_API_KEY=None, ALLOW_OFFLINE_SIMULATION=True.
# Pass condition: ProviderFactory returns an instance of OfflineSimulationProvider.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_providers.py::test_factory_falls_back_to_offline_when_gemini_key_missing
def test_factory_falls_back_to_offline_when_gemini_key_missing():
    """Verifies that missing GOOGLE_API_KEY triggers OfflineSimulationProvider if simulation allowed."""
    settings = Settings(
        LLM_PROVIDER="gemini",
        GOOGLE_API_KEY=None,
        ALLOW_OFFLINE_SIMULATION=True,
    )
    provider = ProviderFactory.create_provider(settings)
    assert isinstance(provider, OfflineSimulationProvider)


# Purpose: Verifies that missing API key raises a PermanentProviderError when offline simulation is forbidden.
# Why: Strict configuration enforcement in production where silent fallback to offline simulation would be inappropriate.
# Input/Condition: Settings with LLM_PROVIDER="gemini", GOOGLE_API_KEY=None, ALLOW_OFFLINE_SIMULATION=False.
# Pass condition: ProviderFactory.create_provider raises PermanentProviderError.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_providers.py::test_factory_raises_permanent_error_when_offline_disabled_and_no_key
def test_factory_raises_permanent_error_when_offline_disabled_and_no_key():
    """Verifies that missing GOOGLE_API_KEY raises PermanentProviderError if offline simulation is disallowed."""
    settings = Settings(
        LLM_PROVIDER="gemini",
        GOOGLE_API_KEY=None,
        ALLOW_OFFLINE_SIMULATION=False,
    )
    with pytest.raises(PermanentProviderError):
        ProviderFactory.create_provider(settings)


# Purpose: Verifies that valid GOOGLE_API_KEY instantiates GeminiProvider with the configured model.
# Why: Confirms correct factory instantiation for Google Gemini.
# Input/Condition: Settings with LLM_PROVIDER="gemini", GOOGLE_API_KEY="AIzaSyDummyTestKey", GEMINI_MODEL="gemini-3.6-flash".
# Pass condition: Returned object is GeminiProvider with matching provider_name and model_name.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_providers.py::test_factory_creates_gemini_provider
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


# Purpose: Verifies that valid OPENAI_API_KEY instantiates OpenAIProvider with the configured model.
# Why: Confirms multi-provider extensibility for OpenAI models.
# Input/Condition: Settings with LLM_PROVIDER="openai", OPENAI_API_KEY="sk-dummy-test-key", OPENAI_MODEL="gpt-4o-mini".
# Pass condition: Returned object is OpenAIProvider with provider_name "openai" and model_name "gpt-4o-mini".
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_providers.py::test_factory_creates_openai_provider
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


# Purpose: Verifies that offline simulator correctly categorizes urgent plumbing requests (burst pipe).
# Why: Ensures deterministic fallback can handle common emergency plumbing requests when offline.
# Input/Condition: Prompt containing description "Pipe burst under the sink flooding water".
# Pass condition: Returns category "Plumbing", urgency "High", confidence >= 0.85, needs_more_information is False.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_providers.py::test_offline_provider_classifies_burst_pipe
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


# Purpose: Verifies that offline simulator detects hazardous electrical requests (sparking socket).
# Why: Ensures urgent safety hazards are classified with High urgency even without external LLM availability.
# Input/Condition: Prompt containing description "Wall socket is sparking and smoke coming out".
# Pass condition: Returns category "Electrical", urgency "High", confidence >= 0.85.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_providers.py::test_offline_provider_classifies_sparking_socket
@pytest.mark.asyncio
async def test_offline_provider_classifies_sparking_socket():
    """Verifies that offline simulator detects electrical issues."""
    provider = OfflineSimulationProvider()
    prompt = "<customer_description>Wall socket is sparking and smoke coming out</customer_description>"
    result = await provider.generate_problem_understanding(prompt, "instruction")

    assert result.category == "Electrical"
    assert result.urgency == "High"
    assert result.confidence >= 0.85


# Purpose: Verifies that offline simulator marks vague descriptions as Unclassified with Unknown urgency.
# Why: Ambiguous descriptions must request clarification rather than guessing a category.
# Input/Condition: Prompt containing description "It is broken please fix".
# Pass condition: Returns category "Unclassified", urgency "Unknown", needs_more_information is True, confidence <= 0.4, and at least 1 follow-up question.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_providers.py::test_offline_provider_vague_input
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


# Purpose: Verifies that GeminiProvider successfully parses a standard Google Gemini API 200 OK JSON response.
# Why: Validates the HTTP integration contract and structured JSON deserialization from the Gemini endpoint.
# Input/Condition: Mocked Google Gemini API returning HTTP 200 with candidates parts containing Plumbing JSON.
# Pass condition: Returns structured result with category="Plumbing", problem_summary matching JSON, urgency="Medium", confidence=0.88.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_providers.py::test_gemini_provider_successful_reasoning
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


# Purpose: Verifies that an HTTP 429 rate limit triggers a retry and succeeds on attempt 2.
# Why: Transient API throttling must not fail user requests if a quick retry can recover.
# Input/Condition: First POST returns HTTP 429 (Rate limit reached), second POST returns HTTP 200 OK.
# Pass condition: Provider retries, returns category="Electrical", and confidence=0.9 without throwing an exception.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_providers.py::test_gemini_provider_transient_retry_success
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


# Purpose: Verifies that HTTP 401 (Invalid API key) fails fast immediately without useless retries.
# Why: Authentication errors are permanent configuration bugs; retrying would waste time and resources.
# Input/Condition: Gemini API returns HTTP 401 with error "API key not valid".
# Pass condition: Raises PermanentProviderError with status_code=401; exactly 1 HTTP call was attempted.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_providers.py::test_gemini_provider_permanent_error_fail_fast
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


# Purpose: Verifies that repeated socket timeouts exhausting maximum attempts raise TransientProviderError.
# Why: Network outages must be classified as transient so the backend orchestrator can trigger recovery or retry later.
# Input/Condition: Gemini POST requests continuously raise httpx.ReadTimeout up to max_attempts (2).
# Pass condition: Raises TransientProviderError.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_providers.py::test_gemini_provider_exhausted_timeout_raises_transient
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

