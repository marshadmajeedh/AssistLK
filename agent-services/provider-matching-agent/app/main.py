from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from typing import Optional, Dict, Any
import uuid

# Import the compiled graph from your agent.py
from .agent import graph
from langgraph.types import Command

app = FastAPI(title="Provider Matching Agent API")

class MatchRequest(BaseModel):
    objective: str

class ResumeRequest(BaseModel):
    thread_id: str
    action: str  # "Approve" or "Reject"
    admin_id: str

@app.post("/match/start")
async def start_match(req: MatchRequest):
    thread_id = str(uuid.uuid4())
    config = {"configurable": {"thread_id": thread_id}}
    
    initial_state = {
        "WorkflowId": thread_id,
        "Objective": req.objective,
        "Plan": ["Score Candidates", "Await Admin Approval", "Finalize"],
        "CompletedSteps": [],
        "Errors": [],
        "Retries": 0,
        "ApprovalStatus": "Running",
        "FinalOutcome": None,
        "StartedAt": "",
        "UpdatedAt": "",
        "CompletedAt": None
    }
    
    try:
        # Run the graph until it hits the interrupt()
        result = graph.invoke(initial_state, config=config)
        
        # Safely extract token usage per Lab 5 rules
        token_usage = result.get("usage_metadata", {"total_tokens": 0})
        
        # Safely extract FinalOutcome without throwing NoneType errors
        final_outcome = result.get("FinalOutcome") or {}
        recommended = final_outcome.get("recommended_provider") or final_outcome.get("recommended_candidate")
        
        return {
            "thread_id": thread_id,
            "status": result.get("ApprovalStatus"),
            "recommended_provider": recommended,
            "tokens_consumed": token_usage
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/match/resume")
async def resume_match(req: ResumeRequest):
    config = {"configurable": {"thread_id": req.thread_id}}
    
    try:
        # Verify state exists and is paused
        current_state = graph.get_state(config)
        if not current_state or not current_state.next:
            raise HTTPException(status_code=400, detail="No pending approval found for this thread.")
            
        # Resume the graph using the Lab 6 Command pattern
        result = graph.invoke(
            Command(resume={"action": req.action, "admin_id": req.admin_id}), 
            config=config
        )
        
        return {
            "thread_id": req.thread_id,
            "status": result.get("ApprovalStatus"),
            "final_outcome": result.get("FinalOutcome")
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))