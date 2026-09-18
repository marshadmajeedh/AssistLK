import os
import math
import logging
from datetime import datetime, timezone
from typing import Dict, Any, List, Optional, TypedDict
from dotenv import load_dotenv

from langgraph.graph import StateGraph, START, END
from langgraph.checkpoint.memory import MemorySaver
from langgraph.types import interrupt

from langchain_google_genai import ChatGoogleGenerativeAI
from langchain_core.messages import SystemMessage, HumanMessage

from .tools import SearchEligibleProviders, CalculateDistance

load_dotenv()
logger = logging.getLogger(__name__)

# Mandatory 14-field state defined in the Component 2 blueprint
class MatchingState(TypedDict):
    WorkflowId: str
    Objective: str
    Plan: List[str]
    CurrentStep: str
    CompletedSteps: List[str]
    ToolResults: List[Dict[str, Any]]
    ValidationResults: List[Dict[str, Any]]
    Errors: List[str]
    Retries: int
    ApprovalStatus: str 
    FinalOutcome: Optional[Dict[str, Any]]
    StartedAt: str
    UpdatedAt: str
    CompletedAt: Optional[str]
    usage_metadata: Optional[Dict[str, Any]]
    # Dynamic fields passed from ASP.NET Core
    customer_latitude: Optional[float]
    customer_longitude: Optional[float]
    urgency_level: Optional[int]
    eligible_providers: Optional[List[Dict[str, Any]]]

# Initialize Gemini model
llm = ChatGoogleGenerativeAI(
    model="gemini-3.6-flash",
    google_api_key=os.getenv("GOOGLE_API_KEY")
)

def match_and_score_providers(state: MatchingState) -> Dict[str, Any]:
    state["UpdatedAt"] = datetime.now(timezone.utc).isoformat()
    state["CurrentStep"] = "Scoring_Candidates"
    
    try:
        # 1. Parse urgency level (use dynamic field if passed from C#, else parse string)
        urgency = state.get("urgency_level") or 2
        if "urgency:" in state.get("Objective", "").lower():
            try:
                urgency = int(state["Objective"].lower().split("urgency:")[1].strip()[0])
            except (ValueError, IndexError):
                pass

        # 2. Get customer coordinates dynamically from C# (fallback to Colombo baseline)
        cust_lat = state.get("customer_latitude") or 6.9270
        cust_lon = state.get("customer_longitude") or 79.8610

        # 3. Check for dynamic database providers sent from ASP.NET Core
        providers = state.get("eligible_providers")

        # Fallback to tools.py only if no live candidates were passed
        if not providers:
            try:
                search_res = SearchEligibleProviders.invoke({
                    "urgency": urgency,
                    "requirements": ["plumbing"]
                })
            except Exception:
                search_res = SearchEligibleProviders.invoke({
                    "category": "plumbing",
                    "required_skills": ["plumbing"]
                })
            providers = search_res.get("providers", []) if isinstance(search_res, dict) else []

        # 4. Spatial Distance Calculation & Urgency-Adaptive Scoring
        scored_candidates = []
        for p in providers:
            p_lat = p.get("latitude", 6.9271)
            p_lon = p.get("longitude", 79.8612)
            radius_limit = p.get("operating_radius_km") or p.get("OperatingRadiusKm") or 5.0

            dist_res = CalculateDistance.invoke({
                "cust_lat": cust_lat,
                "cust_lon": cust_lon,
                "prov_lat": p_lat,
                "prov_lon": p_lon
            })
            distance = dist_res.get("distance_km", 999.0) if isinstance(dist_res, dict) else 999.0

            if distance > radius_limit:
                continue

            rating = float(p.get("rating", 4.0))
            verified = bool(p.get("verified", False))

            norm_rating = rating / 5.0
            norm_proximity = 1.0 / (1.0 + (distance / 5.0))

            if urgency >= 4:
                # Emergency Jobs: 80% Proximity / 20% Rating
                score = round((norm_proximity * 0.8) + (norm_rating * 0.2), 3)
            else:
                # Scheduled Jobs: 70% Quality & Verification / 30% Proximity
                verified_bonus = 0.1 if verified else 0.0
                effective_quality = min(norm_rating + verified_bonus, 1.0)
                score = round((effective_quality * 0.7) + (norm_proximity * 0.3), 3)

            scored_candidates.append({
                "provider_id": p.get("provider_id") or p.get("id", "unknown"),
                "name": p.get("name") or p.get("business_name", "Provider"),
                "score": score,
                "distance_km": distance,
                "rating": rating,
                "verified": verified,
                "match_rationale": ""
            })

        # Rank candidates descending by score
        scored_candidates.sort(key=lambda x: x["score"], reverse=True)
        for idx, c in enumerate(scored_candidates):
            c["rank"] = idx + 1

        top_candidate = scored_candidates[0] if scored_candidates else None

        # 5. Invoke Gemini for Natural-Language Audit Rationale & Token Tracking
        if top_candidate:
            try:
                prompt = [
                    SystemMessage(content="You are an expert dispatcher for the AssistLK platform. Provide a concise, professional 1-sentence audit rationale explaining why this provider is the optimal match."),
                    HumanMessage(content=f"Objective: {state['Objective']}. Selected: {top_candidate['name']}, Urgency: {urgency}/5, Distance: {top_candidate['distance_km']}km, Rating: {top_candidate['rating']} stars, Verified: {top_candidate['verified']}.")
                ]
                ai_msg = llm.invoke(prompt)

                if isinstance(ai_msg.content, list):
                    text_blocks = [
                        block.get("text", "") 
                        for block in ai_msg.content 
                        if isinstance(block, dict) and "text" in block
                    ]
                    top_candidate["match_rationale"] = " ".join(text_blocks)
                else:
                    top_candidate["match_rationale"] = str(ai_msg.content)

                if hasattr(ai_msg, "usage_metadata") and ai_msg.usage_metadata:
                    state["usage_metadata"] = ai_msg.usage_metadata
            except Exception as llm_err:
                logger.warning(f"LLM rationale fallback: {llm_err}")
                top_candidate["match_rationale"] = f"Top-ranked match based on distance ({top_candidate['distance_km']}km) and score ({top_candidate['score']})."

        state["ToolResults"] = scored_candidates
        state["FinalOutcome"] = {
            "recommended_candidate": top_candidate,
            "recommended_provider": top_candidate
        }
        state["ApprovalStatus"] = "Pending"
        state["CompletedSteps"].append("match_and_score_providers")

    except Exception as e:
        print(f"--> ERROR inside match_and_score_providers: {e}")
        state["Errors"].append(str(e))
        state["ApprovalStatus"] = "Failed"

    return state

