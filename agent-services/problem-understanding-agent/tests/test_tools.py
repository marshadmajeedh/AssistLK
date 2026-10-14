# Purpose: Tests deterministic domain tools used by the Problem Understanding Agent graph nodes.
# Why: Validates location extraction, heuristic classification, and service knowledge retrieval tools that support agent reasoning without needing external LLMs.
# Real vs Mocked: Real deterministic tool implementations in pure Python; no mocks and no network calls.
# Prerequisites: Python virtual environment with dependencies installed in agent-services/problem-understanding-agent/.venv.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_tools.py

"""Unit tests for Component 1 deterministic tools."""
from app.tools.location_extraction import extract_location_tool
from app.tools.problem_classification import classify_problem_tool
from app.tools.service_knowledge import get_service_knowledge_tool


# --- Location Extraction Tool Tests ---


# Purpose: Verifies that valid latitude/longitude within geographic bounds are preserved.
# Why: Accurate GPS coordinates are necessary for downstream provider dispatch and matching.
# Input/Condition: locationText="Colombo 03", latitude=6.901234, longitude=79.851234.
# Pass condition: Result has_coordinates is True and latitude/longitude match input exactly.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_tools.py::test_location_tool_valid_coordinates
def test_location_tool_valid_coordinates():
    """Verifies that valid latitude/longitude within bounds are preserved."""
    result = extract_location_tool("Colombo 03", 6.901234, 79.851234)
    assert result.normalized_location == "Colombo 03"
    assert result.has_coordinates is True
    assert result.latitude == 6.901234
    assert result.longitude == 79.851234


# Purpose: Verifies that if only latitude or only longitude is provided, partial coordinates are discarded.
# Why: A single coordinate is unusable for geospatial distance calculation and would lead to incorrect matching.
# Input/Condition: One coordinate pair with latitude only (7.2906, None), another with longitude only (None, 80.2170).
# Pass condition: has_coordinates is False and both coordinates become None for both cases.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_tools.py::test_location_tool_discards_partial_coordinates
def test_location_tool_discards_partial_coordinates():
    """Verifies that if only latitude or only longitude is supplied, both are discarded."""
    res1 = extract_location_tool("Kandy", 7.2906, None)
    assert res1.has_coordinates is False
    assert res1.latitude is None
    assert res1.longitude is None

    res2 = extract_location_tool("Galle", None, 80.2170)
    assert res2.has_coordinates is False
    assert res2.latitude is None
    assert res2.longitude is None


# Purpose: Verifies that coordinates exceeding geographic limits are discarded rather than corrupted.
# Why: Guards against malformed data (e.g. latitude > 90 or longitude > 180) corrupting map rendering or spatial queries.
# Input/Condition: Out-of-bound latitude (95.0, 79.8) and out-of-bound longitude (9.6615, 185.0).
# Pass condition: has_coordinates is False and both coordinates become None.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_tools.py::test_location_tool_discards_out_of_bound_coordinates
def test_location_tool_discards_out_of_bound_coordinates():
    """Verifies that coordinates exceeding geographic limits are discarded rather than corrupted."""
    result = extract_location_tool("Negombo", 95.0, 79.8)
    assert result.has_coordinates is False
    assert result.latitude is None
    assert result.longitude is None

    res_lon = extract_location_tool("Jaffna", 9.6615, 185.0)
    assert res_lon.has_coordinates is False
    assert res_lon.latitude is None
    assert res_lon.longitude is None


# Purpose: Verifies that location text has surrounding whitespace trimmed and blank strings become None.
# Why: Clean address and area names prevent duplicate location records and formatting glitches.
# Input/Condition: "   Nugegoda Junction   " with whitespace, and "     " with whitespace only.
# Pass condition: "Nugegoda Junction" is trimmed; whitespace-only string returns None.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_tools.py::test_location_tool_normalizes_whitespace
def test_location_tool_normalizes_whitespace():
    """Verifies that location text is stripped and empty strings return None."""
    res1 = extract_location_tool("   Nugegoda Junction   ", None, None)
    assert res1.normalized_location == "Nugegoda Junction"

    res2 = extract_location_tool("     ", None, None)
    assert res2.normalized_location is None


