"""ProblemClassificationTool - Deterministic keyword problem classifier."""
import re
from dataclasses import dataclass
from typing import ClassVar


@dataclass(frozen=True)
class ProblemClassificationData:
    """Output data returned by ProblemClassificationTool."""

    category: str
    confidence: float
    supporting_terms: list[str]


class CategoryDefinition:
    """Canonical service category and its deterministic keywords."""

    def __init__(self, category: str, keywords: list[str]) -> None:
        self.category = category
        self.keywords = keywords


CATEGORIES: list[CategoryDefinition] = [
    CategoryDefinition(
        "Plumbing",
        [
            "pipe", "leak", "leaking", "plumbing", "tap", "drip", "dripping",
            "water", "drain", "flush", "toilet", "sink", "faucet",
            "burst", "flood", "flooding", "blocked", "clog",
        ],
    ),
    CategoryDefinition(
        "Electrical",
        [
            "electric", "electrical", "socket", "plug", "wiring",
            "wire", "switch", "circuit", "breaker", "fuse", "power",
            "light", "lights", "outlet", "sparks", "spark", "shock",
            "tripped", "trip", "burning", "burn", "smoke",
        ],
    ),
    CategoryDefinition(
        "Vehicle Repair",
        [
            "car", "vehicle", "engine", "battery", "tyre", "tire",
            "wheel", "brake", "brakes", "transmission", "ignition",
            "starter", "fuel", "exhaust", "overheating", "accident",
            "truck", "van", "motorcycle", "bike", "breakdown",
        ],
    ),
    CategoryDefinition(
        "Appliance Repair",
        [
            "fridge", "refrigerator", "washing", "washer", "dryer",
            "dishwasher", "oven", "microwave", "freezer", "air conditioner",
            "ac", "heater", "fan", "appliance", "cooling", "heating",
        ],
    ),
]


def _word_matches_keyword(words: set[str], keyword: str) -> bool:
    if keyword in words:
        return True
    for word in words:
        if word.startswith(keyword):
            return True
        if keyword.startswith(word) and len(word) >= 4:
            return True
    return False


def classify_problem_tool(description: str) -> ProblemClassificationData:
    """
    Classifies a natural-language description into canonical categories using deterministic keywords.

    No database, no external dependencies, 100% deterministic.
    """
    if not description or not description.strip():
        return ProblemClassificationData(
            category="Unclassified",
            confidence=0.0,
            supporting_terms=[],
        )

    lower = description.lower()
    # Split on whitespace and punctuation matching C# implementation
    tokens = set(re.split(r"[ ,.\!?;\:\t\r\n]+", lower))
    tokens.discard("")

    best_category = "Unclassified"
    best_score = 0
    best_terms: list[str] = []

    for cat_def in CATEGORIES:
        matched_terms: list[str] = []
        for kw in cat_def.keywords:
            if " " in kw:
                if kw in lower:
                    matched_terms.append(kw)
            else:
                if _word_matches_keyword(tokens, kw):
                    matched_terms.append(kw)

        if len(matched_terms) > best_score:
            best_score = len(matched_terms)
            best_category = cat_def.category
            best_terms = matched_terms

    if best_score == 0:
        confidence = 0.2
        best_category = "Unclassified"
    else:
        confidence = min(0.5 + (best_score * 0.1), 0.95)

    return ProblemClassificationData(
        category=best_category,
        confidence=round(confidence, 2),
        supporting_terms=best_terms,
    )
