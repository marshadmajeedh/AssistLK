"""Conservative fusion rules layered over existing text-only guardrails."""
import re
from app.safety.guardrails import DANGEROUS_TERMS
from app.schemas.visual_result import safe_evidence_text


def sanitize_visual_result(result: dict) -> dict:
    observations = [o for o in result.get("visualObservations", [])
                    if safe_evidence_text(o["observation"])
                    and not any(term in o["observation"].lower() for term in DANGEROUS_TERMS)]
    limitations = [s for s in result.get("visualLimitations", []) if safe_evidence_text(s)
                   and not any(term in s.lower() for term in DANGEROUS_TERMS)]
    return {**result, "visualObservations": observations, "visualLimitations": limitations}


def text_urgency_floor(text: str) -> str | None:
    # Only explicit text hazards; images can never establish their absence.
    for urgency, terms in [("Critical", ("fire", "explosion", "severe collision")),
                           ("High", ("sparking", "sparks", "burning smell", "smoke", "flooding", "burst"))]:
        for term in terms:
            for match in re.finditer(r"\b" + re.escape(term) + r"\b", text.lower()):
                preceding = text[max(0, match.start() - 28):match.start()].lower().split()
                if not any(word in {"no", "not", "without", "never"} for word in preceding[-3:]):
                    return urgency
    return None
