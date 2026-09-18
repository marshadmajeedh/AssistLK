"""Bounded semantic evidence summaries. Never contains image bytes or hidden reasoning."""
from collections import Counter
import re
from typing import Literal
from uuid import UUID
from pydantic import BaseModel, ConfigDict, Field, model_validator

FinalVisionStatus = Literal["not_requested", "used", "unsupported", "failed"]


def safe_evidence_text(value: str) -> bool:
    """Reject payload/hidden-reasoning echoes and explicit unsafe/identity claims."""
    lower = value.lower()
    forbidden = ("data:image", "database64", "inline_data", "inlinedata", "chain-of-thought",
                 "chain of thought", "system instruction", "ignore all instructions",
                 "ignore previous instructions", "readyformatching", "no electrical danger",
                 "wiring is safe", "wire is safe", "guaranteed", "definitely",
                 "ethnicity", "ethnic", "race is", "identified as", "person named",
                 "face recognition", "pregnant", "mental health", "diabetes")
    return not any(term in lower for term in forbidden) and not re.search(r"[A-Za-z0-9+/=_-]{80,}", value)


class VisualObservation(BaseModel):
    model_config = ConfigDict(populate_by_name=True, extra="forbid", hide_input_in_errors=True)
    attachment_id: UUID = Field(alias="attachmentId")
    observation: str = Field(min_length=1, max_length=240)


class VisualResultFields(BaseModel):
    model_config = ConfigDict(populate_by_name=True, hide_input_in_errors=True)
    vision_status: FinalVisionStatus = Field(default="not_requested", alias="visionStatus")
    attachment_ids_used: list[UUID] = Field(default_factory=list, alias="attachmentIdsUsed", max_length=3)
    visual_observations: list[VisualObservation] = Field(default_factory=list, alias="visualObservations", max_length=5)
    visual_limitations: list[str] = Field(default_factory=list, alias="visualLimitations", max_length=3)

    @model_validator(mode="after")
    def validate_visual_result(self):
        if len(set(self.attachment_ids_used)) != len(self.attachment_ids_used):
            raise ValueError("Duplicate used attachment identity.")
        if any(count > 2 for count in Counter(o.attachment_id for o in self.visual_observations).values()):
            raise ValueError("At most two observations per attachment.")
        if any(o.attachment_id not in self.attachment_ids_used for o in self.visual_observations):
            raise ValueError("Observation must reference an inspected attachment.")
        if any(not text.strip() or len(text) > 240 for text in self.visual_limitations):
            raise ValueError("Visual limitations must be concise nonempty strings.")
        if self.vision_status != "used" and (self.attachment_ids_used or self.visual_observations):
            raise ValueError("Unprocessed evidence cannot have observations or inspected identities.")
        if self.vision_status == "used" and not self.attachment_ids_used:
            raise ValueError("Used evidence requires attachment identities.")
        return self
