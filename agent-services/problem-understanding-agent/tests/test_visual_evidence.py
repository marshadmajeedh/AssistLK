# Purpose: Tests visual evidence schema validation, size boundaries, provider capability reporting, and privacy protection.
# Why: Validates that uploaded problem photos strictly adhere to image limits (JPEG only, max 3 photos, size caps) and that base64 data is never leaked into logs, representations, or API error messages.
# Real vs Mocked: Real Pydantic validation, FastAPI ASGI transport, and LangGraph pipeline. Uses synthetic stubs and offline provider; no live image decoding or cloud vision APIs.
# Prerequisites: Python virtual environment with dependencies installed in agent-services/problem-understanding-agent/.venv.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_visual_evidence.py

"""Synthetic transport fixtures only; no photographs, decoders or live providers."""
import base64
import json
from uuid import uuid4

import pytest
from pydantic import ValidationError
from app.graphs.problem_graph import create_problem_understanding_graph
from app.graphs.visual_evidence import prepare_visual_evidence
from app.providers.azure_openai_provider import AzureOpenAIProvider
from app.providers.gemini_provider import GeminiProvider
from app.providers.openai_provider import OpenAIProvider
from app.providers.offline_provider import OfflineSimulationProvider
from app.schemas.request import ProblemUnderstandingInputDto
from app.schemas.visual_evidence import VisualEvidence, MAX_IMAGE_BYTES, MAX_TOTAL_BYTES


def item(size=12, **changes):
    return {"attachmentId": str(uuid4()), "contentType": "image/jpeg",
            "dataBase64": base64.b64encode(b"x" * size).decode(),
            "width": 80, "height": 40, **changes}


def request(items):
    return ProblemUnderstandingInputDto(serviceRequestId=uuid4(),
        description="Water pipe burst in the kitchen causing flooding.", visualEvidence=items)


# Purpose: Verifies that 0, 1, or 3 valid image items are accepted and calculate decoded byte size correctly.
# Why: Component 1 permits up to 3 problem photo attachments; all valid subsets must be accepted.
# Input/Condition: Lists of 0, 1, or 3 valid JPEG VisualEvidence items.
# Pass condition: DTO parses cleanly with matching item count and decoded size of 12 bytes each.
# Note: Parameterized test command runs count=0, 1, and 3.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_visual_evidence.py -k "test_bounded_items_accepted"
@pytest.mark.parametrize("count", [0, 1, 3])
def test_bounded_items_accepted(count):
    parsed = request([item() for _ in range(count)])
    assert len(parsed.visual_evidence) == count
    assert all(x.decoded_size == 12 for x in parsed.visual_evidence)


# Purpose: Verifies backward compatibility when the visualEvidence field is omitted entirely from request payloads.
# Why: Existing text-only clients must continue to function without schema errors.
# Input/Condition: ProblemUnderstandingInputDto instantiated without visualEvidence argument.
# Pass condition: visual_evidence defaults to an empty list ([]).
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_visual_evidence.py::test_omitted_images_are_backward_compatible
def test_omitted_images_are_backward_compatible():
    assert ProblemUnderstandingInputDto(serviceRequestId=uuid4(), description="Leaking pipe").visual_evidence == []


# Purpose: Verifies that malformed image payloads (bad base64, non-JPEG MIME types, out-of-range dimensions, non-UUID IDs, or local paths) are rejected.
# Why: Prevents SSRF attacks, arbitrary file paths, invalid image encodings, and unsupported formats from entering the reasoning pipeline.
# Input/Condition: 15 distinct malformed attribute combinations.
# Pass condition: Pydantic raises ValidationError for every invalid permutation.
# Note: Parameterized test command runs all 15 cases.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_visual_evidence.py -k "test_invalid_evidence_rejected"
@pytest.mark.parametrize("changes", [
    {"dataBase64": "!bad!"}, {"dataBase64": ""}, {"dataBase64": "eA==\n"},
    {"dataBase64": "eB=="}, {"contentType": "image/png"}, {"contentType": "image/webp"},
    {"width": 0}, {"height": -1}, {"width": 2049}, {"height": 2049},
    {"width": True}, {"height": "40"}, {"attachmentId": "not-a-uuid"},
    {"attachmentId": "00000000-0000-0000-0000-000000000000"}, {"path": "/private/photo.jpg"},
])
def test_invalid_evidence_rejected(changes):
    with pytest.raises(ValidationError):
        request([item(**changes)])


