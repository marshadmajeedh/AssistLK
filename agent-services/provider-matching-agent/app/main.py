from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from typing import Optional
from datetime import datetime, timezone
import uuid
from langgraph.types import Command
from .agent import graph
from .state import MatchingState

app = FastAPI()

class MatchRequest(BaseModel):
    objective: str

class ResumeRequest(BaseModel):
    thread_id: str
    action: str  # "Approve" or "Reject"
    admin_id: str

@app.post("/match/start")
def start_match(req: MatchRequest):
    thread_id = str(uuid.uuid4())
    config = {"configurable": {"thread_id": thread_id}}
    
    initial_state: MatchingState = {
        "WorkflowId": thread_id,
        "Objective": req.objective,
        "Plan": [],
        "CurrentStep": "Initialized",
        "CompletedSteps": [],
        "ToolResults": [],
        "ValidationResults": [],
        "Errors": [],
        "Retries": 0,
        "ApprovalStatus": "Running",
        "FinalOutcome": None,
        "StartedAt": datetime.now(timezone.utc).isoformat(),
        "UpdatedAt": datetime.now(timezone.utc).isoformat(),
        "CompletedAt": None
    }
    
    # Run until interrupt
    result = graph.invoke(initial_state, config=config)
    
    return {
        "thread_id": thread_id,
        "status": result.get("ApprovalStatus"),
        "recommended_provider": result.get("FinalOutcome", {}).get("recommended_provider")
    }

@app.post("/match/resume")
def resume_match(req: ResumeRequest):
    config = {"configurable": {"thread_id": req.thread_id}}
    state = graph.get_state(config)
    
    if not state or not state.next:
        raise HTTPException(status_code=400, detail="No pending human approval found for this thread.")
        
    approval_status = "Approved" if req.action == "Approve" else "Rejected"
    
    # Resume with Command
    result = graph.invoke(Command(resume={"ApprovalStatus": approval_status}), config=config)
    
    return {
        "thread_id": req.thread_id,
        "status": result.get("ApprovalStatus"),
        "final_outcome": result.get("FinalOutcome")
    }
