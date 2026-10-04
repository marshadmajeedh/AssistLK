"""Unit and workflow integration tests for LangGraph state machine, scoring rules, and HITL gate."""
import uuid
import tempfile
import os
import pytest
from langgraph.types import Command
from langchain_core.messages import AIMessage

import app.agent as agent_module
from app.agent import (
    match_and_score_providers,
    human_approval_gate,
    finalize_workflow,
    PersistentMemorySaver,
    builder,
)


def _build_initial_state(
    objective: str = "Plumbing emergency: burst water pipe in kitchen. Urgency: 5, Category: plumbing",
    urgency: int = 5,
    cust_lat: float = 6.9270,
    cust_lon: float = 79.8610,
    eligible_providers: list = None,
) -> dict:
    """Helper to construct a valid 14-field initial state dictionary."""
    thread_id = str(uuid.uuid4())
    return {
        "WorkflowId": thread_id,
        "Objective": objective,
        "Plan": ["Score Candidates", "Await Admin Approval", "Finalize"],
        "CurrentStep": "Initialized",
        "CompletedSteps": [],
        "ToolResults": [],
        "ValidationResults": [],
        "Errors": [],
        "Retries": 0,
        "ApprovalStatus": "Running",
        "FinalOutcome": None,
        "StartedAt": "2026-09-30T10:00:00Z",
        "UpdatedAt": "2026-09-30T10:00:00Z",
        "CompletedAt": None,
        "customer_latitude": cust_lat,
        "customer_longitude": cust_lon,
        "urgency_level": urgency,
        "eligible_providers": eligible_providers,
    }


# --- Scoring and Urgency Shift Tests ---


def test_urgency_shift_emergency_prioritizes_proximity():
    """
    Verifies that under High Urgency (>= 4), spatial proximity dominates the score (80% weight).
    A closer provider with a modest rating should rank HIGHER than a far provider with 5.0 stars.
    """
    providers = [
        {
            "id": "p-close",
            "name": "Nearby Plumber",
            "rating": 3.8,
            "verified": False,
            "latitude": 6.9271,  # ~0.02 km away
            "longitude": 79.8612,
            "operating_radius_km": 10.0,
            "skills": ["plumbing"],
        },
        {
            "id": "p-far",
            "name": "Far Top Plumber",
            "rating": 5.0,
            "verified": True,
            "latitude": 6.9800,  # ~6.2 km away
            "longitude": 79.8800,
            "operating_radius_km": 10.0,
            "skills": ["plumbing"],
        }
    ]

    state = _build_initial_state(urgency=5, eligible_providers=providers)
    res = match_and_score_providers(state)

    assert res["ApprovalStatus"] == "Pending"
    ranked = res["ToolResults"]
    assert len(ranked) == 2

    # The close provider must win due to 80% proximity weight
    top_pick = res["FinalOutcome"]["recommended_candidate"]
    assert top_pick["name"] == "Nearby Plumber"
    assert ranked[0]["score"] > ranked[1]["score"]


def test_urgency_shift_scheduled_prioritizes_quality():
    """
    Verifies that under Low/Normal Urgency (< 4), provider quality & verification dominate (70% weight).
    A highly rated, verified provider should outrank an unverified, lower-rated nearby provider.
    """
    providers = [
        {
            "id": "p-close-lower-rated",
            "name": "Mediocre Nearby",
            "rating": 3.5,
            "verified": False,
            "latitude": 6.9272,  # ~0.03 km away
            "longitude": 79.8611,
            "operating_radius_km": 10.0,
            "skills": ["plumbing"],
        },
        {
            "id": "p-moderate-dist-elite",
            "name": "Elite Verified Master",
            "rating": 4.9,
            "verified": True,
            "latitude": 6.9350,  # ~1.3 km away
            "longitude": 79.8520,
            "operating_radius_km": 10.0,
            "skills": ["plumbing"],
        }
    ]

    state = _build_initial_state(
        objective="Schedule routine bathroom plumbing inspection next Tuesday. Urgency: 2, Category: plumbing",
        urgency=2,
        eligible_providers=providers
    )
    res = match_and_score_providers(state)

    assert res["ApprovalStatus"] == "Pending"
    top_pick = res["FinalOutcome"]["recommended_candidate"]

    # In scheduled mode (urgency=2), 70% quality weight lifts the elite verified provider to rank 1
    assert top_pick["name"] == "Elite Verified Master"
    assert top_pick["verified"] is True