# Purpose: Verifies that uploading 4 images is rejected because the maximum allowed limit is 3.
# Why: Strict quota enforcement prevents bandwidth abuse and token exhaustion during model inference.
# Input/Condition: Payload containing 4 image items.
# Pass condition: Pydantic raises ValidationError.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_visual_evidence.py::test_fourth_rejected
def test_fourth_rejected():
    with pytest.raises(ValidationError):
        request([item() for _ in range(4)])


# Purpose: Verifies that duplicate attachment IDs in the same request are rejected.
# Why: Each photo attachment must be uniquely identifiable for observation mapping.
# Input/Condition: Payload containing two items with identical attachmentId.
# Pass condition: Pydantic raises ValidationError.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_visual_evidence.py::test_duplicate_identity_rejected
def test_duplicate_identity_rejected():
    image = item()
    with pytest.raises(ValidationError):
        request([image, image])


# Purpose: Verifies that the decoded binary byte size is checked against MAX_IMAGE_BYTES, not just the base64 string length.
# Why: Base64 strings are ~33% larger than raw bytes; validation must measure actual binary payload size.
# Input/Condition: Image size exceeds MAX_IMAGE_BYTES by 1 or 3 bytes.
# Pass condition: Pydantic raises ValidationError.
# Note: Parameterized test command runs both size boundary cases.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_visual_evidence.py -k "test_decoded_size_limit_not_just_encoded_length"
@pytest.mark.parametrize("size", [MAX_IMAGE_BYTES + 1, MAX_IMAGE_BYTES + 3])
def test_decoded_size_limit_not_just_encoded_length(size):
    with pytest.raises(ValidationError):
        request([item(size)])


# Purpose: Verifies per-image and aggregate total byte size boundaries across multiple attachments.
# Why: Protects the system against cumulative payload bloat across multiple images.
# Input/Condition: Exactly two maximum-size images (equals MAX_TOTAL_BYTES), then two max-size images plus 1 extra byte.
# Pass condition: Sum of two max images equals MAX_TOTAL_BYTES; adding even 1 extra byte raises ValidationError.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_visual_evidence.py::test_per_image_and_aggregate_boundaries
def test_per_image_and_aggregate_boundaries():
    assert sum(x.decoded_size for x in request([item(MAX_IMAGE_BYTES), item(MAX_IMAGE_BYTES)]).visual_evidence) == MAX_TOTAL_BYTES
    with pytest.raises(ValidationError):
        request([item(MAX_IMAGE_BYTES), item(MAX_IMAGE_BYTES), item(1)])


class CapableStub(OfflineSimulationProvider):
    @property
    def supports_images(self):
        return True

    async def generate_problem_understanding(self, prompt, system_instruction, visual_evidence=None):
        result = await self._generate_text_result(prompt, system_instruction)
        result.additional_information = {}
        if visual_evidence:
            result = result.model_copy(update={"vision_status": "used",
                "attachment_ids_used": [i.attachment_id for i in visual_evidence],
                "visual_limitations": ["The exact cause is not visible."]})
        return result


# Purpose: Verifies that each provider adapter correctly declares whether it supports multimodal image reasoning.
# Why: Graph preparation node inspects supports_images to decide whether to dispatch images or report 'unsupported'.
# Input/Condition: Provider instances: OfflineSimulationProvider, GeminiProvider, OpenAIProvider, AzureOpenAIProvider.
# Pass condition: OfflineSimulationProvider reports False; cloud providers report True.
# Note: Parameterized test command runs all 4 provider adapters.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_visual_evidence.py -k "test_current_adapters_report_implemented_capability"
@pytest.mark.parametrize("provider", [
    OfflineSimulationProvider(),
    GeminiProvider("fake"),
    OpenAIProvider("fake"),
    AzureOpenAIProvider("fake", "https://fake.openai.azure.com"),
])
def test_current_adapters_report_implemented_capability(provider):
    assert provider.supports_images is (not isinstance(provider, OfflineSimulationProvider))


