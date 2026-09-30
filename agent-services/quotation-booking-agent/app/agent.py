"""
LangGraph workflow for Component 3 — Quotation & Booking.

Single-agent design:
  One distinct agent — the Quotation & Booking Agent — owns this workflow.
  Internally it runs three steps:

      1. validate         — deterministic business-rule checks
      2. approval_gate    — human-in-the-loop pause via interrupt()
      3. finalize         — record the decision outcome

Flow:
  START → validate → approval_gate (INTERRUPT) → finalize → END

Architectural note:
  The .NET API drives this workflow. The one-directional flow is:

      .NET  → Python  (start workflow, resume with decision)
      Python → .NET   (only for read-only lookups like get_quotation)

  The .NET side is responsible for:
    - moving the quotation to WAITING_FOR_CUSTOMER_APPROVAL
    - creating the Booking when the customer approves

  The Python side must NOT call those mutating .NET endpoints itself,
  otherwise we would create a circular call (.NET → Python → .NET → ...)
  and risk creating the Booking twice.

Safe-failure policy:
  The only outbound call the agent makes (get_quotation) is wrapped in
  try/except. If the backend is unreachable, validation proceeds with the
  locally computed total and the workflow still completes.

Persistence:
  The workflow uses an async SQLite-backed checkpointer (AsyncSqliteSaver).
  Workflow state — including the paused interrupt — survives process
  restarts. The SQLite file lives next to this module and is git-ignored.
"""

from typing import Any

import aiosqlite
from langgraph.graph import StateGraph, START, END
from langgraph.types import interrupt
from langgraph.checkpoint.sqlite.aio import AsyncSqliteSaver

from .state import QuotationState
from .tools import get_quotation


# ------------------------------------------------------------------
# Step 1 — Validate
# ------------------------------------------------------------------

async def validate_node(state: QuotationState) -> QuotationState:
    """Apply deterministic business rules before any human review."""
    errors: list[str] = []

    items = state.get("items") or []
    if not items:
        errors.append("Quotation must have at least one item.")

    total = 0.0
    for item in items:
        amount = float(item.get("amount", 0))
        quantity = int(item.get("quantity", 1))
        if amount <= 0:
            errors.append(
                f"Item '{item.get('description', '?')}' amount must be positive."
            )
        if quantity <= 0:
            errors.append(
                f"Item '{item.get('description', '?')}' quantity must be positive."
            )
        total += amount * quantity

    if state.get("quotation_id") is None:
        errors.append("quotation_id is required.")

    if errors:
        return {
            "validation_passed": False,
            "validation_errors": errors,
            "total_amount": total,
        }

    # Best-effort reconciliation of the total from the backend.
    try:
        server_q = await get_quotation(state["quotation_id"])
        total = float(server_q.get("totalAmount", total))
    except Exception:
        pass

    return {
        "validation_passed": True,
        "validation_errors": [],
        "total_amount": total,
    }


# ------------------------------------------------------------------
# Step 2 — Human approval gate (INTERRUPT)
# ------------------------------------------------------------------

async def approval_gate_node(state: QuotationState) -> QuotationState:
    """
    Pauses the workflow for a human customer decision.
    The AI cannot decide pricing; only a human approval advances the flow.

    NOTE: The .NET side already moved the quotation to
    WAITING_FOR_CUSTOMER_APPROVAL before calling this workflow.
    Do NOT call back into .NET from here — it would loop.
    """
    decision_payload: Any = interrupt(
        {
            "type": "quotation_approval",
            "quotation_id": state["quotation_id"],
            "service_request_id": state.get("service_request_id"),
            "provider_id": state.get("provider_id"),
            "total_amount": state.get("total_amount"),
            "allowed_actions": ["approve", "reject"],
            "message": "Customer must approve or reject this quotation.",
        }
    )

    if isinstance(decision_payload, dict):
        decision = decision_payload.get("decision")
        remarks = decision_payload.get("remarks")
    else:
        decision = decision_payload
        remarks = None

    return {
        "approval_requested": True,
        "customer_decision": decision,
        "customer_remarks": remarks,
    }


# ------------------------------------------------------------------
# Step 3 — Finalize (record the outcome, do NOT call .NET)
# ------------------------------------------------------------------

async def finalize_node(state: QuotationState) -> QuotationState:
    """Record the outcome. The .NET side creates the Booking."""
    decision = state.get("customer_decision")

    if decision == "approve":
        return {
            "booking_id": None,
            "booking_status": None,
            "final_message": "Customer approved the quotation.",
        }

    if decision == "reject":
        reason = (
            state.get("customer_remarks")
            or state.get("rejection_reason")
            or "Customer rejected the quotation."
        )
        return {
            "booking_id": None,
            "booking_status": None,
            "rejection_reason": reason,
            "final_message": "Customer rejected the quotation.",
        }

    return {
        "error": f"Unknown customer decision: {decision}",
        "final_message": "Workflow ended without a valid decision.",
    }


# ------------------------------------------------------------------
# Routing
# ------------------------------------------------------------------

def _route_after_validation(state: QuotationState) -> str:
    if state.get("validation_passed"):
        return "approval_gate"
    return "end"


# ------------------------------------------------------------------
# Graph assembly (async, with persistent SQLite checkpointer)
# ------------------------------------------------------------------

async def build_quotation_workflow() -> tuple[Any, aiosqlite.Connection]:
    """
    Build the LangGraph workflow with an AsyncSqliteSaver checkpointer.

    Returns (compiled_graph, sqlite_connection). The caller MUST keep the
    connection open for the lifetime of the app and close it on shutdown.
    """
    builder = StateGraph(QuotationState)

    builder.add_node("validate", validate_node)
    builder.add_node("approval_gate", approval_gate_node)
    builder.add_node("finalize", finalize_node)

    builder.add_edge(START, "validate")
    builder.add_conditional_edges(
        "validate",
        _route_after_validation,
        {"approval_gate": "approval_gate", "end": END},
    )
    builder.add_edge("approval_gate", "finalize")
    builder.add_edge("finalize", END)

    # Async SQLite checkpointer — required because the workflow uses
    # astream()/ainvoke(). The connection must stay open for the process
    # lifetime, so we return it alongside the compiled graph.
    conn = await aiosqlite.connect("quotation_workflow.sqlite")
    checkpointer = AsyncSqliteSaver(conn)

    graph = builder.compile(checkpointer=checkpointer)
    return graph, conn