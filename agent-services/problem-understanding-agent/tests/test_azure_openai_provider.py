# Purpose: Tests Azure OpenAI provider integration, factory selection, error handling/retries, multimodal reasoning, and FastAPI execution.
# Why: Validates that AssistLK can use Azure OpenAI as an alternative LLM backend with full feature parity (text, images, retries, LangGraph execution).
# Real vs Mocked: Real FastAPI app, ASGI client, LangGraph execution, and Pydantic parsing. Azure OpenAI HTTP endpoint is mocked with `respx` (no live Azure credentials or calls).
# Prerequisites: Python virtual environment with dependencies installed in agent-services/problem-understanding-agent/.venv.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_azure_openai_provider.py

"""Unit and integration tests for Azure OpenAI provider support, factory selection, retries, and multimodal reasoning."""
import base64
import json
from uuid import uuid4
import httpx
import pytest
import respx
from httpx import ASGITransport, AsyncClient

from app.config import Settings, get_settings
from app.graphs.problem_graph import create_problem_understanding_graph
from app.main import app
from app.providers.azure_openai_provider import AzureOpenAIProvider
from app.providers.base import PermanentProviderError, TransientProviderError
from app.providers.factory import ProviderFactory
from app.providers.gemini_provider import GeminiProvider
from app.providers.offline_provider import OfflineSimulationProvider
from app.providers.openai_provider import OpenAIProvider
from app.schemas.visual_evidence import VisualEvidence

AZURE_ENDPOINT = "https://test-assistlk.openai.azure.com"
DEPLOYMENT = "gpt-4.1-mini"
API_VERSION = "2025-01-01-preview"
AZURE_CHAT_URL = f"{AZURE_ENDPOINT}/openai/deployments/{DEPLOYMENT}/chat/completions?api-version={API_VERSION}"


# ==============================================================================
# 1. Provider Selection & Configuration Tests
# ==============================================================================

# Purpose: Confirms that LLM_PROVIDER="gemini" creates a GeminiProvider instance.
# Why: Ensures default Gemini configuration is preserved when multiple providers are supported.
# Input/Condition: Settings with LLM_PROVIDER="gemini", GOOGLE_API_KEY="AIzaSyTestKey", GEMINI_MODEL="gemini-3.6-flash".
# Pass condition: Resulting provider is GeminiProvider with provider_name="gemini".
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_azure_openai_provider.py::test_factory_selects_gemini_by_default
def test_factory_selects_gemini_by_default():
    """Confirms LLM_PROVIDER=gemini selects GeminiProvider."""
    settings = Settings(
        LLM_PROVIDER="gemini",
        GOOGLE_API_KEY="AIzaSyTestKey",
        GEMINI_MODEL="gemini-3.6-flash",
    )
    provider = ProviderFactory.create_provider(settings)
    assert isinstance(provider, GeminiProvider)
    assert provider.provider_name == "gemini"
    assert provider.model_name == "gemini-3.6-flash"


# Purpose: Confirms that LLM_PROVIDER="azure_openai" creates an AzureOpenAIProvider instance with multimodal support enabled.
# Why: Validates the factory routing and deployment configuration for Azure OpenAI.
# Input/Condition: Settings with LLM_PROVIDER="azure_openai" and valid Azure connection parameters.
# Pass condition: Resulting provider is AzureOpenAIProvider with provider_name="azure_openai", model_name matching deployment, and supports_images=True.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_azure_openai_provider.py::test_factory_selects_azure_openai
def test_factory_selects_azure_openai():
    """Confirms LLM_PROVIDER=azure_openai selects AzureOpenAIProvider with configured deployment."""
    settings = Settings(
        LLM_PROVIDER="azure_openai",
        AZURE_OPENAI_API_KEY="test-azure-key",
        AZURE_OPENAI_ENDPOINT=AZURE_ENDPOINT,
        AZURE_OPENAI_DEPLOYMENT_NAME=DEPLOYMENT,
        AZURE_OPENAI_API_VERSION=API_VERSION,
    )
    provider = ProviderFactory.create_provider(settings)
    assert isinstance(provider, AzureOpenAIProvider)
    assert provider.provider_name == "azure_openai"
    assert provider.model_name == DEPLOYMENT
    assert provider.supports_images is True