def test_score_calculation_exact_formulas():
    """
    Verifies the mathematical precision of the dual scoring formulas.
    Urgency >= 4: Score = (norm_proximity * 0.8) + (norm_rating * 0.2)
    Urgency < 4: Score = (effective_quality * 0.7) + (norm_proximity * 0.3)
    """
    provider = [{
        "id": "p-test",
        "name": "Formula Tester",
        "rating": 4.0,
        "verified": True,
        "latitude": 6.9270,  # Distance = 0.0 km
        "longitude": 79.8610,
        "operating_radius_km": 10.0,
        "skills": ["plumbing"],
    }]

    # At distance = 0: norm_proximity = 1.0 / (1.0 + 0) = 1.0
    # norm_rating = 4.0 / 5.0 = 0.8
    # Urgency 5 formula: (1.0 * 0.8) + (0.8 * 0.2) = 0.8 + 0.16 = 0.96
    state_urg5 = _build_initial_state(urgency=5, eligible_providers=provider)
    res_urg5 = match_and_score_providers(state_urg5)
    assert res_urg5["ToolResults"][0]["score"] == 0.96

    # Urgency 2 formula:
    # effective_quality = min(0.8 + 0.1, 1.0) = 0.9
    # Score = (0.9 * 0.7) + (1.0 * 0.3) = 0.63 + 0.30 = 0.93
    state_urg2 = _build_initial_state(
        objective="Routine scheduled plumbing inspection. Category: plumbing, Urgency: 2",
        urgency=2,
        eligible_providers=provider
    )
    res_urg2 = match_and_score_providers(state_urg2)
    assert res_urg2["ToolResults"][0]["score"] == 0.93


def test_scoring_node_does_not_mutate_input_state():
    """Graph nodes must return a new state object instead of mutating checkpoint input."""
    state = _build_initial_state(
        urgency=5,
        eligible_providers=[{
            "id": "p-test",
            "name": "Test Plumber",
            "rating": 4.5,
            "verified": True,
            "latitude": 6.9270,
            "longitude": 79.8610,
            "operating_radius_km": 10.0,
            "skills": ["plumbing"],
        }],
    )
    original_completed_steps = list(state["CompletedSteps"])
    original_updated_at = state["UpdatedAt"]

    result = match_and_score_providers(state)

    assert result is not state
    assert state["CompletedSteps"] == original_completed_steps
    assert state["UpdatedAt"] == original_updated_at
    assert result["CompletedSteps"] == ["match_and_score_providers"]


# --- Operational Radius and Skill Filtering Tests ---


def test_operating_radius_boundary_exclusion():
    """Verifies that providers outside their operating radius are excluded from recommendations."""
    providers = [
        {
            "id": "p-in-radius",
            "name": "Inside Radius",
            "rating": 4.0,
            "latitude": 6.9350,  # ~1.35 km away
            "longitude": 79.8520,
            "operating_radius_km": 5.0,  # 1.35 km <= 5.0 km -> INCLUDED
            "skills": ["plumbing"],
        },
        {
            "id": "p-out-radius",
            "name": "Outside Radius",
            "rating": 5.0,
            "latitude": 6.9800,  # ~6.2 km away
            "longitude": 79.8800,
            "operating_radius_km": 3.0,  # 6.2 km > 3.0 km -> EXCLUDED
            "skills": ["plumbing"],
        }
    ]

    state = _build_initial_state(urgency=3, eligible_providers=providers)
    res = match_and_score_providers(state)

    candidate_names = [c["name"] for c in res["ToolResults"]]
    assert "Inside Radius" in candidate_names
    assert "Outside Radius" not in candidate_names


