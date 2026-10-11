"""Integration tests for FastAPI REST endpoints (/health, /match/start, /match/resume)."""
from typing import Any, Dict
import pytest
from httpx import AsyncClient


@pytest.mark.asyncio
async def test_health_endpoint(async_client: AsyncClient):
    """Verifies that GET /health returns 200 OK and healthy status."""
    response = await async_client.get("/health")
    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "healthy"
    assert data["service"] == "provider-matching-agent"
    assert "model" in data


@pytest.mark.asyncio
async def test_match_start_success(
    async_client: AsyncClient,
    valid_match_request_payload: Dict[str, Any],
):
    """
    Verifies that POST /match/start accepts candidate providers,
    runs the scoring pipeline, reaches human approval gate, and returns thread_id.
    """
    response = await async_client.post("/match/start", json=valid_match_request_payload)
    assert response.status_code == 200

    data = response.json()
    assert "thread_id" in data
    assert data["status"] == "Pending"
    assert "tokens_consumed" in data
    assert data["tokens_consumed"]["total_tokens"] > 0

    recommended = data["recommended_provider"]
    assert recommended is not None
    assert recommended["name"] == "Kamal Perera"
    assert recommended["score"] > 0
    assert "match_rationale" in recommended
    assert len(recommended["match_rationale"]) > 0


@pytest.mark.asyncio
async def test_match_start_default_fallback_to_tools(async_client: AsyncClient):
    """
    Verifies that when eligible_providers is null, the agent falls back
    to SearchEligibleProviders tool to retrieve candidates.
    """
    payload = {
        "objective": "Need a plumber to fix leaking sink. Category: plumbing, Urgency: 3",
        "urgency": 3,
        "customer_latitude": 6.9270,
        "customer_longitude": 79.8610,
        "eligible_providers": None,
    }
    response = await async_client.post("/match/start", json=payload)
    assert response.status_code == 200

    data = response.json()
    assert "thread_id" in data
    assert data["status"] == "Pending"
    assert data["recommended_provider"] is not None


@pytest.mark.asyncio
async def test_match_start_no_eligible_providers_within_radius(async_client: AsyncClient):
    """
    Verifies that when all providers exceed their operational radius,
    the endpoint returns No_Eligible_Providers without crashing.
    """
    payload = {
        "objective": "Need emergency plumbing. Category: plumbing, Urgency: 5",
        "urgency": 5,
        "customer_latitude": 6.9270,
        "customer_longitude": 79.8610,
        "eligible_providers": [
            {
                "provider_id": "out-of-range-001",
                "name": "Out of Range Provider",
                "rating": 5.0,
                "verified": True,
                "latitude": 9.6615,  # Jaffna (>300 km away)
                "longitude": 80.0255,
                "operating_radius_km": 5.0,
                "skills": ["plumbing"],
            }
        ],
    }
    response = await async_client.post("/match/start", json=payload)
    assert response.status_code == 200

    data = response.json()
    assert data["status"] == "No_Eligible_Providers"
    assert data["recommended_provider"] is None
    assert "operational radius" in data["message"].lower()


@pytest.mark.asyncio
async def test_match_start_schema_validation_failure(async_client: AsyncClient):
    """Verifies that invalid payloads (missing required fields) return HTTP 422."""
    invalid_payload = {
        "urgency": 5,
        # Missing required "objective" field
    }
    response = await async_client.post("/match/start", json=invalid_payload)
    assert response.status_code == 422


@pytest.mark.asyncio
async def test_full_hitl_workflow_start_and_approve(
    async_client: AsyncClient,
    valid_match_request_payload: Dict[str, Any],
):
    """
    End-to-End HITL workflow test:
    1. POST /match/start creates a paused thread awaiting Admin review.
    2. POST /match/resume with 'Approve' completes the workflow with status 'Approved'.
    """
    # 1. Start match
    start_res = await async_client.post("/match/start", json=valid_match_request_payload)
    assert start_res.status_code == 200
    thread_id = start_res.json()["thread_id"]

    # 2. Resume match with Admin approval
    resume_payload = {
        "thread_id": thread_id,
        "action": "Approve",
        "admin_id": "admin-supervisor-42",
    }
    resume_res = await async_client.post("/match/resume", json=resume_payload)
    assert resume_res.status_code == 200

    resume_data = resume_res.json()
    assert resume_data["thread_id"] == thread_id
    assert resume_data["status"] == "Approved"


@pytest.mark.asyncio
async def test_full_hitl_workflow_start_and_reject(
    async_client: AsyncClient,
    valid_match_request_payload: Dict[str, Any],
):
    """
    End-to-End HITL workflow test:
    1. POST /match/start creates a paused thread.
    2. POST /match/resume with 'Reject' completes the workflow with status 'Rejected'.
    """
    # 1. Start match
    start_res = await async_client.post("/match/start", json=valid_match_request_payload)
    assert start_res.status_code == 200
    thread_id = start_res.json()["thread_id"]

    # 2. Resume match with Admin rejection
    resume_payload = {
        "thread_id": thread_id,
        "action": "Reject",
        "admin_id": "admin-supervisor-42",
    }
    resume_res = await async_client.post("/match/resume", json=resume_payload)
    assert resume_res.status_code == 200

    resume_data = resume_res.json()
    assert resume_data["thread_id"] == thread_id
    assert resume_data["status"] == "Rejected"


@pytest.mark.asyncio
async def test_match_resume_lost_checkpoint_graceful_acknowledgment(async_client: AsyncClient):
    """
    Verifies that resuming an unknown or lost thread ID gracefully acknowledges
    the Admin's decision without throwing a 500 error.
    """
    lost_resume_payload = {
        "thread_id": "00000000-0000-0000-0000-000000000000",
        "action": "Approve",
        "admin_id": "admin-supervisor-99",
    }
    response = await async_client.post("/match/resume", json=lost_resume_payload)
    assert response.status_code == 200

    data = response.json()
    assert data["status"] == "Approved"
    assert "acknowledged gracefully" in data["final_outcome"]["note"]


@pytest.mark.asyncio
async def test_match_resume_schema_validation_failure(async_client: AsyncClient):
    """Verifies that an invalid resume payload (missing fields) returns HTTP 422."""
    response = await async_client.post("/match/resume", json={"thread_id": "some-id"})
    assert response.status_code == 422
