"""Unit tests for Component 2 allow-listed tools: CalculateDistance and SearchEligibleProviders."""
import pytest
from app.tools import CalculateDistance, SearchEligibleProviders


# --- CalculateDistance Tool Tests ---


def test_calculate_distance_identical_coordinates():
    """Verifies that identical coordinates yield exactly 0.0 km distance."""
    result = CalculateDistance.invoke({
        "cust_lat": 6.9271,
        "cust_lon": 79.8612,
        "prov_lat": 6.9271,
        "prov_lon": 79.8612,
    })
    assert result["status"] == "success"
    assert result["distance_km"] == 0.0


def test_calculate_distance_known_colombo_to_kandy():
    """
    Verifies geographic accuracy of Haversine formula.
    Colombo (6.9344, 79.8428) to Kandy (7.2906, 80.6337) is approximately 95 - 105 km.
    """
    result = CalculateDistance.invoke({
        "cust_lat": 6.9344,
        "cust_lon": 79.8428,
        "prov_lat": 7.2906,
        "prov_lon": 80.6337,
    })
    assert result["status"] == "success"
    assert 90.0 <= result["distance_km"] <= 110.0


def test_calculate_distance_local_urban_proximity():
    """
    Verifies small distance calculation within Colombo urban grid.
    Coordinates separated by ~1.3 km return reasonable sub-2km distance.
    """
    result = CalculateDistance.invoke({
        "cust_lat": 6.9271,
        "cust_lon": 79.8612,
        "prov_lat": 6.9350,
        "prov_lon": 79.8520,
    })
    assert result["status"] == "success"
    assert 1.0 <= result["distance_km"] <= 2.0


def test_calculate_distance_southern_hemisphere():
    """
    Verifies Haversine formula handles negative latitude coordinates (Southern Hemisphere).
    Sydney (-33.8688, 151.2093) to Melbourne (-37.8136, 144.9631) is ~710 km.
    """
    result = CalculateDistance.invoke({
        "cust_lat": -33.8688,
        "cust_lon": 151.2093,
        "prov_lat": -37.8136,
        "prov_lon": 144.9631,
    })
    assert result["status"] == "success"
    assert 700.0 <= result["distance_km"] <= 730.0


def test_calculate_distance_invalid_input_validation():
    """Verifies that non-numeric coordinate inputs are rejected by tool schema validation."""
    with pytest.raises(Exception):
        CalculateDistance.invoke({
            "cust_lat": "invalid_latitude",
            "cust_lon": 79.8612,
            "prov_lat": 6.9271,
            "prov_lon": 79.8612,
        })


def test_calculate_distance_internal_error_handling():
    """Verifies that internal runtime errors in distance calculation return fallback 9999.0 km."""
    result = CalculateDistance.func(None, 79.8612, 6.9271, 79.8612)
    assert result["status"] == "error"
    assert result["distance_km"] == 9999.0
    assert "message" in result


def test_calculate_distance_metadata():
    """Verifies LangChain tool declaration and docstring."""
    assert CalculateDistance.name == "CalculateDistance"
    assert "Haversine" in CalculateDistance.description


# --- SearchEligibleProviders Tool Tests ---


def test_search_eligible_providers_default_structure():
    """Verifies that SearchEligibleProviders returns structured provider candidates."""
    result = SearchEligibleProviders.invoke({})
    assert result["status"] == "success"
    assert "providers" in result
    assert isinstance(result["providers"], list)
    assert len(result["providers"]) > 0


def test_search_eligible_providers_candidate_schema():
    """Verifies each provider returned satisfies the required schema."""
    result = SearchEligibleProviders.invoke({"category": "plumbing", "urgency": 3})
    assert result["status"] == "success"
    providers = result["providers"]

    for p in providers:
        assert "id" in p
        assert "name" in p
        assert "skills" in p and isinstance(p["skills"], list)
        assert "verified" in p and isinstance(p["verified"], bool)
        assert "rating" in p and 0.0 <= p["rating"] <= 5.0
        assert "latitude" in p
        assert "longitude" in p


def test_search_eligible_providers_custom_parameters():
    """Verifies invoking SearchEligibleProviders with custom requirements."""
    result = SearchEligibleProviders.invoke({
        "category": "electrical",
        "required_skills": ["wiring", "breaker"],
        "urgency": 5,
        "requirements": ["electrical"],
    })
    assert result["status"] == "success"
    assert isinstance(result["providers"], list)


def test_search_eligible_providers_metadata():
    """Verifies LangChain tool metadata and docstring."""
    assert SearchEligibleProviders.name == "SearchEligibleProviders"
    assert "Queries verified" in SearchEligibleProviders.description
