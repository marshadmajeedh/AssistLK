"""
Workflow state for the Quotation & Booking Agent (Component 3).

Single-agent workflow:
  validate → approval_gate (INTERRUPT) → finalize

State is persisted by LangGraph's checkpointer across the human-in-the-loop
pause at WAITING_FOR_CUSTOMER_APPROVAL.

Note: `service_request_id` and `provider_id` are UUIDs serialized as strings,
because Python's JSON handling doesn't natively carry the .NET Guid type.
"""

from typing import Literal, Optional, TypedDict


class QuotationItemDict(TypedDict, total=False):
    description: str
    amount: float
    quantity: int


class QuotationState(TypedDict, total=False):
    # ---- Inputs ----
    quotation_id: int
    service_request_id: Optional[str]     # UUID as string
    provider_id: Optional[str]            # UUID as string
    items: list[QuotationItemDict]
    notes: Optional[str]

    # ---- Step 1: validation ----
    validation_passed: bool
    validation_errors: list[str]
    total_amount: float

    # ---- Step 2: human approval ----
    approval_requested: bool
    customer_decision: Optional[Literal["approve", "reject"]]
    customer_remarks: Optional[str]
    rejection_reason: Optional[str]

    # ---- Step 3: finalize ----
    booking_id: Optional[int]
    booking_status: Optional[str]
    final_message: Optional[str]

    # ---- Cross-cutting ----
    error: Optional[str]