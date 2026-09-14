"""Deterministic preparation only: no decoding, model calls or visual inference."""
from app.graphs.state import ProblemUnderstandingState
from app.providers.base import BaseLLMProvider
from app.schemas.visual_evidence import VisualEvidence, validate_evidence_collection


def prepare_visual_evidence(state: ProblemUnderstandingState, provider: BaseLLMProvider) -> dict:
    items = state.get("visual_evidence", [])
    if not items:
        return {"vision_status": "not_requested"}
    try:
        if not all(isinstance(item, VisualEvidence) for item in items):
            raise ValueError("Evidence must pass request validation first.")
        validate_evidence_collection(items)
    except (ValueError, TypeError, AttributeError):
        # Only reachable for invalid direct graph callers; HTTP validation rejects such input.
        return {"vision_status": "failed"}
    return {"vision_status": "available" if provider.supports_images else "unsupported"}
