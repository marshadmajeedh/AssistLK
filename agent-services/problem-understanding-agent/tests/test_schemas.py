# Purpose: Tests Pydantic input and output DTO schemas for Problem Understanding Agent.
# Why: Validates data contract boundaries between .NET backend and Python AI agent, ensuring invalid payloads are rejected before reasoning.
# Real vs Mocked: Real Pydantic schema validation. No LLM or database involved.
# Prerequisites: Python virtual environment with dependencies installed in agent-services/problem-understanding-agent/.venv.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_schemas.py

"""Unit tests for Pydantic input/output contracts, invariants, and validation rules."""
import uuid
import pytest
from pydantic import ValidationError
from app.schemas.request import (
    AgentExecutionRequest,
    ClarificationHistoryItemDto,
    ProblemUnderstandingInputDto,
)
from app.schemas.response import (
    AgentExecutionResponse,
    ExecutionMetadataDto,
    ProblemUnderstandingOutputDto,
)


# Purpose: Validates that standard camelCase .NET payload parses correctly into typed models.
# Why: Ensures seamless serialization/deserialization between ASP.NET Core backend and FastAPI agent service.
# Input/Condition: Valid JSON dictionary matching .NET AgentExecutionRequest with nested clarificationHistory and GPS.
# Pass condition: All fields deserialize into corresponding snake_case Pydantic attributes matching expected values.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_schemas.py::test_input_schema_valid_payload
def test_input_schema_valid_payload():
    """Validates that standard camelCase .NET payload parses correctly into typed models."""
    req_id = uuid.uuid4()
    srv_id = uuid.uuid4()
    payload = {
        "requestId": str(req_id),
        "agentName": "ProblemUnderstandingAgent",
        "operation": "analyze-problem",
        "input": {
            "serviceRequestId": str(srv_id),
            "description": "Leaking kitchen sink faucet dripping constantly.",
            "locationText": "Kandy City Center",
            "latitude": 7.2906,
            "longitude": 80.6337,
            "categoryHint": "Plumbing",
            "clarificationHistory": [
                {
                    "round": 1,
                    "question": "Is the faucet loose or leaking from base?",
                    "answer": "Leaking from the base spout",
                }
            ],
        },
    }

    req = AgentExecutionRequest.model_validate(payload)
    assert req.request_id == req_id
    assert req.input.service_request_id == srv_id
    assert req.input.latitude == 7.2906
    assert req.input.longitude == 80.6337
    assert req.input.category_hint == "Plumbing"
    assert len(req.input.clarification_history) == 1
    assert req.input.clarification_history[0].round == 1




# Purpose: Validates that an empty or missing description string fails schema validation.
# Why: Service requests must have problem descriptions so the AI and human providers have context.
# Input/Condition: ProblemUnderstandingInputDto instantiated with description="".
# Pass condition: Pydantic raises ValidationError mentioning "description".
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_schemas.py::test_input_schema_rejects_empty_description
def test_input_schema_rejects_empty_description():
    """Validates that an empty or missing description fails schema validation."""
    with pytest.raises(ValidationError) as exc:
        ProblemUnderstandingInputDto(
            serviceRequestId=uuid.uuid4(),
            description="",
        )
    assert "description" in str(exc.value)


# Purpose: Validates that out-of-range GPS coordinates (latitude > 90 or longitude < -180) are rejected.
# Why: Prevents invalid geographic coordinates from entering matching and location services.
# Input/Condition: Latitude=95.0 (> 90) or Longitude=-185.0 (< -180).
# Pass condition: Pydantic raises ValidationError for both out-of-bound inputs.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_schemas.py::test_input_schema_rejects_out_of_bound_gps
def test_input_schema_rejects_out_of_bound_gps():
    """Validates that latitude > 90 and longitude < -180 are rejected by input schema."""
    with pytest.raises(ValidationError):
        ProblemUnderstandingInputDto(
            serviceRequestId=uuid.uuid4(),
            description="Valid description",
            latitude=95.0,  # Invalid (> 90)
            longitude=80.0,
        )

    with pytest.raises(ValidationError):
        ProblemUnderstandingInputDto(
            serviceRequestId=uuid.uuid4(),
            description="Valid description",
            latitude=7.0,
            longitude=-185.0,  # Invalid (< -180)
        )


# Purpose: Validates round numbering (must be >= 1) and non-empty question in clarification history.
# Why: Clarification history tracks user question/answer rounds and must maintain positive round indexes and valid question text.
# Input/Condition: round=0 or question="".
# Pass condition: Pydantic raises ValidationError in both cases.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_schemas.py::test_clarification_history_item_validation
def test_clarification_history_item_validation():
    """Validates round numbering and question length limits in clarification history."""
    with pytest.raises(ValidationError):
        ClarificationHistoryItemDto(round=0, question="Valid?", answer="Yes")

    with pytest.raises(ValidationError):
        ClarificationHistoryItemDto(round=1, question="", answer="Yes")


