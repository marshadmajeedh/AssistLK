"""Shared pytest fixtures and test utilities."""
from typing import Any, AsyncGenerator
import pytest
from httpx import ASGITransport, AsyncClient
from app.config import Settings
from app.main import app
from app.schemas.request import AgentExecutionRequest


@pytest.fixture
def offline_settings() -> Settings:
    """Provides test settings configured for deterministic offline simulation."""
    return Settings(
        SERVICE_NAME="problem-understanding-agent-test",
        ENVIRONMENT="test",
        LLM_PROVIDER="offline",
        ALLOW_OFFLINE_SIMULATION=True,
        INTERNAL_API_KEY=None,
        LLM_TIMEOUT_SECONDS=2.0,
        LLM_MAX_ATTEMPTS=2,
    )


@pytest.fixture
def secured_settings() -> Settings:
    """Provides test settings with internal service key enabled."""
    return Settings(
        SERVICE_NAME="problem-understanding-agent-test",
        ENVIRONMENT="test",
        LLM_PROVIDER="offline",
        ALLOW_OFFLINE_SIMULATION=True,
        INTERNAL_API_KEY="test-secret-key-12345",
        LLM_TIMEOUT_SECONDS=2.0,
        LLM_MAX_ATTEMPTS=2,
    )


@pytest.fixture
def valid_plumbing_request_payload() -> dict[str, Any]:
    """Sample valid request payload for plumbing repair."""
    return {
        "requestId": "9c1b33fa-40ef-4f67-85ae-954992dc7a18",
        "agentName": "ProblemUnderstandingAgent",
        "operation": "analyze-problem",
        "timestamp": "2026-09-11T14:30:00Z",
        "parameters": {},
        "input": {
            "serviceRequestId": "7b8f9e01-2345-6789-abcd-ef0123456789",
            "description": "Water pipe burst in the kitchen causing serious flooding.",
            "locationText": "Colombo 07, Cinnamon Gardens",
            "latitude": 6.9044,
            "longitude": 79.8665,
            "categoryHint": "Plumbing",
            "clarificationHistory": [
                {
                    "round": 1,
                    "question": "Is the main shutoff valve closed?",
                    "answer": "Yes, water main has been isolated."
                }
            ],
        },
    }


@pytest.fixture
def sample_agent_request(valid_plumbing_request_payload: dict[str, Any]) -> AgentExecutionRequest:
    """Constructs parsed AgentExecutionRequest model."""
    return AgentExecutionRequest.model_validate(valid_plumbing_request_payload)


@pytest.fixture
async def async_client(offline_settings: Settings) -> AsyncGenerator[AsyncClient, None]:
    """Async HTTP test client against FastAPI application."""
    from app.config import get_settings
    app.dependency_overrides[get_settings] = lambda: offline_settings
    transport = ASGITransport(app=app)
    async with AsyncClient(transport=transport, base_url="http://test") as client:
        yield client
    app.dependency_overrides.clear()
