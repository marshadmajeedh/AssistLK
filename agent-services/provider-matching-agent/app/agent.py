from datetime import datetime, timezone
from langgraph.graph import StateGraph, START, END
from langgraph.checkpoint.memory import MemorySaver
from langchain_core.messages import HumanMessage, AIMessage, SystemMessage
from .state import MatchingState
from .tools import SearchEligibleProviders, CalculateDistance
import os
from langchain_google_genai import ChatGoogleGenerativeAI
import json

llm = ChatGoogleGenerativeAI(
    model="gemini-2.5-flash",
    google_api_key=os.getenv("GOOGLE_API_KEY", "dummy_key")
)

tools = [SearchEligibleProviders, CalculateDistance]
llm_with_tools = llm.bind_tools(tools)

def match_providers(state: MatchingState) -> dict:
    state["UpdatedAt"] = datetime.now(timezone.utc).isoformat()
    state["CurrentStep"] = "Fetching and scoring providers"
    
    try:
        urgency = int(state["Objective"].split("urgency:")[-1].strip()[0]) if "urgency:" in state["Objective"] else 2
        
        providers = SearchEligibleProviders.invoke({"urgency": urgency, "requirements": []})
        
        scored_providers = []
        for p in providers:
            if "error" in p:
                raise Exception(p["error"])
                
            dist = CalculateDistance.invoke({"provider_id": p["id"], "job_location": {}})
            
            # Dynamic Scoring logic
            if urgency >= 4:
                # Emergency Jobs: 80% Proximity + 20% Rating
                score = (1 / max(dist, 0.1)) * 0.8 + (p["rating"] / 5.0) * 0.2
                rationale = f"Emergency matching: High weight on proximity ({dist}km)."
            else:
                # Scheduled Jobs: 70% Verified Skills/Rating + 30% Proximity
                verified_bonus = 0.2 if p["verified"] else 0
                score = ((p["rating"] / 5.0) + verified_bonus) * 0.7 + (1 / max(dist, 0.1)) * 0.3
                rationale = f"Scheduled matching: High weight on verified skills and rating."
            
            scored_providers.append({**p, "score": score, "distance": dist, "rationale": rationale})
        
        scored_providers.sort(key=lambda x: x["score"], reverse=True)
        top_provider = scored_providers[0] if scored_providers else None
        
        state["ToolResults"] = scored_providers
        state["FinalOutcome"] = {"recommended_provider": top_provider}
        state["ApprovalStatus"] = "Pending"
        state["CompletedSteps"].append("match_providers")
    except Exception as e:
        state["Errors"].append(str(e))
        state["ApprovalStatus"] = "Failed"
        
    return state

def human_approval_gate(state: MatchingState) -> dict:
    # This node will be interrupted before execution
    state["UpdatedAt"] = datetime.now(timezone.utc).isoformat()
    # When resumed, we check if Approved or Rejected
    return state

def complete_workflow(state: MatchingState) -> dict:
    state["CompletedAt"] = datetime.now(timezone.utc).isoformat()
    state["CurrentStep"] = "Completed"
    return state

def route_approval(state: MatchingState) -> str:
    if state["ApprovalStatus"] == "Pending":
        return "human_approval_gate"
    return "complete_workflow"

builder = StateGraph(MatchingState)
builder.add_node("match_providers", match_providers)
builder.add_node("human_approval_gate", human_approval_gate)
builder.add_node("complete_workflow", complete_workflow)

builder.add_edge(START, "match_providers")
builder.add_conditional_edges("match_providers", route_approval, {
    "human_approval_gate": "human_approval_gate",
    "complete_workflow": "complete_workflow"
})
builder.add_edge("human_approval_gate", "complete_workflow")
builder.add_edge("complete_workflow", END)

checkpointer = MemorySaver()
graph = builder.compile(checkpointer=checkpointer, interrupt_before=["human_approval_gate"])
