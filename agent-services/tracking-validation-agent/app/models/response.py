from pydantic import BaseModel

class ValidationResponse(BaseModel):
    is_valid: bool
    status: str  # VALID, INVALID, SUSPICIOUS
    reason: str

class SentimentResponse(BaseModel):
    sentiment: str  # POSITIVE, NEUTRAL, NEGATIVE
    should_route_to_complaint: bool
    summary: str