def test_skill_compatibility_filtering():
    """Verifies that providers lacking relevant skills are filtered out for specific categories."""
    providers = [
        {
            "id": "p-plumber",
            "name": "Professional Plumber",
            "rating": 4.5,
            "latitude": 6.9271,
            "longitude": 79.8612,
            "operating_radius_km": 10.0,
            "skills": ["pipe leak repair", "drainage"],
        },
        {
            "id": "p-electrician",
            "name": "Solar Electrician",
            "rating": 4.9,
            "latitude": 6.9271,
            "longitude": 79.8612,
            "operating_radius_km": 10.0,
            "skills": ["solar wiring", "inverter repair"],
        }
    ]

    state = _build_initial_state(
        objective="Fix pipe leak in kitchen. Category: plumbing, Urgency: 3",
        urgency=3,
        eligible_providers=providers
    )
    res = match_and_score_providers(state)

    candidate_ids = [c["provider_id"] for c in res["ToolResults"]]
    assert "p-plumber" in candidate_ids
    assert "p-electrician" not in candidate_ids


def test_zero_eligible_providers_graceful_handling():
    """
    Verifies that when zero candidates qualify, the system transitions to No_Eligible_Providers
    and does not crash or trigger admin interrupt.
    """
    providers = [
        {
            "id": "p-far-away",
            "name": "Distant Plumber",
            "rating": 5.0,
            "latitude": 8.0000,  # >100 km away
            "longitude": 80.5000,
            "operating_radius_km": 5.0,
            "skills": ["plumbing"],
        }
    ]

    state = _build_initial_state(urgency=4, eligible_providers=providers)
    res = match_and_score_providers(state)

    assert res["ApprovalStatus"] == "No_Eligible_Providers"
    assert res["ToolResults"] == []
    assert res["FinalOutcome"]["recommended_candidate"] is None

    # Passing this state to human_approval_gate should immediately bypass interrupt
    gate_res = human_approval_gate(res)
    assert gate_res["CurrentStep"] == "No_Eligible_Providers"


# --- LLM Rationale and Fallback Tests ---


def test_llm_rationale_generation_and_metadata(mock_llm_invoke):
    """Verifies that LLM rationale and token usage metadata are stored in state."""
    state = _build_initial_state(urgency=5, eligible_providers=[
        {
            "id": "p-kamal",
            "name": "Kamal Perera",
            "rating": 4.8,
            "verified": True,
            "latitude": 6.9271,
            "longitude": 79.8612,
            "operating_radius_km": 10.0,
            "skills": ["plumbing"],
        }
    ])

    res = match_and_score_providers(state)
    top_cand = res["FinalOutcome"]["recommended_candidate"]

    assert top_cand["match_rationale"] != ""
    assert "Top-ranked" in top_cand["match_rationale"]
    assert "usage_metadata" in res
    assert res["usage_metadata"]["total_tokens"] == 63


def test_llm_failure_triggers_fallback_rationale(monkeypatch):
    """Verifies that if the LLM invocation throws an exception, a fallback rationale is created."""
    class FailingLLM:
        def invoke(self, *args, **kwargs):
            raise RuntimeError("API quota exhausted or network failure")

    monkeypatch.setattr(agent_module, "llm", FailingLLM())

    state = _build_initial_state(urgency=5, eligible_providers=[
        {
            "id": "p-kamal",
            "name": "Kamal Perera",
            "rating": 4.8,
            "latitude": 6.9271,
            "longitude": 79.8612,
            "operating_radius_km": 10.0,
            "skills": ["plumbing"],
        }
    ])

    res = match_and_score_providers(state)
    top_cand = res["FinalOutcome"]["recommended_candidate"]

    # Fallback template must be populated without crashing
    assert "Top-ranked match based on distance" in top_cand["match_rationale"]
    assert res["ApprovalStatus"] == "Pending"


# --- LangGraph Interruption and Resumption (HITL) Tests ---


def test_graph_pauses_at_human_approval_gate():
    """
    Verifies that the compiled LangGraph stops and raises an interrupt at human_approval_gate.
    """
    fresh_checkpointer = PersistentMemorySaver(file_path=tempfile.mktemp(suffix=".pkl"))
    test_graph = builder.compile(checkpointer=fresh_checkpointer)

    initial_state = _build_initial_state(urgency=5, eligible_providers=[
        {
            "id": "p-kamal",
            "name": "Kamal Perera",
            "rating": 4.8,
            "latitude": 6.9271,
            "longitude": 79.8612,
            "operating_radius_km": 10.0,
            "skills": ["plumbing"],
        }
    ])

    config = {"configurable": {"thread_id": initial_state["WorkflowId"]}}
    test_graph.invoke(initial_state, config=config)

    # Check graph state at interrupt
    snapshot = test_graph.get_state(config)
    assert snapshot.next == ("human_approval_gate",)
    assert len(snapshot.tasks) > 0
    assert snapshot.tasks[0].interrupts[0].value["message"] == "High-impact match awaiting Admin review"


