from typing import TypedDict, List, Dict, Any, Optional
from datetime import datetime

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
