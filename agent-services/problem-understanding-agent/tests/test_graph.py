# Purpose: Tests the LangGraph state machine graph for Component 1 Problem Understanding Agent.
# Why: Validates workflow state transitions, tool execution sequencing, customer category hint override, clarification history merging, and error propagation.
# Real vs Mocked: Real LangGraph compiled workflow and deterministic domain tools. The LLM provider is either OfflineSimulationProvider or a FailingMockProvider; no live model calls.
# Prerequisites: Python virtual environment with dependencies installed in agent-services/problem-understanding-agent/.venv.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_graph.py

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


# Purpose: Verifies that customer CategoryHint is non-authoritative and overridden by actual problem description evidence.
# Why: Customers often pick incorrect categories (e.g. Electrical for a plumbing leak); AI reasoning must classify by factual description.
# Input/Condition: category_hint="Electrical", but description="Water is gushing out from the broken pipe under my kitchen sink."
# Pass condition: Output category is "Plumbing" (not "Electrical"), urgency="High", confidence >= 0.8.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_graph.py::test_graph_category_hint_is_non_authoritative
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


# Purpose: Verifies that clarification Q&A history is incorporated into reasoning to resolve ambiguous requests.
# Why: When customers answer follow-up questions, subsequent reasoning must use those answers to reach high confidence.
# Input/Condition: Description "Water leaking in bathroom" + clarification round 1 answering "Yes, water is heavily flooding through the ceiling pipe."
# Pass condition: Output category is "Plumbing" and needsMoreInformation is False (sufficient information reached).
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_graph.py::test_graph_evaluates_clarification_history
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


# Purpose: Verifies that empty/whitespace-only descriptions bypass tool execution and route immediately to finalization.
# Why: Optimizes resource usage: empty inputs cannot be classified by domain tools and must immediately prompt for more info.
# Input/Condition: description="   " (whitespace-only).
# Pass condition: Category is "Unclassified", urgency="Unknown", needsMoreInformation=True, confidence=0.0, and 0 tool executions were recorded.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_graph.py::test_graph_empty_input_fast_path
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


# Purpose: Verifies that overly brief or vague descriptions trigger NeedsMoreInformation=True and follow-up questions.
# Why: Prevents assigning an arbitrary category to ambiguous requests like "It is broken."
# Input/Condition: description="It is broken." without specific keywords.
# Pass condition: Category is "Unclassified", urgency="Unknown", needsMoreInformation=True, followUpQuestions has at least 1 item.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_graph.py::test_graph_ambiguous_input_demands_clarification
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


# Purpose: Verifies that when an LLM provider fails with TransientProviderError, the graph propagates the error rather than inventing a result.
# Why: Fabricating a fake diagnosis when the model is down is unsafe; raising allows the backend orchestrator to recover or retry.
# Input/Condition: Provider configured as FailingMockProvider raising TransientProviderError.
# Pass condition: Graph execution raises TransientProviderError.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_graph.py::test_graph_propagates_provider_error_when_provider_fails
@pytest.mark.asyncio
async def test_graph_propagates_provider_error_when_provider_fails():
    """
    Verifies that when LLM provider fails with a ProviderError (e.g. transient 503/429),
    the graph does NOT fabricate an AI result with NeedsMoreInformation=True.
    Instead, it raises the ProviderError to allow proper backend recovery.
    """
    provider = FailingMockProvider()
    graph = create_problem_understanding_graph(provider)

    initial_state = {
        "request_id": "test-req-5",
        "service_request_id": "test-srv-5",
        "description": "Water leaking from kitchen pipe.",
        "clarification_history": [],
    }

    with pytest.raises(TransientProviderError):
        await graph.ainvoke(initial_state)


# Purpose: Verifies that domain tool executions are audited with tool name, success flag, and execution timing.
# Why: Observability requirement to monitor tool execution performance and failures across pipeline runs.
# Input/Condition: Valid service request with description, location, and GPS.
# Pass condition: Exactly 3 tool executions recorded (LocationExtractionTool, ProblemClassificationTool, ServiceKnowledgeTool), all with success=True and durationMs >= 0.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_graph.py::test_graph_produces_safe_tool_execution_audits
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