def test_graph_resume_with_approval():
    """
    Verifies resuming the graph with action='Approve' transitions state to Approved and completes.
    """
    fresh_checkpointer = PersistentMemorySaver(file_path=tempfile.mktemp(suffix=".pkl"))
    test_graph = builder.compile(checkpointer=fresh_checkpointer)

    initial_state = _build_initial_state(urgency=5, eligible_providers=[
        {
            "id": "p-kamal",
            "name": "Kamal Perera",
            "rating": 4.8,
            "latitude": 6.9271,
            "longitude": 79.8612,
            "operating_radius_km": 10.0,
            "skills": ["plumbing"],
        }
    ])

    config = {"configurable": {"thread_id": initial_state["WorkflowId"]}}
    test_graph.invoke(initial_state, config=config)

    # Resume graph with Admin approval
    resumed_state = test_graph.invoke(
        Command(resume={"action": "Approve", "admin_id": "admin-user-001"}),
        config=config
    )

    assert resumed_state["ApprovalStatus"] == "Approved"
    assert resumed_state["CurrentStep"] == "Completed"
    assert "human_approval_gate" in resumed_state["CompletedSteps"]
    assert "finalize_workflow" in resumed_state["CompletedSteps"]
    assert resumed_state["CompletedAt"] is not None


def test_graph_resume_with_rejection():
    """
    Verifies resuming the graph with action='Reject' transitions state to Rejected.
    """
    fresh_checkpointer = PersistentMemorySaver(file_path=tempfile.mktemp(suffix=".pkl"))
    test_graph = builder.compile(checkpointer=fresh_checkpointer)

    initial_state = _build_initial_state(urgency=4, eligible_providers=[
        {
            "id": "p-kamal",
            "name": "Kamal Perera",
            "rating": 4.8,
            "latitude": 6.9271,
            "longitude": 79.8612,
            "operating_radius_km": 10.0,
            "skills": ["plumbing"],
        }
    ])

    config = {"configurable": {"thread_id": initial_state["WorkflowId"]}}
    test_graph.invoke(initial_state, config=config)

    # Resume graph with Admin rejection
    resumed_state = test_graph.invoke(
        Command(resume={"action": "Reject", "admin_id": "admin-user-001"}),
        config=config
    )

    assert resumed_state["ApprovalStatus"] == "Rejected"
    assert resumed_state["CurrentStep"] == "Completed"


# --- PersistentMemorySaver Checkpoint Test ---


def test_persistent_memory_saver_save_and_reload():
    """Verifies that PersistentMemorySaver correctly serializes and restores checkpoints."""
    temp_path = tempfile.mktemp(suffix=".pkl")

    try:
        saver1 = PersistentMemorySaver(file_path=temp_path)
        test_graph = builder.compile(checkpointer=saver1)

        initial_state = _build_initial_state(urgency=5, eligible_providers=[
            {
                "id": "p-kamal",
                "name": "Kamal Perera",
                "rating": 4.8,
                "latitude": 6.9271,
                "longitude": 79.8612,
                "operating_radius_km": 10.0,
                "skills": ["plumbing"],
            }
        ])

        config = {"configurable": {"thread_id": initial_state["WorkflowId"]}}
        test_graph.invoke(initial_state, config=config)

        # File should exist and have data
        assert os.path.exists(temp_path)
        assert os.path.getsize(temp_path) > 0

        # Load fresh saver reading from same file
        saver2 = PersistentMemorySaver(file_path=temp_path)
        loaded_tuple = saver2.get_tuple(config)

        assert loaded_tuple is not None
        assert loaded_tuple.checkpoint is not None
        assert loaded_tuple.checkpoint["channel_values"]["WorkflowId"] == initial_state["WorkflowId"]
    finally:
        if os.path.exists(temp_path):
            os.remove(temp_path)
