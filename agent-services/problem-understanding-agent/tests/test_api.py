"""Integration tests for FastAPI REST endpoints and internal authentication."""
from typing import Any
import pytest
from httpx import ASGITransport, AsyncClient
from app.config import Settings, get_settings
from app.main import app


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
