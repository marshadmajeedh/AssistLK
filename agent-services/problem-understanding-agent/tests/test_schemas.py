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


def test_input_schema_rejects_empty_description():
    """Validates that an empty or missing description fails schema validation."""
    with pytest.raises(ValidationError) as exc:
        ProblemUnderstandingInputDto(
            serviceRequestId=uuid.uuid4(),
            description="",
        )
    assert "description" in str(exc.value)


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


def test_clarification_history_item_validation():
    """Validates round numbering and question length limits in clarification history."""
    with pytest.raises(ValidationError):
        ClarificationHistoryItemDto(round=0, question="Valid?", answer="Yes")

    with pytest.raises(ValidationError):
        ClarificationHistoryItemDto(round=1, question="", answer="Yes")


def test_output_schema_canonical_category_enforcement():
    """Validates that output rejects non-canonical category strings."""
    with pytest.raises(ValidationError):
        ProblemUnderstandingOutputDto(
            category="Carpentry",  # Not an allowed category
            problemSummary="Possible woodwork issue.",
        )


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
