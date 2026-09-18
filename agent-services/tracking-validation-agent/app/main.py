import uvicorn
from fastapi import FastAPI
from fastapi.responses import RedirectResponse
from dotenv import load_dotenv
from fastapi.middleware.cors import CORSMiddleware

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

@app.get("/", include_in_schema=False)
async def root():
    return RedirectResponse(url="/docs")


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
    uvicorn.run(app, host="0.0.0.0", port=8000)