from langchain_core.tools import tool
import random

@tool
def SearchEligibleProviders(urgency: int, requirements: list[str]) -> list[dict]:
    """Search for providers eligible for the job based on requirements and urgency."""
    try:
        # Mocking database fetch for eligible providers
        return [
            {"id": "p1", "name": "Alice Services", "skills": ["plumbing", "electric"], "verified": True, "rating": 4.8, "proximity": 2.5},
            {"id": "p2", "name": "Bob Repairs", "skills": ["plumbing"], "verified": True, "rating": 4.5, "proximity": 5.0},
            {"id": "p3", "name": "Charlie Fixes", "skills": ["plumbing"], "verified": False, "rating": 4.2, "proximity": 1.2},
        ]
    except Exception as e:
        return [{"error": str(e)}]

@tool
def CalculateDistance(provider_id: str, job_location: dict) -> float:
    """Calculate distance in km between a provider and the job location."""
    try:
        # Mocking distance calculation
        return round(random.uniform(1.0, 15.0), 2)
    except Exception as e:
        return -1.0
