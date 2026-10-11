"""
Golden-case tests for the Quotation & Booking workflow.

These tests exercise the LangGraph nodes directly (unit tests) and the
FastAPI endpoints via an in-process ASGI transport (integration tests).

They do NOT require a running .NET API or PostgreSQL instance:
  - The only outbound backend call (get_quotation) is wrapped in try/except
    inside validate_node, so backend failures fall back to the locally
    computed total without crashing the workflow.
  - The HTTP-level test exercises the FastAPI app in-process using
    httpx.ASGITransport, so no external network is required.

Coverage:
  1. Validation rejects non-positive amounts
  2. Validation accepts valid items and computes the total
  3. Approval gate fires the interrupt and captures the customer decision
  4. Finalize returns the approval outcome (booking is created by .NET)
  5. Finalize returns the rejection outcome
  6. HTTP endpoints enforce the internal API key and complete the
     start → interrupt → resume → finalize cycle
"""

import asyncio

import httpx

from app.agent import (
    approval_gate_node,
    finalize_node,
    validate_node,
)
from app.main import app


# ------------------------------------------------------------------
# 1. Validation — rejects invalid amounts
# ------------------------------------------------------------------
def test_validate_node_rejects_invalid_amounts():
    state = {
        "quotation_id": 42,
        "items": [
            {"description": "Brake pad", "amount": 0, "quantity": 2},
        ],
    }

    result = asyncio.run(validate_node(state))

    assert result["validation_passed"] is False
    assert any(
        "amount must be positive" in msg
        for msg in result["validation_errors"]
    )
    assert result["total_amount"] == 0.0


# ------------------------------------------------------------------
# 2. Validation — accepts valid items and computes the total
# ------------------------------------------------------------------
def test_validate_node_accepts_valid_items_and_computes_total():
    state = {
        "quotation_id": 42,
        "items": [
            {"description": "Oil change", "amount": 50, "quantity": 2},
            {"description": "Air filter", "amount": 25, "quantity": 2},
        ],
    }

    result = asyncio.run(validate_node(state))

    assert result["validation_passed"] is True
    assert result["validation_errors"] == []
    assert result["total_amount"] == 150.0


# ------------------------------------------------------------------
# 3. Approval gate — interrupt fires, decision is captured
# ------------------------------------------------------------------
def test_approval_gate_interrupts_and_returns_customer_decision(monkeypatch):
    # Patch interrupt to emulate a resume payload coming from the caller.
    monkeypatch.setattr(
        "app.agent.interrupt",
        lambda payload: {"decision": "approve", "remarks": "Looks good"},
    )

    state = {
        "quotation_id": 99,
        "service_request_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "provider_id": "e5f340e6-fd9c-4d49-8da5-78e858bce614",
        "total_amount": 120.0,
    }

    result = asyncio.run(approval_gate_node(state))

    assert result["approval_requested"] is True
    assert result["customer_decision"] == "approve"
    assert result["customer_remarks"] == "Looks good"


# ------------------------------------------------------------------
# 4. Finalize — approval outcome (booking is created by .NET side)
# ------------------------------------------------------------------
def test_finalize_node_returns_approval_for_backend_to_apply():
    result = asyncio.run(
        finalize_node(
            {
                "quotation_id": 99,
                "customer_decision": "approve",
                "customer_remarks": "Looks good",
            }
        )
    )

    assert result["booking_id"] is None
    assert result["booking_status"] is None
    assert result["final_message"] == "Customer approved the quotation."


# ------------------------------------------------------------------
# 5. Finalize — rejection outcome
# ------------------------------------------------------------------
def test_finalize_node_returns_rejection_for_backend_to_apply():
    result = asyncio.run(
        finalize_node(
            {
                "quotation_id": 99,
                "customer_decision": "reject",
                "customer_remarks": "Price too high",
            }
        )
    )

    assert result["booking_id"] is None
    assert result["booking_status"] is None
    assert result["rejection_reason"] == "Price too high"
    assert result["final_message"] == "Customer rejected the quotation."


# ------------------------------------------------------------------
# 6. HTTP-level golden path — internal API key + full lifecycle
# ------------------------------------------------------------------
def test_http_workflow_requires_internal_key_and_returns_final_state(monkeypatch):
    async def exercise_workflow():
        transport = httpx.ASGITransport(app=app)
        async with httpx.AsyncClient(
            transport=transport, base_url="http://test"
        ) as client:
            payload = {
                "quotation_id": 99,
                "service_request_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                "provider_id": "e5f340e6-fd9c-4d49-8da5-78e858bce614",
                "items": [
                    {"description": "Repair", "amount": 60, "quantity": 2},
                ],
            }

            # Turn on the internal key check for this test only.
            monkeypatch.setenv("INTERNAL_API_KEY", "test-internal-key")

            # (a) Request without the header → 401
            unauthorized = await client.post("/workflows/start", json=payload)
            assert unauthorized.status_code == 401, unauthorized.text

            # (b) Request with the correct header → 200 + waiting_for_approval
            headers = {"X-Internal-Api-Key": "test-internal-key"}
            started = await client.post(
                "/workflows/start", json=payload, headers=headers
            )
            assert started.status_code == 200, started.text
            start_result = started.json()
            assert start_result["status"] == "waiting_for_approval"
            assert "thread_id" in start_result

            # (c) Resume with the same thread_id → 200 + completed
            resumed = await client.post(
                "/workflows/resume",
                json={
                    "thread_id": start_result["thread_id"],
                    "decision": "approve",
                    "remarks": "Approved",
                },
                headers=headers,
            )
            assert resumed.status_code == 200, resumed.text
            final_state = resumed.json()["final_state"]
            assert final_state["quotation_id"] == 99
            assert final_state["customer_decision"] == "approve"

    asyncio.run(exercise_workflow())