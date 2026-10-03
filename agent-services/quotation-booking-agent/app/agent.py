"""
LangGraph workflow for Component 3 — Quotation & Booking.

Four-step workflow (single-agent service):
  1. validate            — deterministic business-rule checks
  2. analyze_quotation   — LLM risk assessment (Gemini or OpenAI)
  3. approval_gate       — human-in-the-loop pause via interrupt()
  4. finalize            — record the decision outcome

Flow:
  START → validate → analyze_quotation → approval_gate (INTERRUPT) → finalize → END

The LLM node degrades gracefully: if no LLM credentials are configured or
the call fails, the workflow records `llm_error` and continues — never crashes.

Persistence: AsyncSqliteSaver checkpointer.
"""

import json
from typing import Any

import aiosqlite
from langgraph.graph import StateGraph, START, END
from langgraph.types import interrupt
from langgraph.checkpoint.sqlite.aio import AsyncSqliteSaver

from .state import QuotationState, QuotationRiskAssessment
from .tools import get_quotation
from .llm_provider import get_chat_model
from .prompts.quotation_analysis import SYSTEM_PROMPT, build_user_message


# ------------------------------------------------------------------
# Step 1 — Validate (deterministic)
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

    # Best-effort reconciliation with the backend
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
# Step 2 — LLM risk assessment
# ------------------------------------------------------------------

def _extract_text(content: Any) -> str:
    """
    Normalize the LLM response content to a plain string.

    - OpenAI returns a string.
    - Gemini returns a list of content blocks like
      [{'type': 'text', 'text': 'PONG', 'extras': {...}}].
    """
    if isinstance(content, list):
        return "".join(
            part.get("text", "") if isinstance(part, dict) else str(part)
            for part in content
        )
    return str(content or "")


def _extract_usage(response: Any) -> dict:
    """
    Extract token usage from either OpenAI or Gemini response shapes.

    - OpenAI: response.response_metadata['token_usage'] with keys
              prompt_tokens / completion_tokens / total_tokens
    - Gemini: response.usage_metadata with keys
              input_tokens / output_tokens / total_tokens
    """
    # Try OpenAI-style first
    rm = getattr(response, "response_metadata", None)
    if isinstance(rm, dict):
        usage = rm.get("token_usage")
        if isinstance(usage, dict) and usage:
            return usage

    # Try Gemini-style
    um = getattr(response, "usage_metadata", None)
    if isinstance(um, dict) and um:
        return um

    return {}


def _model_name(model: Any) -> str:
    """Return a best-effort model name for the audit trail."""
    for attr in ("model", "model_name", "model_id"):
        value = getattr(model, attr, None)
        if isinstance(value, str) and value:
            return value
    return "unknown"


async def analyze_quotation_node(state: QuotationState) -> QuotationState:
    """
    Call the LLM to produce a structured risk assessment.

    Safe-failure policy:
      Any exception is caught and recorded in `llm_error`. The workflow
      always proceeds to the approval gate.
    """
    # Skip if validation already failed.
    if not state.get("validation_passed"):
        return {}

    model = get_chat_model()
    if model is None:
        return {
            "llm_error": "No LLM credentials configured — skipping LLM assessment.",
            "risk_assessment": None,
        }

    try:
        user_message = build_user_message(
            quotation_id=state.get("quotation_id") or 0,
            service_category=state.get("service_category"),
            total_amount=float(state.get("total_amount") or 0.0),
            items=state.get("items") or [],
            notes=state.get("notes"),
        )

        response = await model.ainvoke(
            [
                ("system", SYSTEM_PROMPT),
                ("human", user_message),
            ]
        )

        # Extract text content
        raw = _extract_text(response.content).strip()

        # Strip markdown fences if the model added them
        if raw.startswith("```"):
            raw = raw.strip("`")
            if raw.lower().startswith("json"):
                raw = raw[4:].lstrip()
            raw = raw.strip()

        parsed = json.loads(raw)

        # Extract token usage (handles both provider shapes)
        usage = _extract_usage(response)

        assessment: QuotationRiskAssessment = {
            "risk_level": str(parsed.get("risk_level", "medium")).lower(),
            "confidence": float(parsed.get("confidence", 0.5)),
            "rationale": str(parsed.get("rationale", "")).strip(),
            "suggested_concerns": [
                str(c) for c in (parsed.get("suggested_concerns") or [])
            ],
            "recommendation": str(
                parsed.get("recommendation", "request_clarification")
            ),
            "model": _model_name(model),
            "prompt_tokens": int(
                usage.get("prompt_tokens")
                or usage.get("input_tokens")
                or 0
            ),
            "completion_tokens": int(
                usage.get("completion_tokens")
                or usage.get("output_tokens")
                or 0
            ),
            "total_tokens": int(usage.get("total_tokens") or 0),
        }

        return {"risk_assessment": assessment, "llm_error": None}

    except json.JSONDecodeError as e:
        return {
            "risk_assessment": None,
            "llm_error": f"LLM returned non-JSON output: {e}",
        }
    except Exception as e:
        return {
            "risk_assessment": None,
            "llm_error": f"LLM call failed: {type(e).__name__}: {e}",
        }


# ------------------------------------------------------------------
# Step 3 — Human approval gate
# ------------------------------------------------------------------

async def approval_gate_node(state: QuotationState) -> QuotationState:
    """
    Pause for the customer's decision.

    The AI cannot decide pricing. The customer sees the LLM risk
    assessment (in the interrupt payload) before deciding.
    """
    decision_payload: Any = interrupt(
        {
            "type": "quotation_approval",
            "quotation_id": state["quotation_id"],
            "service_request_id": state.get("service_request_id"),
            "provider_id": state.get("provider_id"),
            "total_amount": state.get("total_amount"),
            "allowed_actions": ["approve", "reject"],
            "risk_assessment": state.get("risk_assessment"),
            "llm_error": state.get("llm_error"),
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
# Step 4 — Finalize
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
        return "analyze_quotation"
    return "end"


# ------------------------------------------------------------------
# Graph assembly
# ------------------------------------------------------------------

async def build_quotation_workflow() -> tuple[Any, aiosqlite.Connection]:
    """
    Build the LangGraph workflow with an AsyncSqliteSaver checkpointer.

    Returns (compiled_graph, sqlite_connection). The caller must keep the
    connection open and close it on shutdown.
    """
    builder = StateGraph(QuotationState)

    builder.add_node("validate", validate_node)
    builder.add_node("analyze_quotation", analyze_quotation_node)
    builder.add_node("approval_gate", approval_gate_node)
    builder.add_node("finalize", finalize_node)

    builder.add_edge(START, "validate")
    builder.add_conditional_edges(
        "validate",
        _route_after_validation,
        {"analyze_quotation": "analyze_quotation", "end": END},
    )
    builder.add_edge("analyze_quotation", "approval_gate")
    builder.add_edge("approval_gate", "finalize")
    builder.add_edge("finalize", END)

    conn = await aiosqlite.connect("quotation_workflow.sqlite")
    checkpointer = AsyncSqliteSaver(conn)

    graph = builder.compile(checkpointer=checkpointer)
    return graph, conn