# Purpose: Confirms that existing openai and offline provider configurations remain intact alongside Azure.
# Why: Prevents regressions in standard OpenAI and offline simulator factory creation.
# Input/Condition: Settings for offline and direct openai providers.
# Pass condition: Factory creates OfflineSimulationProvider and OpenAIProvider respectively.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_azure_openai_provider.py::test_factory_preserves_openai_and_offline
def test_factory_preserves_openai_and_offline():
    """Confirms existing openai and offline providers remain intact."""
    s_offline = Settings(LLM_PROVIDER="offline")
    assert isinstance(ProviderFactory.create_provider(s_offline), OfflineSimulationProvider)

    s_openai = Settings(LLM_PROVIDER="openai", OPENAI_API_KEY="sk-test", OPENAI_MODEL="gpt-4o-mini")
    p_openai = ProviderFactory.create_provider(s_openai)
    assert isinstance(p_openai, OpenAIProvider)
    assert p_openai.provider_name == "openai"


# Purpose: Confirms missing Azure credentials fail fast with PermanentProviderError when offline simulation is disabled.
# Why: Prevents silent failures or misconfigurations in strict production deployments.
# Input/Condition: LLM_PROVIDER="azure_openai", AZURE_OPENAI_API_KEY=None, ALLOW_OFFLINE_SIMULATION=False.
# Pass condition: Raises PermanentProviderError stating keys are required.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_azure_openai_provider.py::test_factory_missing_azure_config_fails_fast_when_offline_disallowed
def test_factory_missing_azure_config_fails_fast_when_offline_disallowed():
    """Confirms missing Azure config raises PermanentProviderError when offline simulation is disabled."""
    settings = Settings(
        LLM_PROVIDER="azure_openai",
        AZURE_OPENAI_API_KEY=None,
        AZURE_OPENAI_ENDPOINT=None,
        ALLOW_OFFLINE_SIMULATION=False,
    )
    with pytest.raises(PermanentProviderError) as exc_info:
        ProviderFactory.create_provider(settings)
    assert "AZURE_OPENAI_API_KEY and AZURE_OPENAI_ENDPOINT are required" in str(exc_info.value)


# Purpose: Confirms missing Azure credentials fall back to offline simulation when allowed.
# Why: Supports seamless developer workflow when Azure OpenAI credentials are not configured locally.
# Input/Condition: LLM_PROVIDER="azure_openai", AZURE_OPENAI_API_KEY=None, ALLOW_OFFLINE_SIMULATION=True.
# Pass condition: Factory returns OfflineSimulationProvider with model_name="offline-azure-sim".
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_azure_openai_provider.py::test_factory_missing_azure_config_engages_simulator_when_allowed
def test_factory_missing_azure_config_engages_simulator_when_allowed():
    """Confirms missing Azure config falls back to offline simulation in dev/test mode."""
    settings = Settings(
        LLM_PROVIDER="azure_openai",
        AZURE_OPENAI_API_KEY=None,
        AZURE_OPENAI_ENDPOINT=None,
        ALLOW_OFFLINE_SIMULATION=True,
    )
    provider = ProviderFactory.create_provider(settings)
    assert isinstance(provider, OfflineSimulationProvider)
    assert provider.model_name == "offline-azure-sim"


# Purpose: Confirms endpoint URL without http:// or https:// scheme is rejected.
# Why: Prevents invalid HTTP requests and misconfigured base URLs.
# Input/Condition: endpoint="invalid-url-without-scheme".
# Pass condition: Raises PermanentProviderError with scheme validation message.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_azure_openai_provider.py::test_azure_provider_validates_endpoint_format
def test_azure_provider_validates_endpoint_format():
    """Confirms endpoint without http:// or https:// is rejected."""
    with pytest.raises(PermanentProviderError) as exc:
        AzureOpenAIProvider(api_key="key", endpoint="invalid-url-without-scheme")
    assert "must start with https:// or http://" in str(exc.value)


