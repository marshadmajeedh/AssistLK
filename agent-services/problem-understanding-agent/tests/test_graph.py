"""Integration tests for the LangGraph Problem Understanding state machine."""
import pytest
from app.graphs.problem_graph import create_problem_understanding_graph
from app.providers.base import BaseLLMProvider, LLMProviderResult, TransientProviderError
from app.providers.offline_provider import OfflineSimulationProvider


class FailingMockProvider(BaseLLMProvider):
    """Provider that simulates unrecoverable provider failure for degraded testing."""

    @property
    def provider_name(self) -> str:
        return "failing-mock"

    @property
    def model_name(self) -> str:
        return "mock-fail-v1"

    async def generate_problem_understanding(self, prompt: str, system_instruction: str) -> LLMProviderResult:
        raise TransientProviderError("Simulated LLM network connection timeout.")


@pytest.mark.asyncio
async def test_graph_category_hint_is_non_authoritative():
    """
    Verifies Requirement 7: CategoryHint is non-authoritative.
    Customer hints 'Electrical', but description describes water pipe leak.
    Agent must return 'Plumbing'.
    """
    provider = OfflineSimulationProvider()
    graph = create_problem_understanding_graph(provider)

    initial_state = {
        "request_id": "test-req-1",
        "service_request_id": "test-srv-1",
        "description": "Water is gushing out from the broken pipe under my kitchen sink.",
        "location_text": "Colombo 03",
        "category_hint": "Electrical",  # Conflicting customer hint!
        "clarification_history": [],
    }

    final_state = await graph.ainvoke(initial_state)
    output = final_state["output"]

    assert output["category"] == "Plumbing"
    assert output["category"] != "Electrical"
    assert output["urgency"] == "High"
    assert output["confidence"] >= 0.8


@pytest.mark.asyncio
async def test_graph_evaluates_clarification_history():
    """
    Verifies Requirement 8: Clarification history is incorporated as customer evidence.
    """
    provider = OfflineSimulationProvider()
    graph = create_problem_understanding_graph(provider)

    initial_state = {
        "request_id": "test-req-2",
        "service_request_id": "test-srv-2",
        "description": "Water leaking in bathroom.",
        "location_text": "Galle Fort",
        "clarification_history": [
            {
                "round": 1,
                "question": "Is water actively flooding the floor?",
                "answer": "Yes, water is heavily flooding through the ceiling pipe.",
            }
        ],
    }

    final_state = await graph.ainvoke(initial_state)
    output = final_state["output"]

    assert output["category"] == "Plumbing"
    assert output["needsMoreInformation"] is False


@pytest.mark.asyncio
async def test_graph_empty_input_fast_path():
    """
    Verifies Requirement 13: Empty description conditionally routes straight to finalize.
    """
    provider = OfflineSimulationProvider()
    graph = create_problem_understanding_graph(provider)

    initial_state = {
        "request_id": "test-req-3",
        "service_request_id": "test-srv-3",
        "description": "   ",  # Whitespace-only input
        "clarification_history": [],
    }

    final_state = await graph.ainvoke(initial_state)
    output = final_state["output"]

    assert output["category"] == "Unclassified"
    assert output["urgency"] == "Unknown"
    assert output["needsMoreInformation"] is True
    assert output["confidence"] == 0.0
    assert len(output["followUpQuestions"]) >= 1
    # Tools should NOT have run on empty input
    assert len(final_state.get("tool_executions", [])) == 0


@pytest.mark.asyncio
async def test_graph_ambiguous_input_demands_clarification():
    """
    Verifies that overly brief or generic descriptions trigger NeedsMoreInformation=True.
    """
    provider = OfflineSimulationProvider()
    graph = create_problem_understanding_graph(provider)

    initial_state = {
        "request_id": "test-req-4",
        "service_request_id": "test-srv-4",
        "description": "It is broken.",  # Very short, ambiguous
        "clarification_history": [],
    }

    final_state = await graph.ainvoke(initial_state)
    output = final_state["output"]

    assert output["category"] == "Unclassified"
    assert output["urgency"] == "Unknown"
    assert output["needsMoreInformation"] is True
    assert len(output["followUpQuestions"]) >= 1


@pytest.mark.asyncio
async def test_graph_degraded_fallback_when_provider_fails():
    """
    Verifies Requirement 22: If LLM fails, graph degrades safely:
    Category = Unclassified, Urgency = Unknown, confidence = 0.2, needsMoreInformation = True, Degraded = True.
    """
    provider = FailingMockProvider()
    graph = create_problem_understanding_graph(provider)

    initial_state = {
        "request_id": "test-req-5",
        "service_request_id": "test-srv-5",
        "description": "Water leaking from kitchen pipe.",
        "clarification_history": [],
    }

    final_state = await graph.ainvoke(initial_state)
    output = final_state["output"]

    assert final_state["degraded"] is True
    assert output["category"] == "Unclassified"
    assert output["urgency"] == "Unknown"
    assert output["needsMoreInformation"] is True
    assert output["confidence"] <= 0.4
    assert output["additionalInformation"].get("Degraded") == "True"


@pytest.mark.asyncio
async def test_graph_produces_safe_tool_execution_audits():
    """
    Verifies Requirement 27: Tool execution summaries are recorded safely with timing.
    """
    provider = OfflineSimulationProvider()
    graph = create_problem_understanding_graph(provider)

    initial_state = {
        "request_id": "test-req-6",
        "service_request_id": "test-srv-6",
        "description": "Water pipe burst in kitchen.",
        "location_text": "Colombo 07",
        "latitude": 6.9271,
        "longitude": 79.8612,
        "clarification_history": [],
    }

    final_state = await graph.ainvoke(initial_state)
    tool_executions = final_state.get("tool_executions", [])

    assert len(tool_executions) == 3
    tool_names = [t["tool"] for t in tool_executions]
    assert "LocationExtractionTool" in tool_names
    assert "ProblemClassificationTool" in tool_names
    assert "ServiceKnowledgeTool" in tool_names

    for t in tool_executions:
        assert t["success"] is True
        assert t["durationMs"] >= 0
