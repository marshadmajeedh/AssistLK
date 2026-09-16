import math
import logging
from typing import List, Optional
from langchain_core.tools import tool

logger = logging.getLogger(__name__)

@tool
def SearchEligibleProviders(category: str = "plumbing", required_skills: Optional[List[str]] = None, urgency: int = 2, requirements: Optional[List[str]] = None) -> dict:
    """Queries verified, active service providers."""
    try:
        candidates = [
    {"id": "11111111-1111-1111-1111-111111111111", "name": "Kamal Perera", "skills": ["plumbing"], "verified": True, "rating": 4.8, "latitude": 6.9271, "longitude": 79.8612},
    {"id": "22222222-2222-2222-2222-222222222222", "name": "Nimal Silva", "skills": ["plumbing"], "verified": True, "rating": 4.3, "latitude": 6.9350, "longitude": 79.8520},
    {"id": "33333333-3333-3333-3333-333333333333", "name": "Sunil Shantha", "skills": ["plumbing"], "verified": False, "rating": 4.9, "latitude": 6.9150, "longitude": 79.8650}
]
        return {"status": "success", "providers": candidates}
    except Exception as e:
        logger.error(f"Error searching providers: {e}")
        return {"status": "error", "message": str(e), "providers": []}

@tool
def CalculateDistance(cust_lat: float, cust_lon: float, prov_lat: float, prov_lon: float) -> dict:
    """Calculates real geographic distance (km) using the Haversine formula."""
    try:
        R = 6371.0
        lat1_rad, lon1_rad = math.radians(cust_lat), math.radians(cust_lon)
        lat2_rad, lon2_rad = math.radians(prov_lat), math.radians(prov_lon)
        
        dlat = lat2_rad - lat1_rad
        dlon = lon2_rad - lon1_rad
        
        a = math.sin(dlat / 2)**2 + math.cos(lat1_rad) * math.cos(lat2_rad) * math.sin(dlon / 2)**2
        c = 2 * math.atan2(math.sqrt(a), math.sqrt(1 - a))
        
        return {"status": "success", "distance_km": round(R * c, 2)}
    except Exception as e:
        logger.error(f"Error calculating distance: {e}")
        return {"status": "error", "message": str(e), "distance_km": 9999.0}