# ==============================================================================
# 2. Azure Reasoning & Output Mapping Tests
# ==============================================================================

# Purpose: Verifies that AzureOpenAIProvider executes a text-only chat completion and maps response to LLMProviderResult.
# Why: Validates the core text reasoning flow against the Azure OpenAI JSON response schema.
# Input/Condition: Mocked Azure chat completion returning structured Plumbing analysis JSON.
# Pass condition: Result has category="Plumbing", urgency="Medium", confidence=0.89, needs_more_information=False.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_azure_openai_provider.py::test_azure_text_reasoning_success_and_dto_mapping
@pytest.mark.asyncio
@respx.mock
async def test_azure_text_reasoning_success_and_dto_mapping():
    """
    Verifies that AzureOpenAIProvider executes a chat completion and parses
    standard structured C1 result into LLMProviderResult.
    """
    mock_body = {
        "choices": [
            {
                "message": {
                    "content": json.dumps({
                        "category": "Plumbing",
                        "problemSummary": "Bathroom sink drain pipe is leaking continuously.",
                        "urgency": "Medium",
                        "needsMoreInformation": False,
                        "followUpQuestions": [],
                        "confidence": 0.89,
                        "scope": "Repair connection between sink drain and pipe.",
                        "safetyAdvisories": ["Keep area ventilated and mop up standing water."]
                    })
                }
            }
        ]
    }

    respx.post(AZURE_CHAT_URL).mock(return_value=httpx.Response(200, json=mock_body))

    http_client = httpx.AsyncClient()
    provider = AzureOpenAIProvider(
        api_key="test-key",
        endpoint=AZURE_ENDPOINT,
        deployment_name=DEPLOYMENT,
        api_version=API_VERSION,
        client=http_client,
    )

    result = await provider.generate_problem_understanding(
        prompt="Bathroom sink drain pipe is leaking continuously.",
        system_instruction="Analyze the customer service request.",
    )

    assert result.category == "Plumbing"
    assert result.urgency == "Medium"
    assert result.confidence == 0.89
    assert result.needs_more_information is False
    assert len(result.follow_up_questions) == 0
    assert "drain pipe is leaking" in result.problem_summary


# Purpose: Verifies that AzureOpenAIProvider formats visual evidence as base64 data URLs in user messages and parses visionStatus='used'.
# Why: Validates multimodal analysis capability of the Azure OpenAI adapter.
# Input/Condition: Problem description plus base64 JPEG visual evidence; mocked Azure response with visionStatus='used'.
# Pass condition: Outgoing payload has image_url block with data:image/jpeg;base64,... and parsed result has vision_status="used".
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_azure_openai_provider.py::test_azure_multimodal_image_reasoning
@pytest.mark.asyncio
@respx.mock
async def test_azure_multimodal_image_reasoning():
    """
    Verifies that AzureOpenAIProvider formats visual evidence as image_url
    and validates multimodal result with visionStatus='used'.
    """
    att_id = uuid4()
    mock_body = {
        "choices": [
            {
                "message": {
                    "content": json.dumps({
                        "category": "Plumbing",
                        "problemSummary": "Kitchen sink P-trap joint is leaking water.",
                        "urgency": "Medium",
                        "needsMoreInformation": False,
                        "followUpQuestions": [],
                        "confidence": 0.85,
                        "visionStatus": "used",
                        "attachmentIdsUsed": [str(att_id)],
                        "visualObservations": [
                            {"attachmentId": str(att_id), "observation": "Moisture visible on PVC drain trap."}
                        ],
                        "visualLimitations": [],
                        "visualAmbiguityResolved": True,
                        "textImageConflict": False,
                    })
                }
            }
        ]
    }

    route = respx.post(AZURE_CHAT_URL).mock(return_value=httpx.Response(200, json=mock_body))

    http_client = httpx.AsyncClient()
    provider = AzureOpenAIProvider(
        api_key="test-key",
        endpoint=AZURE_ENDPOINT,
        deployment_name=DEPLOYMENT,
        api_version=API_VERSION,
        client=http_client,
    )

    evidence = [
        VisualEvidence(
            attachmentId=att_id,
            contentType="image/jpeg",
            dataBase64=base64.b64encode(b"dummy-image-bytes").decode("ascii"),
            width=200,
            height=150,
        )
    ]

    result = await provider.generate_problem_understanding(
        prompt="Kitchen sink leaking underneath.",
        system_instruction="Analyze problem with images.",
        visual_evidence=evidence,
    )

    assert result.vision_status == "used"
    assert len(result.visual_observations) == 1
    assert result.visual_observations[0].attachment_id == att_id
    assert "PVC drain trap" in result.visual_observations[0].observation

    # Verify that image_url was sent to Azure endpoint
    sent_request = route.calls.last.request
    body_json = json.loads(sent_request.content.decode("utf-8"))
    user_msg = body_json["messages"][1]["content"]
    assert isinstance(user_msg, list)
    image_items = [item for item in user_msg if item.get("type") == "image_url"]
    assert len(image_items) == 1
    assert "data:image/jpeg;base64," in image_items[0]["image_url"]["url"]


