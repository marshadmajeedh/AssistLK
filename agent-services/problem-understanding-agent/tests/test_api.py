"""Integration tests for FastAPI REST endpoints and internal authentication.

Suite overview:
- What this suite tests: The FastAPI HTTP layer for the Problem Understanding Agent,
  including health probes, agent analysis execution, endpoint routing aliases,
  Pydantic request validation, and internal API key security headers.
- Why these tests are needed: Ensures the HTTP API exposed to the C# backend is robust,
  rejects malformed payloads before processing, enforces internal authentication tokens,
  and provides health status for container orchestrators.
- What is real vs mocked: The FastAPI application, ASGI routing, HTTP request/response
  handling, and schema validation are real. The AI provider is faked/mocked using the
  in-memory mock provider so no live LLM or external network is called.
- Prerequisites: Python virtual environment with dependencies installed.
- PowerShell command to run suite from repository root:
  & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_api.py
"""
from typing import Any
import pytest
from httpx import ASGITransport, AsyncClient
from app.config import Settings, get_settings
from app.main import app


# Purpose: Checks that GET /health returns HTTP 200 with service and provider metadata.
# Why: Health check endpoints are used by Docker and container orchestrators to monitor service availability.
# Scenario: An HTTP GET request is sent to /health without any credentials.
# Pass condition: HTTP status is 200 and JSON contains "healthy", "service", "provider", and "model" keys.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_api.py -k "test_health_endpoint"
@pytest.mark.asyncio
async def test_health_endpoint(async_client: AsyncClient):
    """Verifies Requirement 23: GET /health returns 200 and healthy status."""
    response = await async_client.get("/health")
    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "healthy"
    assert "service" in data
    assert "provider" in data
    assert "model" in data


# Purpose: Checks that POST /agent/execute successfully analyzes a valid service request.
# Why: This is the primary endpoint called by the C# backend to run AI problem understanding.
# Scenario: A valid plumbing problem JSON payload is posted to /agent/execute.
# Pass condition: HTTP 200, success flag is True, category is Plumbing, urgency is classified, and 3 tool runs are recorded.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_api.py -k "test_agent_execute_endpoint_success"
@pytest.mark.asyncio
async def test_agent_execute_endpoint_success(
    async_client: AsyncClient,
    valid_plumbing_request_payload: dict[str, Any],
):
    """Verifies Requirement 24: POST /agent/execute executes LangGraph and returns compliant response."""
    response = await async_client.post("/agent/execute", json=valid_plumbing_request_payload)
    assert response.status_code == 200

    data = response.json()
    assert data["success"] is True
    assert data["requestId"] == valid_plumbing_request_payload["requestId"]

    result = data["result"]
    assert result["category"] == "Plumbing"
    assert "burst" in result["problemSummary"].lower() or "leak" in result["problemSummary"].lower()
    assert result["urgency"] in ("High", "Critical", "Medium")
    assert 0.0 <= result["confidence"] <= 1.0

    metadata = data["metadata"]
    assert metadata["agentName"] == "ProblemUnderstandingAgent"
    assert metadata["durationMs"] >= 0
    assert len(metadata["toolExecutions"]) == 3


# Purpose: Checks that the internal alias path /internal/v1/problem-understanding/analyze functions identically to /agent/execute.
# Why: Guarantees backward compatibility with legacy backend service client routes.
# Scenario: A valid plumbing request payload is posted to the alternative alias route.
# Pass condition: HTTP 200, success is True, and category is Plumbing.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_api.py -k "test_agent_execute_alias_endpoint"
@pytest.mark.asyncio
async def test_agent_execute_alias_endpoint(
    async_client: AsyncClient,
    valid_plumbing_request_payload: dict[str, Any],
):
    """Verifies that the /internal/v1/problem-understanding/analyze alias works identically."""
    response = await async_client.post(
        "/internal/v1/problem-understanding/analyze",
        json=valid_plumbing_request_payload,
    )
    assert response.status_code == 200
    data = response.json()
    assert data["success"] is True
    assert data["result"]["category"] == "Plumbing"


# Purpose: Checks that malformed request payloads fail fast with HTTP 422 Unprocessable Entity.
# Why: Prevents empty or invalid input data from entering and failing the downstream agent pipeline.
# Scenario: POST /agent/execute is called with an invalid payload having an empty description string.
# Pass condition: HTTP status code is 422.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_api.py -k "test_agent_execute_schema_validation_failure"
@pytest.mark.asyncio
async def test_agent_execute_schema_validation_failure(async_client: AsyncClient):
    """Verifies that invalid payloads (missing required fields) return HTTP 422."""
    invalid_payload = {
        "requestId": "invalid-uuid",
        "input": {
            "description": "",  # Empty description is invalid
        },
    }
    response = await async_client.post("/agent/execute", json=invalid_payload)
    assert response.status_code == 422


