import uvicorn
from datetime import datetime
import re

from fastapi import FastAPI
from fastapi.responses import RedirectResponse
from dotenv import load_dotenv
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, ConfigDict, Field, model_validator

from app.models.request import ValidationRequest, SentimentRequest
from app.models.response import ValidationResponse, SentimentResponse
from app.agent import run_tracking_validation, run_sentiment_analysis

load_dotenv()

app = FastAPI(
    title="AssistLK Agent 4 - Validation & Safety Service",
    description="Handles workflow state transition validation, safety checks, and sentiment analysis."
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
    allow_credentials=True,
)


class CompletionValidationRequest(BaseModel):
    model_config = ConfigDict(extra="forbid")

    job_started_at: datetime
    job_completed_at: datetime

    @model_validator(mode="after")
    def validate_timestamps(self):
        if (self.job_started_at.tzinfo is None) != (self.job_completed_at.tzinfo is None):
            raise ValueError("job_started_at and job_completed_at must use the same timezone style")
        if self.job_completed_at <= self.job_started_at:
            raise ValueError("job_completed_at must be later than job_started_at")
        return self

    @property
    def duration_seconds(self) -> float:
        return (self.job_completed_at - self.job_started_at).total_seconds()


class CompletionValidationResponse(BaseModel):
    is_suspicious: bool
    duration_seconds: float
    reason: str


class SentimentAnalysisRequest(BaseModel):
    model_config = ConfigDict(extra="forbid")

    rating: int = Field(ge=1, le=5)
    review_text: str = Field(min_length=1, max_length=5000)


class SentimentAnalysisResponse(BaseModel):
    should_escalate: bool
    sentiment: str
    matched_terms: list[str] = Field(default_factory=list)


_NEGATIVE_SENTIMENT_TERMS = (
    "abuse",
    "abusive",
    "angry",
    "awful",
    "bad",
    "complaint",
    "damaged",
    "fraud",
    "frustrated",
    "hate",
    "horrible",
    "incompetent",
    "insult",
    "scam",
    "rude",
    "terrible",
    "unacceptable",
    "useless",
)


def _find_negative_terms(review_text: str) -> list[str]:
    normalized_text = review_text.casefold()
    return [
        term
        for term in _NEGATIVE_SENTIMENT_TERMS
        if re.search(rf"\b{re.escape(term)}\b", normalized_text)
    ]

@app.get("/", include_in_schema=False)
async def root():
    return RedirectResponse(url="/docs")


@app.post("/validate-completion", response_model=CompletionValidationResponse)
async def validate_completion(req: CompletionValidationRequest):
    duration_seconds = req.duration_seconds
    is_suspicious = duration_seconds < 120
    reason = (
        "Rapid Completion Fraud: job completed in less than 2 minutes."
        if is_suspicious
        else "Completion duration is within the expected threshold."
    )
    return CompletionValidationResponse(
        is_suspicious=is_suspicious,
        duration_seconds=duration_seconds,
        reason=reason,
    )


@app.post("/analyze-sentiment", response_model=SentimentAnalysisResponse)
async def analyze_review_sentiment(req: SentimentAnalysisRequest):
    matched_terms = _find_negative_terms(req.review_text)
    should_escalate = req.rating <= 2 or bool(matched_terms)
    sentiment = "NEGATIVE" if should_escalate else "POSITIVE"
    return SentimentAnalysisResponse(
        should_escalate=should_escalate,
        sentiment=sentiment,
        matched_terms=matched_terms,
    )


# --- Agent 4: State Transition Validation ---
@app.post("/agent/validate", response_model=ValidationResponse)
async def validate_transition(req: ValidationRequest):
    result = run_tracking_validation(
        job_id=req.job_id,
        current_status=req.current_status,
        target_status=req.target_status,
        elapsed_minutes=req.elapsed_minutes,
        note=req.note
    )
    return ValidationResponse(**result)


# --- Agent 4: Safety & Sentiment Analysis ---
@app.post("/agent/analyze-sentiment", response_model=SentimentResponse)
async def analyze_sentiment(req: SentimentRequest):
    result = run_sentiment_analysis(req.feedback_text)
    return SentimentResponse(**result)


if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8003)