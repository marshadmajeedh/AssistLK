from pydantic import BaseModel
from typing import Optional

class ValidationRequest(BaseModel):
    job_id: str
    current_status: str
    target_status: str
    elapsed_minutes: float
    note: Optional[str] = ""

class SentimentRequest(BaseModel):
    feedback_text: str