# Purpose: Validates that output DTO rejects non-canonical category strings.
# Why: Ensures AI agent output aligns with the canonical service categories supported by the platform.
# Input/Condition: ProblemUnderstandingOutputDto created with category="Carpentry" (unsupported category).
# Pass condition: Pydantic raises ValidationError due to Literal/Enum category constraint.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_schemas.py::test_output_schema_canonical_category_enforcement
def test_output_schema_canonical_category_enforcement():
    """Validates that output rejects non-canonical category strings."""
    with pytest.raises(ValidationError):
        ProblemUnderstandingOutputDto(
            category="Carpentry",  # Not an allowed category
            problemSummary="Possible woodwork issue.",
        )


# Purpose: Validates that output confidence is clamped between 0.0 and 1.0.
# Why: Downstream matching algorithms require a normalized confidence score regardless of raw model output.
# Input/Condition: Out-of-range confidence scores: 1.5 (> 1.0) and -0.3 (< 0.0).
# Pass condition: Resulting confidence is clamped to 1.0 and 0.0 respectively.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_schemas.py::test_output_schema_confidence_clamping
def test_output_schema_confidence_clamping():
    """Validates that confidence is clamped to [0.0, 1.0]."""
    out1 = ProblemUnderstandingOutputDto(
        category="Plumbing",
        problemSummary="Possible pipe leak.",
        confidence=1.5,
    )
    assert out1.confidence == 1.0

    out2 = ProblemUnderstandingOutputDto(
        category="Plumbing",
        problemSummary="Possible pipe leak.",
        confidence=-0.3,
    )
    assert out2.confidence == 0.0


# Purpose: Validates that follow-up questions are strictly bounded to at most 3 items.
# Why: Prevents cognitive overload for customers by limiting clarification questions to maximum 3.
# Input/Condition: ProblemUnderstandingOutputDto initialized with 5 follow-up questions.
# Pass condition: Length of follow_up_questions is truncated to 3, keeping the first 3 items.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_schemas.py::test_output_schema_max_3_follow_up_questions
def test_output_schema_max_3_follow_up_questions():
    """Validates that follow-up questions are strictly bounded to at most 3 items."""
    out = ProblemUnderstandingOutputDto(
        category="Unclassified",
        problemSummary="Possible issue.",
        needsMoreInformation=True,
        followUpQuestions=["Q1", "Q2", "Q3", "Q4", "Q5"],
    )
    assert len(out.follow_up_questions) == 3
    assert out.follow_up_questions == ["Q1", "Q2", "Q3"]


# Purpose: Verifies that no hidden reasoning, chain-of-thought, or raw prompt fields exist in response schemas.
# Why: Security requirement to prevent model internal chain-of-thought, system prompts, or credentials from leaking to customers.
# Input/Condition: Inspection of field names in ProblemUnderstandingOutputDto, ExecutionMetadataDto, and AgentExecutionResponse.
# Pass condition: None of the prohibited keys (e.g. "chain_of_thought", "thought", "scratchpad", "prompt", "api_key") exist in model fields.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_schemas.py::test_no_chain_of_thought_fields
def test_no_chain_of_thought_fields():
    """Verifies that no hidden reasoning, chain-of-thought, or raw prompt fields exist in response schemas."""
    output_fields = set(ProblemUnderstandingOutputDto.model_fields.keys())
    metadata_fields = set(ExecutionMetadataDto.model_fields.keys())
    response_fields = set(AgentExecutionResponse.model_fields.keys())

    prohibited = {
        "chain_of_thought", "chainOfThought",
        "thought", "thoughts",
        "scratchpad", "hidden_reasoning",
        "prompt", "raw_prompt",
        "api_key", "bearer_token", "jwt",
    }

    assert prohibited.isdisjoint(output_fields)
    assert prohibited.isdisjoint(metadata_fields)
    assert prohibited.isdisjoint(response_fields)


# Purpose: Verifies that result fields live solely inside the `result` property and are not duplicated at root level.
# Why: Architecture contract ensures clean separation between envelope metadata and agent domain results.
# Input/Condition: Inspection of field names on AgentExecutionResponse.
# Pass condition: Authoritative result fields are disjoint from root fields; only 'result' and 'metadata' contain them.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_schemas.py::test_no_duplicated_result_fields_at_root
def test_no_duplicated_result_fields_at_root():
    """
    Verifies Correction #5: result fields (category, confidence, urgency, etc.)
    must live solely inside `result` and NOT be duplicated at root level.
    """
    response_fields = set(AgentExecutionResponse.model_fields.keys())
    authoritative_fields = {"category", "problemSummary", "confidence", "urgency", "needsMoreInformation", "followUpQuestions"}

    # None of the authoritative result fields should exist directly on AgentExecutionResponse
    assert authoritative_fields.isdisjoint(response_fields)
    assert "result" in response_fields
    assert "metadata" in response_fields
