"""State schema definition for Problem Understanding LangGraph state machine."""
from typing import Any, TypedDict
from app.schemas.visual_evidence import VisualEvidence, VisionStatus


class ProblemUnderstandingState(TypedDict, total=False):
    """Execution state passed through the LangGraph reasoning pipeline."""

    # Raw Inputs
    request_id: str
    service_request_id: str
    description: str
    location_text: str | None
    latitude: float | None
    longitude: float | None
    category_hint: str | None
    clarification_history: list[dict[str, Any]]

    # One transient collection, never copied into prompts/output/memory.
    visual_evidence: list[VisualEvidence]
    vision_status: VisionStatus
    visual_result: dict[str, Any]
    text_image_conflict: bool
    visual_ambiguity_resolved: bool

    # Validation
    is_empty_input: bool

    # Deterministic Tool 1: Location Extraction
    normalized_location: str | None
    has_coordinates: bool
    extracted_lat: float | None
    extracted_lon: float | None

    # Deterministic Tool 2: Problem Classification
    deterministic_category: str
    deterministic_confidence: float
    deterministic_terms: list[str]

    # Deterministic Tool 3: Service Knowledge
    service_family: str
    safe_terminology: str
    inspection_advised: bool
    possible_missing_info: list[str]

    # Reasoning / LLM
    prompt: str
    llm_raw_category: str | None
    llm_raw_summary: str | None
    llm_raw_urgency: str | None
    llm_raw_confidence: float | None
    llm_raw_needs_more: bool | None
    llm_raw_questions: list[str]
    additional_info: dict[str, str]
    degraded: bool
    llm_error: str | None

    # Ambiguity Evaluation
    is_ambiguous: bool

    # Guardrails / Final Deliverables
    final_category: str
    final_summary: str
    final_urgency: str
    final_confidence: float
    final_needs_more: bool
    final_questions: list[str]

    # Tool Audit Summaries
    tool_executions: list[dict[str, Any]]

    # Final Output Object
    output: dict[str, Any]
    error_message: str | None