# ==============================================================================
# 3. Azure Resilience & Error Handling Tests
# ==============================================================================

# Purpose: Verifies that Azure HTTP 429 rate limit triggers bounded retry and succeeds on attempt 2.
# Why: Ensures temporary Azure OpenAI throttling is recovered automatically without failing user requests.
# Input/Condition: First attempt returns 429 Rate Limit, second attempt returns 200 OK.
# Pass condition: Request succeeds with category="Electrical", route was called exactly 2 times.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_azure_openai_provider.py::test_azure_http_429_rate_limit_with_retry
@pytest.mark.asyncio
@respx.mock
async def test_azure_http_429_rate_limit_with_retry():
    """Verifies that Azure HTTP 429 triggers bounded retry and transient classification."""
    success_body = {
        "choices": [
            {
                "message": {
                    "content": json.dumps({
                        "category": "Electrical",
                        "problemSummary": "Socket spark issue.",
                        "urgency": "High",
                        "needsMoreInformation": False,
                        "confidence": 0.9,
                    })
                }
            }
        ]
    }

    # Attempt 1: 429 Rate Limit, Attempt 2: 200 OK
    route = respx.post(AZURE_CHAT_URL).mock(
        side_effect=[
            httpx.Response(429, json={"error": {"message": "Rate limit exceeded"}}, headers={"retry-after": "0.1"}),
            httpx.Response(200, json=success_body),
        ]
    )

    http_client = httpx.AsyncClient()
    provider = AzureOpenAIProvider(
        api_key="test-key",
        endpoint=AZURE_ENDPOINT,
        deployment_name=DEPLOYMENT,
        api_version=API_VERSION,
        timeout_seconds=2.0,
        max_attempts=2,
        client=http_client,
    )

    result = await provider.generate_problem_understanding("Socket is sparking.", "Instruction")
    assert result.category == "Electrical"
    assert route.call_count == 2


