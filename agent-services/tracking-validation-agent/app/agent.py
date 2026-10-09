from pydantic import BaseModel, Field
from app.services import get_llm  # service.py එකෙන් get_llm Import කරගනී

# --- Pydantic Schemas for Guaranteed LLM Output ---
class ValidationOutput(BaseModel):
    is_valid: bool = Field(description="True if transition is allowed, False otherwise")
    status: str = Field(description="VALID, INVALID, or SUSPICIOUS")
    reason: str = Field(description="Detailed reason for the validation decision")

class SentimentOutput(BaseModel):
    sentiment: str = Field(description="POSITIVE, NEUTRAL, or NEGATIVE")
    should_route_to_complaint: bool = Field(description="True if sentiment is NEGATIVE or mentions major issues")
    summary: str = Field(description="Short 1-sentence summary of the issue")


def run_tracking_validation(job_id: str, current_status: str, target_status: str, elapsed_minutes: float, note: str = "") -> dict:
    llm = get_llm()
    # Structured Output එකට LLM එක Bind කිරීම
    structured_llm = llm.with_structured_output(ValidationOutput)

    prompt = f"""
    You are an AI Safety & Validation Agent for AssistLK service marketplace.
    Validate this job status transition:
    - Job ID: {job_id}
    - Current Status: {current_status}
    - Target Status: {target_status}
    - Elapsed Minutes: {elapsed_minutes}
    - User Note: {note}

    Rules:
    1. Progression must follow strictly: Assigned -> OnTheWay -> Arrived -> InProgress -> Completed.
    2. Any status can transition to 'Cancelled'.
    3. If transition from InProgress to Completed takes LESS than 2.0 minutes, flag status as 'SUSPICIOUS'.
    """
    
    try:
        res = structured_llm.invoke(prompt)
        return res.model_dump()
    except Exception as e:
        return {"is_valid": False, "status": "INVALID", "reason": f"Validation Error: {str(e)}"}


def run_sentiment_analysis(feedback_text: str) -> dict:
    llm = get_llm()
    structured_llm = llm.with_structured_output(SentimentOutput)

    prompt = f"""
    Analyze the customer feedback for a completed service:
    Feedback: "{feedback_text}"

    Determine:
    1. Sentiment: POSITIVE, NEUTRAL, or NEGATIVE.
    2. should_route_to_complaint: true if sentiment is NEGATIVE or mentions major issues, otherwise false.
    3. summary: Short 1-sentence summary of the issue.
    """
    
    try:
        res = structured_llm.invoke(prompt)
        return res.model_dump()
    except Exception as e:
        return {"sentiment": "NEUTRAL", "should_route_to_complaint": False, "summary": "Error analyzing feedback."}