# Purpose: Checks that internal API key authentication blocks unauthorized calls and accepts valid keys.
# Why: Secures internal microservice endpoints against unauthenticated access when an API key is configured.
# Scenario: Calls without a key, with an invalid key, with X-Internal-Api-Key, and with X-Api-Key.
# Pass condition: Missing or wrong key returns HTTP 401; valid key in either supported header returns HTTP 200.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_api.py -k "test_internal_authentication_enforcement"
@pytest.mark.asyncio
async def test_internal_authentication_enforcement(
    secured_settings: Settings,
    valid_plumbing_request_payload: dict[str, Any],
):
    """
    Verifies Requirement 25: When INTERNAL_API_KEY is configured,
    requests without or with wrong keys return 401; valid keys return 200.
    """
    app.dependency_overrides[get_settings] = lambda: secured_settings
    transport = ASGITransport(app=app)

    async with AsyncClient(transport=transport, base_url="http://test") as client:
        # 1. No key -> 401
        res_no_key = await client.post("/agent/execute", json=valid_plumbing_request_payload)
        assert res_no_key.status_code == 401

        # 2. Wrong key -> 401
        res_wrong_key = await client.post(
            "/agent/execute",
            json=valid_plumbing_request_payload,
            headers={"X-Internal-Api-Key": "wrong-secret-key"},
        )
        assert res_wrong_key.status_code == 401

        # 3. Valid X-Internal-Api-Key -> 200
        res_valid = await client.post(
            "/agent/execute",
            json=valid_plumbing_request_payload,
            headers={"X-Internal-Api-Key": "test-secret-key-12345"},
        )
        assert res_valid.status_code == 200
        assert res_valid.json()["success"] is True

        # 4. Valid X-Api-Key -> 200
        res_valid_alt = await client.post(
            "/agent/execute",
            json=valid_plumbing_request_payload,
            headers={"X-Api-Key": "test-secret-key-12345"},
        )
        assert res_valid_alt.status_code == 200

    app.dependency_overrides.clear()


# Purpose: Checks that image-enhanced multimodal execution requests strictly enforce API key security.
# Why: Multimodal endpoints consume vision processing resources and must be equally authenticated.
# Scenario: A payload containing visualEvidence is sent with missing key, wrong key, valid key, and whitespace/quote-padded key.
# Pass condition: Missing/wrong key returns HTTP 401; valid key returns HTTP 200 with success True.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_api.py -k "test_internal_authentication_multimodal_request_enforcement"
@pytest.mark.asyncio
async def test_internal_authentication_multimodal_request_enforcement(
    secured_settings,
    valid_plumbing_request_payload,
):
    """
    Verifies that image-enhanced (multimodal) execution requests enforce the
    exact same authentication: 401 on missing or wrong key, 200 on matching key.
    """
    multimodal_payload = dict(valid_plumbing_request_payload)
    multimodal_payload["input"] = dict(multimodal_payload["input"])
    multimodal_payload["input"]["visualEvidence"] = [
        {
            "attachmentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "contentType": "image/jpeg",
            "dataBase64": "eHh4eHh4eHh4eHg=",
            "width": 80,
            "height": 40,
        }
    ]

    app.dependency_overrides[get_settings] = lambda: secured_settings
    transport = ASGITransport(app=app)

    async with AsyncClient(transport=transport, base_url="http://test") as client:
        # 1. Missing key on multimodal -> 401
        res_missing = await client.post("/agent/execute", json=multimodal_payload)
        assert res_missing.status_code == 401
        assert "Invalid or missing internal service API key" in res_missing.json()["detail"]

        # 2. Wrong key on multimodal -> 401
        res_wrong = await client.post(
            "/agent/execute",
            json=multimodal_payload,
            headers={"X-Internal-Api-Key": "incorrect-key"},
        )
        assert res_wrong.status_code == 401

        # 3. Matching key on multimodal -> 200
        res_matching = await client.post(
            "/agent/execute",
            json=multimodal_payload,
            headers={"X-Internal-Api-Key": "test-secret-key-12345"},
        )
        assert res_matching.status_code == 200
        assert res_matching.json()["success"] is True

        # 4. Key with surrounding whitespace or quotes -> normalized and accepted
        res_quoted = await client.post(
            "/agent/execute",
            json=multimodal_payload,
            headers={"X-Internal-Api-Key": '  "test-secret-key-12345"  '},
        )
        assert res_quoted.status_code == 200

    app.dependency_overrides.clear()


# Purpose: Checks that /health remains accessible without authentication even when API key security is configured.
# Why: Health check monitors and load balancer probes do not carry authentication headers and must not be blocked.
# Scenario: GET /health is requested while secured_settings (INTERNAL_API_KEY enabled) is active.
# Pass condition: HTTP 200 and response body indicates "healthy" status.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_api.py -k "test_health_endpoint_remains_unauthenticated_under_secured_settings"
@pytest.mark.asyncio
async def test_health_endpoint_remains_unauthenticated_under_secured_settings(
    secured_settings,
):
    """
    Verifies that /health remains unauthenticated and returns 200 OK
    even when INTERNAL_API_KEY is configured.
    """
    app.dependency_overrides[get_settings] = lambda: secured_settings
    transport = ASGITransport(app=app)

    async with AsyncClient(transport=transport, base_url="http://test") as client:
        res = await client.get("/health")
        assert res.status_code == 200
        assert res.json()["status"] == "healthy"

    app.dependency_overrides.clear()