# Purpose: Verifies that exhausting maximum attempts on 429 raises TransientProviderError with status_code=429.
# Why: Distinguishes temporary throttling from permanent bugs so the orchestrator knows it is transient.
# Input/Condition: Both attempts return 429 Rate Limit.
# Pass condition: Raises TransientProviderError with status_code=429 after 2 attempts.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_azure_openai_provider.py::test_azure_http_429_exhausted_raises_transient_error
@pytest.mark.asyncio
@respx.mock
async def test_azure_http_429_exhausted_raises_transient_error():
    """Verifies that exhausting attempts on 429 raises TransientProviderError with status_code 429."""
    route = respx.post(AZURE_CHAT_URL).mock(
        return_value=httpx.Response(429, json={"error": {"message": "Rate limit reached"}})
    )

    http_client = httpx.AsyncClient()
    provider = AzureOpenAIProvider(
        api_key="test-key",
        endpoint=AZURE_ENDPOINT,
        deployment_name=DEPLOYMENT,
        api_version=API_VERSION,
        timeout_seconds=1.0,
        max_attempts=2,
        client=http_client,
    )

    with pytest.raises(TransientProviderError) as exc:
        await provider.generate_problem_understanding("Socket is sparking.", "Instruction")

    assert exc.value.status_code == 429
    assert route.call_count == 2


# Purpose: Verifies that Azure 503 (Service Unavailable) raises TransientProviderError after retries.
# Why: Server-side downtime on Azure is transient and must trigger backend recovery mechanisms.
# Input/Condition: Both attempts return HTTP 503.
# Pass condition: Raises TransientProviderError with status_code=503 after 2 attempts.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_azure_openai_provider.py::test_azure_http_503_service_unavailable_raises_transient_error
@pytest.mark.asyncio
@respx.mock
async def test_azure_http_503_service_unavailable_raises_transient_error():
    """Verifies that Azure 503 triggers bounded retry and raises TransientProviderError."""
    route = respx.post(AZURE_CHAT_URL).mock(
        return_value=httpx.Response(503, json={"error": {"message": "Service unavailable"}})
    )

    http_client = httpx.AsyncClient()
    provider = AzureOpenAIProvider(
        api_key="test-key",
        endpoint=AZURE_ENDPOINT,
        deployment_name=DEPLOYMENT,
        api_version=API_VERSION,
        timeout_seconds=1.0,
        max_attempts=2,
        client=http_client,
    )

    with pytest.raises(TransientProviderError) as exc:
        await provider.generate_problem_understanding("Leak in sink.", "Instruction")

    assert exc.value.status_code == 503
    assert route.call_count == 2


# Purpose: Verifies that Azure HTTP 401 (Invalid API key) fails fast without retrying.
# Why: Invalid credentials cannot succeed on retry and should fail immediately.
# Input/Condition: HTTP 401 with invalid API key response.
# Pass condition: Raises PermanentProviderError with status_code=401; exactly 1 call was made.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_azure_openai_provider.py::test_azure_http_401_fails_fast_immediately
@pytest.mark.asyncio
@respx.mock
async def test_azure_http_401_fails_fast_immediately():
    """Verifies that Azure HTTP 401 (Authentication failure) fails fast without retrying."""
    route = respx.post(AZURE_CHAT_URL).mock(
        return_value=httpx.Response(401, json={"error": {"message": "Invalid API key"}})
    )

    http_client = httpx.AsyncClient()
    provider = AzureOpenAIProvider(
        api_key="invalid-key",
        endpoint=AZURE_ENDPOINT,
        deployment_name=DEPLOYMENT,
        api_version=API_VERSION,
        timeout_seconds=2.0,
        max_attempts=2,
        client=http_client,
    )

    with pytest.raises(PermanentProviderError) as exc:
        await provider.generate_problem_understanding("Leak in sink.", "Instruction")

    assert exc.value.status_code == 401
    assert route.call_count == 1  # No retries on 401!


# Purpose: Verifies that invalid or non-JSON output from the model raises PermanentProviderError.
# Why: Malformed model outputs violate data contracts and cannot be parsed into typed schemas.
# Input/Condition: Mocked Azure response containing non-JSON content "Not a JSON object".
# Pass condition: Raises PermanentProviderError.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_azure_openai_provider.py::test_azure_invalid_json_response_raises_permanent
@pytest.mark.asyncio
@respx.mock
async def test_azure_invalid_json_response_raises_permanent():
    """Verifies that invalid or non-JSON output raises PermanentProviderError."""
    respx.post(AZURE_CHAT_URL).mock(
        return_value=httpx.Response(200, json={"choices": [{"message": {"content": "Not a JSON object"}}]})
    )

    http_client = httpx.AsyncClient()
    provider = AzureOpenAIProvider(
        api_key="test-key",
        endpoint=AZURE_ENDPOINT,
        deployment_name=DEPLOYMENT,
        api_version=API_VERSION,
        client=http_client,
    )

    with pytest.raises(PermanentProviderError):
        await provider.generate_problem_understanding("Broken tap.", "Instruction")