def human_approval_gate(state: MatchingState) -> Dict[str, Any]:
    state["UpdatedAt"] = datetime.now(timezone.utc).isoformat()

    # Short-circuit if previous node failed
    if state.get("ApprovalStatus") == "Failed" or state.get("Errors"):
        state["CurrentStep"] = "Failed"
        return state

    state["CurrentStep"] = "Awaiting_Admin_Approval"
    final_outcome = state.get("FinalOutcome") or {}

    decision = interrupt({
        "message": "High-impact match awaiting Admin review",
        "workflow_id": state.get("WorkflowId"),
        "recommended": final_outcome.get("recommended_candidate")
    })

    if isinstance(decision, dict) and decision.get("action") == "Approve":
        state["ApprovalStatus"] = "Approved"
    else:
        state["ApprovalStatus"] = "Rejected"

    state["CompletedSteps"].append("human_approval_gate")
    return state

def finalize_workflow(state: MatchingState) -> Dict[str, Any]:
    state["CompletedAt"] = datetime.now(timezone.utc).isoformat()
    state["CurrentStep"] = "Completed"
    return state

# Compile graph with MemorySaver checkpointer
builder = StateGraph(MatchingState)
builder.add_node("match_and_score_providers", match_and_score_providers)
builder.add_node("human_approval_gate", human_approval_gate)
builder.add_node("finalize_workflow", finalize_workflow)

builder.add_edge(START, "match_and_score_providers")
builder.add_edge("match_and_score_providers", "human_approval_gate")
builder.add_edge("human_approval_gate", "finalize_workflow")
builder.add_edge("finalize_workflow", END)

checkpointer = MemorySaver()
graph = builder.compile(checkpointer=checkpointer)