# --- Problem Classification Tool Tests ---


# Purpose: Verifies deterministic keyword classification across canonical service categories.
# Why: When LLMs are offline or confidence is low, deterministic classification ensures requests are still categorized.
# Input/Condition: Problem descriptions containing clear keywords for Plumbing, Electrical, Vehicle Repair, and Appliance Repair.
# Pass condition: Each returns the exact canonical category, matching supporting keywords, and confidence >= 0.6.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_tools.py::test_classification_tool_canonical_categories
def test_classification_tool_canonical_categories():
    """Verifies deterministic classification across canonical service categories."""
    plumbing = classify_problem_tool("The pipe under the bathroom sink is leaking water everywhere.")
    assert plumbing.category == "Plumbing"
    assert "pipe" in plumbing.supporting_terms
    assert plumbing.confidence >= 0.6

    electrical = classify_problem_tool("The wall socket is sparking and the main breaker tripped.")
    assert electrical.category == "Electrical"
    assert "socket" in electrical.supporting_terms or "sparking" in electrical.supporting_terms
    assert electrical.confidence >= 0.6

    vehicle = classify_problem_tool("My car engine will not start and the battery seems dead.")
    assert vehicle.category == "Vehicle Repair"
    assert "car" in vehicle.supporting_terms
    assert vehicle.confidence >= 0.6

    appliance = classify_problem_tool("The refrigerator is not cooling and the freezer stopped working.")
    assert appliance.category == "Appliance Repair"
    assert "refrigerator" in appliance.supporting_terms or "freezer" in appliance.supporting_terms
    assert appliance.confidence >= 0.6


# Purpose: Verifies that vague, non-specific descriptions fall back to "Unclassified" with baseline confidence.
# Why: Prevents assigning a false category when there is insufficient domain detail.
# Input/Condition: "Something is broken and needs urgent help."
# Pass condition: Category is "Unclassified", confidence is 0.2, and supporting_terms list is empty.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_tools.py::test_classification_tool_vague_input
def test_classification_tool_vague_input():
    """Verifies that non-specific input defaults to Unclassified with baseline confidence."""
    result = classify_problem_tool("Something is broken and needs urgent help.")
    assert result.category == "Unclassified"
    assert result.confidence == 0.2
    assert len(result.supporting_terms) == 0


# Purpose: Verifies that an empty string returns Unclassified with 0.0 confidence.
# Why: Edge-case safety when no description text is available.
# Input/Condition: Empty string ("").
# Pass condition: Category is "Unclassified" and confidence is exactly 0.0.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_tools.py::test_classification_tool_empty_input
def test_classification_tool_empty_input():
    """Verifies that empty string returns Unclassified with 0.0 confidence."""
    result = classify_problem_tool("")
    assert result.category == "Unclassified"
    assert result.confidence == 0.0


# --- Service Knowledge Tool Tests ---


# Purpose: Verifies domain family lookup and safety inspection advice per category.
# Why: Graph reasoning node uses domain knowledge to suggest safety precautions and identify missing diagnostic information.
# Input/Condition: Categories "Electrical", "Plumbing", and "Unclassified".
# Pass condition: Electrical and Plumbing recommend inspection and supply domain checklists; Unclassified does not.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_tools.py::test_service_knowledge_tool_retrieval
def test_service_knowledge_tool_retrieval():
    """Verifies domain family and professional inspection recommendations per category."""
    elec = get_service_knowledge_tool("Electrical")
    assert elec.service_family == "Electrical Systems"
    assert elec.recommends_professional_inspection is True
    assert "circuit breakers" in elec.possible_missing_information[0]

    plumb = get_service_knowledge_tool("Plumbing")
    assert plumb.service_family == "Plumbing and Water Supply"
    assert plumb.recommends_professional_inspection is True
    assert "shutoff valve" in plumb.possible_missing_information[1]

    unclass = get_service_knowledge_tool("Unclassified")
    assert unclass.service_family == "General Services"
    assert unclass.recommends_professional_inspection is False