# ==============================================================================
# 4. End-to-End FastAPI /agent/execute with Azure OpenAI Provider
# ==============================================================================

# Purpose: Verifies that FastAPI /agent/execute endpoint works end-to-end with Azure OpenAI provider configured.
# Why: Confirms the complete API stack (FastAPI -> LangGraph -> Tools -> AzureOpenAIProvider) executes seamlessly.
# Input/Condition: POST /agent/execute with valid AgentExecutionRequest and mocked Azure OpenAI endpoint.
# Pass condition: Returns HTTP 200, success=True, category="Plumbing", and metadata.provider="azure_openai".
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_azure_openai_provider.py::test_fastapi_endpoint_with_azure_provider
@pytest.mark.asyncio
@respx.mock
async def test_fastapi_endpoint_with_azure_provider():
    """
    Verifies that setting LLM_PROVIDER=azure_openai executes the LangGraph state machine
    via FastAPI /agent/execute and returns compliant metadata with provider='azure_openai'.
    """
    mock_azure_json = {
        "choices": [
            {
                "message": {
                    "content": json.dumps({
                        "category": "Plumbing",
                        "problemSummary": "Continuous water leak from kitchen basin pipe.",
                        "urgency": "Medium",
                        "needsMoreInformation": False,
                        "followUpQuestions": [],
                        "confidence": 0.88,
                    })
                }
            }
        ]
    }

    respx.post(AZURE_CHAT_URL).mock(return_value=httpx.Response(200, json=mock_azure_json))

    azure_settings = Settings(
        SERVICE_NAME="problem-understanding-agent-test",
        ENVIRONMENT="test",
        LLM_PROVIDER="azure_openai",
        AZURE_OPENAI_API_KEY="test-azure-secret",
        AZURE_OPENAI_ENDPOINT=AZURE_ENDPOINT,
        AZURE_OPENAI_DEPLOYMENT_NAME=DEPLOYMENT,
        AZURE_OPENAI_API_VERSION=API_VERSION,
        ALLOW_OFFLINE_SIMULATION=False,
        INTERNAL_API_KEY=None,
        LLM_TIMEOUT_SECONDS=2.0,
        LLM_MAX_ATTEMPTS=2,
    )

    app.dependency_overrides[get_settings] = lambda: azure_settings
    transport = ASGITransport(app=app)

    async with AsyncClient(transport=transport, base_url="http://test") as client:
        # Check /health reports azure_openai
        health_res = await client.get("/health")
        assert health_res.status_code == 200
        health_data = health_res.json()
        assert health_data["provider"] == "azure_openai"
        assert health_data["model"] == DEPLOYMENT

        # Execute agent
        req_payload = {
            "requestId": str(uuid4()),
            "agentName": "ProblemUnderstandingAgent",
            "operation": "analyze-problem",
            "input": {
                "serviceRequestId": str(uuid4()),
                "description": "Continuous water leak from kitchen basin pipe.",
                "clarificationHistory": [],
                "visualEvidence": [],
            },
        }

        response = await client.post("/agent/execute", json=req_payload)
        assert response.status_code == 200
        data = response.json()

        assert data["success"] is True
        assert data["result"]["category"] == "Plumbing"
        assert data["result"]["needsMoreInformation"] is False
        assert data["metadata"]["provider"] == "azure_openai"
        assert len(data["metadata"]["toolExecutions"]) == 3

    app.dependency_overrides.clear()