# Purpose: Verifies that visual evidence preparation and reasoning steps are decoupled and do not leak base64 bytes to repr or logs.
# Why: Clean separation of concerns between state preparation and graph reasoning; ensures privacy compliance.
# Input/Condition: Runs graph with text-only vs visual evidence against OfflineSimulationProvider and CapableStub.
# Pass condition: Appropriate vision_status set; base64 strings omitted from outputs and string representations.
# Note: Parameterized test command runs both provider configurations.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_visual_evidence.py -k "test_preparation_and_reasoning_are_separate"
@pytest.mark.parametrize("provider,status", [(OfflineSimulationProvider(), "unsupported"), (CapableStub(), "available")])
async def test_preparation_and_reasoning_are_separate(provider, status):
    images = request([item()]).visual_evidence
    prepared = prepare_visual_evidence({"visual_evidence": images}, provider)
    assert prepared == {"vision_status": status}
    graph = create_problem_understanding_graph(provider)
    initial = {"description": "Water pipe burst in the kitchen causing flooding."}
    plain = await graph.ainvoke(initial)
    visual = await graph.ainvoke({**initial, "visual_evidence": images})
    assert plain["vision_status"] == "not_requested"
    assert visual["vision_status"] == status
    assert {k: v for k, v in plain["output"].items() if k not in {
        "visionStatus", "attachmentIdsUsed", "visualObservations", "visualLimitations"}} == {
        k: v for k, v in visual["output"].items() if k not in {
        "visionStatus", "attachmentIdsUsed", "visualObservations", "visualLimitations"}}
    assert visual["output"]["visionStatus"] == ("used" if provider.supports_images else "unsupported")
    assert visual["output"]["visualObservations"] == []
    assert plain["prompt"] == visual["prompt"]
    assert [t["tool"] for t in visual["tool_executions"]] == [
        "LocationExtractionTool", "ProblemClassificationTool", "ServiceKnowledgeTool"]
    assert images[0].data_base64 not in json.dumps(visual["output"])
    assert images[0].data_base64 not in repr(images[0])


# Purpose: Verifies that unvalidated/corrupted raw input to the graph preparation step safely reports vision_status='failed'.
# Why: Defensive programming prevents runtime exceptions if invalid data reaches internal graph nodes.
# Input/Condition: State containing unvalidated string in visual_evidence.
# Pass condition: prepare_visual_evidence returns {"vision_status": "failed"}.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_visual_evidence.py::test_invalid_direct_graph_input_reports_failed
def test_invalid_direct_graph_input_reports_failed():
    assert prepare_visual_evidence({"visual_evidence": ["unvalidated"]}, CapableStub()) == {"vision_status": "failed"}


# Purpose: Verifies that whitespace-only description with empty visual evidence finalizes immediately with needsMoreInformation=True.
# Why: Empty requests skip model reasoning and tools regardless of visual evidence state.
# Input/Condition: description="   ", visual_evidence=[].
# Pass condition: 0 tool executions, needsMoreInformation=True, vision_status="not_requested", prompt not created.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_visual_evidence.py::test_empty_input_still_finalizes_directly
async def test_empty_input_still_finalizes_directly():
    graph = create_problem_understanding_graph(OfflineSimulationProvider())
    result = await graph.ainvoke({"description": "   ", "visual_evidence": []})
    assert result["tool_executions"] == []
    assert result["output"]["needsMoreInformation"] is True
    assert result["vision_status"] == "not_requested"
    assert "prompt" not in result


# Purpose: Verifies that FastAPI /agent/execute endpoint accepts visual evidence without echoing base64 data in success or validation error responses.
# Why: Security and privacy requirement: image binary data must never appear in HTTP responses or 422 error details.
# Input/Condition: POST /agent/execute with visual evidence, and invalid width=0.
# Pass condition: Valid request returns 200 without "dataBase64" in response text; invalid returns 422 without echoing base64 bytes.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_visual_evidence.py::test_api_transports_to_graph_without_echoing_content
async def test_api_transports_to_graph_without_echoing_content(async_client, valid_plumbing_request_payload):
    payload = valid_plumbing_request_payload
    plain = (await async_client.post("/agent/execute", json=payload)).json()
    payload["input"]["visualEvidence"] = [item()]
    response = await async_client.post("/agent/execute", json=payload)
    assert response.status_code == 200
    assert response.json()["result"] == {**plain["result"], "visionStatus": "unsupported"}
    assert "dataBase64" not in response.text
    payload["input"]["visualEvidence"][0]["width"] = 0
    invalid = await async_client.post("/agent/execute", json=payload)
    assert invalid.status_code == 422
    assert "dataBase64" not in invalid.text
    assert payload["input"]["visualEvidence"][0]["dataBase64"] not in invalid.text

