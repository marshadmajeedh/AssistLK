"""
Workflow state for the Quotation & Booking Agent (Component 3).

Single-agent workflow:
  validate → analyze_quotation (LLM) → approval_gate (INTERRUPT) → finalize

State is persisted by LangGraph's checkpointer across the human-in-the-loop
pause at WAITING_FOR_CUSTOMER_APPROVAL.

Note: `service_request_id` and `provider_id` are UUIDs serialized as strings.
"""

from typing import Literal, Optional, TypedDict


class QuotationItemDict(TypedDict, total=False):
    description: str
    amount: float
    quantity: int


class QuotationRiskAssessment(TypedDict, total=False):
    """LLM-produced risk assessment, surfaced to the customer before approval."""
    risk_level: str                     # "low" | "medium" | "high"
    confidence: float                   # 0.0 .. 1.0
    rationale: str                      # natural-language explanation
    suggested_concerns: list[str]       # bullet items for the customer
    recommendation: str                 # "approve" | "request_clarification" | "reject"
    model: str                          # model name used
    prompt_tokens: int
    completion_tokens: int
    total_tokens: int


class QuotationState(TypedDict, total=False):
    # ---- Inputs ----
    quotation_id: int
    service_request_id: Optional[str]
    provider_id: Optional[str]
    items: list[QuotationItemDict]
    notes: Optional[str]
    service_category: Optional[str]     # helps the LLM reason

    # ---- Step 1: validation ----
    validation_passed: bool
    validation_errors: list[str]
    total_amount: float

    # ---- Step 2: LLM risk assessment ----
    risk_assessment: Optional[QuotationRiskAssessment]
    llm_error: Optional[str]            # safe-failure marker if LLM call fails

    # ---- Step 3: human approval ----
    approval_requested: bool
    customer_decision: Optional[Literal["approve", "reject"]]
    customer_remarks: Optional[str]
    rejection_reason: Optional[str]

    # ---- Step 4: finalize ----
    booking_id: Optional[int]
    booking_status: Optional[str]
    final_message: Optional[str]

    # ---- Cross-cutting ----
    error: Optional[str]