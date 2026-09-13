"""Synthetic transport fixtures only; no photographs, decoders or live providers."""
import base64
import json
from uuid import uuid4

import pytest
from pydantic import ValidationError
from app.graphs.problem_graph import create_problem_understanding_graph
from app.graphs.visual_evidence import prepare_visual_evidence
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


@pytest.mark.parametrize("count", [0, 1, 3])
def test_bounded_items_accepted(count):
    parsed = request([item() for _ in range(count)])
    assert len(parsed.visual_evidence) == count
    assert all(x.decoded_size == 12 for x in parsed.visual_evidence)


def test_omitted_images_are_backward_compatible():
    assert ProblemUnderstandingInputDto(serviceRequestId=uuid4(), description="Leaking pipe").visual_evidence == []


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


def test_fourth_rejected():
    with pytest.raises(ValidationError):
        request([item() for _ in range(4)])


def test_duplicate_identity_rejected():
    image = item()
    with pytest.raises(ValidationError):
        request([image, image])


@pytest.mark.parametrize("size", [MAX_IMAGE_BYTES + 1, MAX_IMAGE_BYTES + 3])
def test_decoded_size_limit_not_just_encoded_length(size):
    with pytest.raises(ValidationError):
        request([item(size)])


def test_per_image_and_aggregate_boundaries():
    assert sum(x.decoded_size for x in request([item(MAX_IMAGE_BYTES), item(MAX_IMAGE_BYTES)]).visual_evidence) == MAX_TOTAL_BYTES
    with pytest.raises(ValidationError):
        request([item(MAX_IMAGE_BYTES), item(MAX_IMAGE_BYTES), item(1)])


class CapableStub(OfflineSimulationProvider):
    @property
    def supports_images(self):
        return True


@pytest.mark.parametrize("provider", [OfflineSimulationProvider(), GeminiProvider("fake"), OpenAIProvider("fake")])
def test_current_adapters_are_not_vision_ready(provider):
    assert provider.supports_images is False


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
    assert plain["output"] == visual["output"]  # Offline cannot invent image findings.
    assert plain["prompt"] == visual["prompt"]
    assert [t["tool"] for t in visual["tool_executions"]] == [
        "LocationExtractionTool", "ProblemClassificationTool", "ServiceKnowledgeTool"]
    assert images[0].data_base64 not in json.dumps(visual["output"])
    assert images[0].data_base64 not in repr(images[0])


def test_invalid_direct_graph_input_reports_failed():
    assert prepare_visual_evidence({"visual_evidence": ["unvalidated"]}, CapableStub()) == {"vision_status": "failed"}


async def test_empty_input_still_finalizes_directly():
    graph = create_problem_understanding_graph(OfflineSimulationProvider())
    result = await graph.ainvoke({"description": "   ", "visual_evidence": []})
    assert result["tool_executions"] == []
    assert result["output"]["needsMoreInformation"] is True
    assert result["vision_status"] == "not_requested"
    assert "prompt" not in result


async def test_api_transports_to_graph_without_echoing_content(async_client, valid_plumbing_request_payload):
    payload = valid_plumbing_request_payload
    plain = (await async_client.post("/agent/execute", json=payload)).json()
    payload["input"]["visualEvidence"] = [item()]
    response = await async_client.post("/agent/execute", json=payload)
    assert response.status_code == 200
    assert response.json()["result"] == plain["result"]
    assert "dataBase64" not in response.text
    payload["input"]["visualEvidence"][0]["width"] = 0
    invalid = await async_client.post("/agent/execute", json=payload)
    assert invalid.status_code == 422
    assert "dataBase64" not in invalid.text
    assert payload["input"]["visualEvidence"][0]["dataBase64"] not in invalid.text
