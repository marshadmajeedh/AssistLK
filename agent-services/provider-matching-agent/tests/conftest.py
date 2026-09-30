"""Shared pytest fixtures and test configuration for Provider Matching Agent."""
import os
import pytest
from typing import Any, Dict, List, AsyncGenerator
from unittest.mock import MagicMock
from httpx import ASGITransport, AsyncClient
from langchain_core.messages import AIMessage

from app.main import app
import app.agent as agent_module


@pytest.fixture(autouse=True)
def mock_llm_invoke(monkeypatch):
    """
    Globally mocks Google Gemini LLM invoke across all tests.
    Ensures tests are 100% offline, deterministic, and do not consume API quota.
    """
    mock_response = AIMessage(
        content="Top-ranked verified provider closest to the requested location with high rating.",
        usage_metadata={"input_tokens": 45, "output_tokens": 18, "total_tokens": 63}
    )

    class MockLLM:
        def __init__(self, response):
            self._response = response
            self.invocations = []

        def invoke(self, messages, *args, **kwargs):
            self.invocations.append(messages)
            return self._response

    mock_llm_obj = MockLLM(mock_response)
    monkeypatch.setattr(agent_module, "llm", mock_llm_obj)
    return mock_llm_obj


@pytest.fixture
def sample_candidates() -> List[Dict[str, Any]]:
    """Provides a realistic set of provider candidates with varied attributes."""
    return [
        {
            "provider_id": "11111111-1111-1111-1111-111111111111",
            "name": "Kamal Perera",
            "skills": ["plumbing", "pipe repair"],
            "verified": True,
            "rating": 4.8,
            "latitude": 6.9271,
            "longitude": 79.8612,
            "operating_radius_km": 10.0,
        },
        {
            "provider_id": "22222222-2222-2222-2222-222222222222",
            "name": "Nimal Silva",
            "skills": ["plumbing"],
            "verified": False,
            "rating": 4.2,
            "latitude": 6.9350,
            "longitude": 79.8520,
            "operating_radius_km": 8.0,
        },
        {
            "provider_id": "33333333-3333-3333-3333-333333333333",
            "name": "Kasun Fernando",
            "skills": ["electrical", "wiring"],
            "verified": True,
            "rating": 4.9,
            "latitude": 6.9270,
            "longitude": 79.8610,
            "operating_radius_km": 10.0,
        },
        {
            "provider_id": "44444444-4444-4444-4444-444444444444",
            "name": "Far Away Provider",
            "skills": ["plumbing"],
            "verified": True,
            "rating": 5.0,
            "latitude": 7.2906,
            "longitude": 80.6337,
            "operating_radius_km": 10.0,  # Located ~95 km away; well outside 10 km
        }
    ]


@pytest.fixture
def valid_match_request_payload(sample_candidates: List[Dict[str, Any]]) -> Dict[str, Any]:
    """Provides a valid payload for POST /match/start."""
    return {
        "objective": "Urgent plumbing repair needed for leaking bathroom pipe. Urgency: 5, Category: plumbing",
        "urgency": 5,
        "customer_latitude": 6.9270,
        "customer_longitude": 79.8610,
        "eligible_providers": sample_candidates,
    }


@pytest.fixture
async def async_client() -> AsyncGenerator[AsyncClient, None]:
    """Async HTTP test client against the FastAPI application."""
    transport = ASGITransport(app=app)
    async with AsyncClient(transport=transport, base_url="http://test") as client:
        yield client
