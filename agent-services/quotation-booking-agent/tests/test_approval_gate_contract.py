"""
Approval-gate contract and enforcement tests.

Maps to SE3110 AI sub-items:
  - approval-enforcement testing
  - business-rule compliance
"""

import asyncio

from app.agent import approval_gate_node, finalize_node


# ------------------------------------------------------------------
# TC-C3-AI-15 — Payload contract shape
# ------------------------------------------------------------------
def test_approval_gate_payload_shape(monkeypatch):
    captured = {}

    def fake_interrupt(payload):
        captured["payload"] = payload
        return {"decision": "approve", "remarks": "ok"}

    monkeypatch.setattr("app.agent.interrupt", fake_interrupt)

    state = {
        "quotation_id": 7,
        "service_request_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "provider_id": "e5f340e6-fd9c-4d49-8da5-78e858bce614",
        "total_amount": 1200.0,
        "risk_assessment": {"risk_level": "low", "confidence": 0.9},
    }

    asyncio.run(approval_gate_node(state))

    payload = captured["payload"]
    assert payload["type"] == "quotation_approval"
    assert payload["quotation_id"] == 7
    assert payload["allowed_actions"] == ["approve", "reject"]


# ------------------------------------------------------------------
# TC-C3-AI-16 — Risk assessment is passed to the customer
# ------------------------------------------------------------------
def test_approval_gate_passes_risk_assessment(monkeypatch):
    captured = {}

    def fake_interrupt(payload):
        captured["payload"] = payload
        return {"decision": "approve"}

    monkeypatch.setattr("app.agent.interrupt", fake_interrupt)

    state = {
        "quotation_id": 7,
        "service_request_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "provider_id": "e5f340e6-fd9c-4d49-8da5-78e858bce614",
        "total_amount": 1200.0,
        "risk_assessment": {"risk_level": "high", "confidence": 0.9},
    }

    asyncio.run(approval_gate_node(state))
    assert captured["payload"]["risk_assessment"]["risk_level"] == "high"


# ------------------------------------------------------------------
# TC-C3-AI-17 — llm_error is surfaced when present
# ------------------------------------------------------------------
def test_approval_gate_surfaces_llm_error(monkeypatch):
    captured = {}

    def fake_interrupt(payload):
        captured["payload"] = payload
        return {"decision": "approve"}

    monkeypatch.setattr("app.agent.interrupt", fake_interrupt)

    state = {
        "quotation_id": 7,
        "service_request_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "provider_id": "e5f340e6-fd9c-4d49-8da5-78e858bce614",
        "total_amount": 1200.0,
        "risk_assessment": None,
        "llm_error": "simulated outage",
    }

    asyncio.run(approval_gate_node(state))
    assert captured["payload"]["llm_error"] == "simulated outage"
    assert captured["payload"]["risk_assessment"] is None


# ------------------------------------------------------------------
# TC-C3-AI-18 — Finalize does not create a booking
# ------------------------------------------------------------------
def test_finalize_never_creates_booking():
    result = asyncio.run(
        finalize_node({"quotation_id": 7, "customer_decision": "approve"})
    )
    assert result["booking_id"] is None
    assert result["booking_status"] is None


# ------------------------------------------------------------------
# TC-C3-AI-19 — Finalize handles unknown decision safely
# ------------------------------------------------------------------
def test_finalize_unknown_decision_returns_error():
    result = asyncio.run(
        finalize_node({"quotation_id": 7, "customer_decision": "auto_approve"})
    )
    assert "error" in result
    assert result["final_message"] == "Workflow ended without a valid decision."


# ------------------------------------------------------------------
# TC-C3-AI-20 — The agent cannot approve on its own
# ------------------------------------------------------------------
def test_agent_does_not_call_approve_quotation_directly(monkeypatch):
    """
    Static check: agent.py must not import approve_quotation or reject_quotation
    from tools.py, per the architectural rule in tools.py's docstring.
    """
    import inspect
    import app.agent as agent_module

    source = inspect.getsource(agent_module)

    assert "approve_quotation" not in source, (
        "agent.py must not call approve_quotation directly — the .NET side owns approval."
    )
    assert "reject_quotation" not in source, (
        "agent.py must not call reject_quotation directly — the .NET side owns rejection."
    )