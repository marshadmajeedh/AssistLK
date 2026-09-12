"""Safety guardrails and output sanitization for Problem Understanding Agent."""
import re
from typing import Literal

CanonicalCategory = Literal[
    "Plumbing",
    "Electrical",
    "Vehicle Repair",
    "Appliance Repair",
    "Unclassified",
]

ALLOWED_CATEGORIES: set[str] = {
    "Plumbing",
    "Electrical",
    "Vehicle Repair",
    "Appliance Repair",
    "Unclassified",
}

DANGEROUS_TERMS: list[str] = [
    "open the wire",
    "open the wiring",
    "touch the wire",
    "handle the wire",
    "strip the wire",
    "strip wire",
    "replace the wire yourself",
    "fix the wire yourself",
    "inspect the wire yourself",
    "do it yourself",
    "breaker yourself",
    "bypass",
    "disassemble the unit",
    "open the panel yourself",
    "replace electrical wiring",
    "replace electrical wiring themselves",
    "replace wiring",
    "wiring themselves",
    "wire themselves",
    "replace wiring yourself",
    "replace electrical wire",
]

GUARANTEED_TERMS: list[str] = [
    "definitely",
    "guaranteed",
    "your wiring is broken",
    "your battery is dead",
    "100% certain",
]

UNCERTAINTY_WORDS: list[str] = [
    "possible",
    "may",
    "could",
    "potential",
]


def sanitize_category(raw_category: str | None) -> CanonicalCategory:
    """Normalizes category against canonical allowed service categories."""
    if not raw_category:
        return "Unclassified"
    cleaned = raw_category.strip()
    for cat in ALLOWED_CATEGORIES:
        if cat.lower() == cleaned.lower():
            return cat  # type: ignore[return-value]
    return "Unclassified"


def sanitize_urgency(raw_urgency: str | None, category: str) -> str:
    """Validates urgency level and enforces Unknown if category is Unclassified."""
    if category.strip().lower() == "unclassified":
        return "Unknown"

    if not raw_urgency:
        return "Medium"

    cleaned = raw_urgency.strip().lower()
    mapping = {
        "critical": "Critical",
        "high": "High",
        "medium": "Medium",
        "low": "Low",
        "unknown": "Unknown",
    }
    return mapping.get(cleaned, "Medium")


def apply_safety_guardrails(
    category: str,
    problem_summary: str,
    urgency: str,
    confidence: float,
    needs_more_information: bool,
    follow_up_questions: list[str],
) -> tuple[CanonicalCategory, str, str, float, bool, list[str]]:
    """
    Enforces strict AssistLK safety policies on agent deliverables:
    1. Eliminates dangerous DIY advice and equipment tampering instructions.
    2. Guarantees uncertainty language (never asserts a guaranteed diagnosis).
    3. Replaces dogmatic diagnosis claims with possibility language.
    4. Enforces 'Unknown' urgency and capped confidence for Unclassified problems.
    5. Bounds follow-up questions to 1-3 safe questions.
    """
    safe_category = sanitize_category(category)
    safe_urgency = sanitize_urgency(urgency, safe_category)

    summary = (problem_summary or "").strip()
    lower_summary = summary.lower()

    # Check for dangerous DIY terms in summary
    has_dangerous = any(term in lower_summary for term in DANGEROUS_TERMS)
    if has_dangerous:
        summary = "Possible safety issue. Professional inspection is recommended to ensure safety."
    else:
        # Check and replace guaranteed diagnosis assertions
        for g in GUARANTEED_TERMS:
            if g in lower_summary:
                # Case-insensitive replacement
                pattern = re.compile(re.escape(g), re.IGNORECASE)
                summary = pattern.sub("possible issue", summary)

    # Ensure uncertainty language prefix
    has_uncertainty = any(u in summary.lower() for u in UNCERTAINTY_WORDS)
    if not has_uncertainty and summary:
        summary = f"Possible {summary[0].lower()}{summary[1:]}"
    elif not summary:
        summary = f"Possible {safe_category.lower()} issue."

    # Sanitize follow-up questions: remove any dangerous instructions
    sanitized_questions: list[str] = []
    for q in follow_up_questions:
        clean_q = q.strip()
        lower_q = clean_q.lower()
        if not any(term in lower_q for term in DANGEROUS_TERMS):
            sanitized_questions.append(clean_q)

    safe_needs_more = needs_more_information
    safe_confidence = round(max(0.0, min(1.0, float(confidence))), 2)

    # Invariants for Unclassified
    if safe_category == "Unclassified":
        safe_urgency = "Unknown"
        safe_needs_more = True
        safe_confidence = min(safe_confidence, 0.4)

    # If more information is needed, ensure between 1 and 3 concise questions
    if safe_needs_more and not sanitized_questions:
        sanitized_questions = [
            "Could you describe the problem you are experiencing in more detail?",
            "What specific symptoms or equipment are involved?",
        ]

    final_questions = sanitized_questions[:3]

    return (
        safe_category,
        summary,
        safe_urgency,
        safe_confidence,
        safe_needs_more,
        final_questions,
    )
