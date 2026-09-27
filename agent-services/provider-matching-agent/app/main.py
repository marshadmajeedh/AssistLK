from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field, AliasChoices, ConfigDict
from typing import Optional, List, Dict, Any
import uuid

from .agent import graph
from langgraph.types import Command

app = FastAPI(title="Provider Matching Agent API")

class ProviderCandidateDTO(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    provider_id: str
    name: str
    rating: float
    latitude: float
    longitude: float
    operating_radius_km: float = Field(
        default=5.0,
        validation_alias=AliasChoices("operating_radius_km", "OperatingRadiusKm", "operatingRadiusKm")
    )
    verified: bool
    skills: List[str]

class MatchRequest(BaseModel):
    objective: str
    urgency: Optional[int] = 2
    customer_latitude: Optional[float] = 6.9270
    customer_longitude: Optional[float] = 79.8610
    eligible_providers: Optional[List[ProviderCandidateDTO]] = None

class ResumeRequest(BaseModel):
    thread_id: str
    action: str  # "Approve" or "Reject"
    admin_id: str

@app.post("/match/start")
async def start_match(req: MatchRequest):
    thread_id = str(uuid.uuid4())
    config = {"configurable": {"thread_id": thread_id}}
    
    # Pack dynamic DB providers and coordinates into LangGraph state
    initial_state = {
        "WorkflowId": thread_id,
        "Objective": req.objective,
        "Plan": ["Score Candidates", "Await Admin Approval", "Finalize"],
        "CurrentStep": "Initialized",
        "CompletedSteps": [],
        "ToolResults": [],
        "ValidationResults": [],
        "Errors": [],
        "Retries": 0,
        "ApprovalStatus": "Running",
        "FinalOutcome": None,
        "StartedAt": "",
        "UpdatedAt": "",
        "CompletedAt": None,
        "customer_latitude": req.customer_latitude,
        "customer_longitude": req.customer_longitude,
        "urgency_level": req.urgency,
        "eligible_providers": [p.model_dump() for p in req.eligible_providers] if req.eligible_providers else None
    }
    
    try:
        # Run the graph until the human approval gate interrupt()
        result = graph.invoke(initial_state, config=config)
        
        token_usage = result.get("usage_metadata", {"total_tokens": 0})
        final_outcome = result.get("FinalOutcome") or {}
        recommended = final_outcome.get("recommended_candidate") or final_outcome.get("recommended_provider")
        
        return {
            "thread_id": thread_id,
            "status": result.get("ApprovalStatus"),
            "recommended_provider": recommended,
            "tokens_consumed": token_usage,
            "final_outcome": final_outcome,
            "message": final_outcome.get("message")
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/match/resume")
async def resume_match(req: ResumeRequest):
    import traceback
    config = {"configurable": {"thread_id": req.thread_id}}
    
    try:
        current_state = graph.get_state(config)
        
        status = None
        final_outcome = None

        if current_state and current_state.next:
            result = graph.invoke(
                Command(resume={"action": req.action, "admin_id": req.admin_id}), 
                config=config
            )
            
            if isinstance(result, dict):
                status = result.get("ApprovalStatus")
                final_outcome = result.get("FinalOutcome")
            elif hasattr(result, "get"):
                status = result.get("ApprovalStatus")
                final_outcome = result.get("FinalOutcome")
            elif isinstance(result, str):
                status = result
        else:
            # Checkpoint was lost across server restarts or already unpaused.
            # Gracefully acknowledge the Admin's HITL decision to allow the downstream workflow to complete.
            action_norm = (req.action or "").strip().lower()
            status = "Approved" if action_norm == "approve" else "Rejected"
            final_outcome = {
                "admin_id": req.admin_id,
                "action": req.action,
                "note": "Decision acknowledged gracefully."
            }
            
        return {
            "thread_id": req.thread_id,
            "status": status or req.action,
            "final_outcome": final_outcome
        }
    except HTTPException:
        raise
    except Exception as e:
        traceback.print_exc()
        raise HTTPException(status_code=500, detail=str(e))