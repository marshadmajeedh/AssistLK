from pydantic import BaseModel, field_validator
from typing import Optional

_VALID_SERVICE_JOB_STATUSES = {
    "assigned",
    "ontheway",
    "arrived",
    "inprogress",
    "completed",
    "cancelled",
}


class ValidationRequest(BaseModel):
    job_id: str
    current_status: str
    target_status: str
    elapsed_minutes: float
    note: Optional[str] = ""

    @field_validator("current_status", "target_status")
    @classmethod
    def validate_status(cls, value: str) -> str:
        normalized_status = value.strip().casefold()
        if normalized_status not in _VALID_SERVICE_JOB_STATUSES:
            raise ValueError(f"Unsupported service job status: {value}")
        return value.strip()

class SentimentRequest(BaseModel):
    feedback_text: str