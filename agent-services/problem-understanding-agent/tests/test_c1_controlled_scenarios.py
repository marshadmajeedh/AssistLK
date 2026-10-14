# Purpose: Tests the 7 controlled diagnostic test matrix scenarios (A through G) for Component 1 Problem Understanding.
# Why: Validates complete end-to-end agent behavior under controlled inputs, multimodal images, network rate limits, and server error faults.
# Real vs Mocked: Real FastAPI application mounted via ASGITransport, real LangGraph compiled workflow, and real Pydantic DTOs. Google Gemini HTTP endpoint is mocked with `respx`; no live API calls or live model keys used.
# Prerequisites: Python virtual environment with dependencies installed in agent-services/problem-understanding-agent/.venv.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_c1_controlled_scenarios.py

"""Controlled diagnostic test matrix for Phase 4 scenarios (A through G).

Verifies C1 Problem Understanding agent behavior under controlled inputs,
mocked Gemini LLM responses, and network fault conditions.
"""
import base64
from pathlib import Path
from uuid import uuid4
import httpx
import pytest
import respx
from httpx import ASGITransport, AsyncClient

from app.config import Settings, get_settings
from app.graphs.problem_graph import create_problem_understanding_graph
from app.main import app
from app.providers.base import TransientProviderError
from app.providers.gemini_provider import GeminiProvider
from app.providers.offline_provider import OfflineSimulationProvider
from app.schemas.request import AgentExecutionRequest
from app.schemas.visual_evidence import VisualEvidence

GEMINI_ENDPOINT = "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent"


@pytest.fixture
def test_settings() -> Settings:
    return Settings(
        SERVICE_NAME="problem-understanding-agent-test",
        ENVIRONMENT="test",
        LLM_PROVIDER="gemini",
        GOOGLE_API_KEY="AIzaSyTestKeyForControlledScenarios",
        GEMINI_MODEL="gemini-3.6-flash",
        ALLOW_OFFLINE_SIMULATION=False,
        INTERNAL_API_KEY=None,
        LLM_TIMEOUT_SECONDS=1.0,
        LLM_MAX_ATTEMPTS=2,
    )


@pytest.fixture
async def api_client(test_settings: Settings):
    app.dependency_overrides[get_settings] = lambda: test_settings
    transport = ASGITransport(app=app)
    async with AsyncClient(transport=transport, base_url="http://test") as client:
        yield client
    app.dependency_overrides.clear()


# ==============================================================================
# TEST A: Complete description without image
# ==============================================================================

# Purpose: Scenario A - Checks that a sufficiently detailed problem description is classified without clarification.
# Why: Customers who provide clear descriptions should immediately proceed without redundant questions.
# Input/Condition: Complete text description of leaking bathroom tap; mocked Gemini returns Plumbing.
# Pass condition: HTTP 200, success=True, category="Plumbing", needsMoreInformation=False, followUpQuestions is empty.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_c1_controlled_scenarios.py::test_scenario_a_complete_description_without_image
@pytest.mark.asyncio
@respx.mock
async def test_scenario_a_complete_description_without_image(api_client: AsyncClient):
    """
    Scenario A: Customer provides sufficiently detailed problem description.
    Expected:
    - Description reaches the agent unaltered.
    - Agent recognizes available problem details.
    - No unnecessary clarification (needsMoreInformation=False).
    - Status/result corresponds to valid Plumbing category with High/Medium urgency.
    """
    complete_description = (
        "The bathroom tap is leaking from its base even when turned off. "
        "The water is collecting around the sink. The tap is located in the upstairs bathroom. "
        "I need a plumber to inspect and repair the leak."
    )

    gemini_response_payload = {
        "candidates": [
            {
                "content": {
                    "parts": [
                        {
                            "text": (
                                '{\n'
                                '  "category": "Plumbing",\n'
                                '  "problemSummary": "Bathroom tap leaking at base with water pooling.",\n'
                                '  "urgency": "Medium",\n'
                                '  "needsMoreInformation": false,\n'
                                '  "followUpQuestions": [],\n'
                                '  "confidence": 0.88,\n'
                                '  "scope": "Repair or replace leaking bathroom tap and seal base.",\n'
                                '  "safetyAdvisories": ["Wipe up standing water to avoid slipping."]\n'
                                '}'
                            )
                        }
                    ]
                }
            }
        ]
    }

    gemini_route = respx.post(GEMINI_ENDPOINT).mock(
        return_value=httpx.Response(200, json=gemini_response_payload)
    )

    req_payload = {
        "requestId": str(uuid4()),
        "agentName": "ProblemUnderstandingAgent",
        "operation": "analyze-problem",
        "input": {
            "serviceRequestId": str(uuid4()),
            "description": complete_description,
            "locationText": "Upstairs bathroom, Colombo 05",
            "categoryHint": "Plumbing",
            "clarificationHistory": [],
            "visualEvidence": [],
        },
    }

    response = await api_client.post("/agent/execute", json=req_payload)
    assert response.status_code == 200

    data = response.json()
    assert data["success"] is True
    assert data["errorMessage"] is None

    result = data["result"]
    assert result["category"] == "Plumbing"
    assert result["needsMoreInformation"] is False
    assert len(result["followUpQuestions"]) == 0
    assert result["confidence"] >= 0.80

    # Verify customer description was in the prompt passed to Gemini
    assert gemini_route.called
    sent_request = gemini_route.calls.last.request
    sent_body = sent_request.content.decode("utf-8")
    assert "bathroom tap is leaking from its base" in sent_body


# ==============================================================================
# TEST B: Incomplete description
# ==============================================================================

# Purpose: Scenario B - Checks that an ambiguous or overly brief description triggers clarification questions.
# Why: Prevents assigning an arbitrary category to uninformative requests like "Something is broken at my house."
# Input/Condition: Incomplete description "Something is broken at my house."
# Pass condition: needsMoreInformation is True, category="Unclassified", urgency="Unknown", followUpQuestions has items.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_c1_controlled_scenarios.py::test_scenario_b_incomplete_description_triggers_clarification
@pytest.mark.asyncio
async def test_scenario_b_incomplete_description_triggers_clarification():
    """
    Scenario B: Customer provides an ambiguous/overly brief description.
    Expected:
    - Clarification is genuinely requested (needsMoreInformation=True).
    - Relevant follow-up questions are generated.
    - Category is Unclassified with Unknown urgency (no invented diagnosis).
    """
    incomplete_description = "Something is broken at my house."

    provider = OfflineSimulationProvider()
    graph = create_problem_understanding_graph(provider)

    initial_state = {
        "request_id": str(uuid4()),
        "service_request_id": str(uuid4()),
        "description": incomplete_description,
        "clarification_history": [],
        "visual_evidence": [],
    }

    final_state = await graph.ainvoke(initial_state)
    output = final_state["output"]

    assert output["needsMoreInformation"] is True
    assert output["category"] == "Unclassified"
    assert output["urgency"] == "Unknown"
    assert len(output["followUpQuestions"]) >= 1
    assert any("broken" in q.lower() or "describe" in q.lower() for q in output["followUpQuestions"])


# ==============================================================================
# TEST C: Complete description with image
# ==============================================================================

# Purpose: Scenario C - Checks that a complete description with a valid JPEG image executes multimodal vision reasoning.
# Why: Validates multimodal image integration from API request through to model output parsing.
# Input/Condition: Complete description + base64 encoded synthetic_sink_leak.jpg fixture; mocked Gemini returns visionStatus='used'.
# Pass condition: HTTP 200, success=True, visionStatus="used", needsMoreInformation=False, visualObservations contains attachmentId.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_c1_controlled_scenarios.py::test_scenario_c_complete_description_with_image
@pytest.mark.asyncio
@respx.mock
async def test_scenario_c_complete_description_with_image(api_client: AsyncClient):
    """
    Scenario C: Customer provides a complete description with a valid JPEG image.
    Expected:
    - Image received and validated correctly.
    - Image reaches multimodal reasoning.
    - Image analysis succeeds with visionStatus='used'.
    - Results mapped correctly without unnecessary clarification.
    """
    fixture_path = Path(__file__).resolve().parent.parent / "scripts" / "fixtures" / "synthetic_sink_leak.jpg"
    assert fixture_path.exists(), f"Fixture {fixture_path} not found"

    with open(fixture_path, "rb") as f:
        img_bytes = f.read()
    b64_img = base64.b64encode(img_bytes).decode("ascii")

    att_id = str(uuid4())
    multimodal_gemini_payload = {
        "candidates": [
            {
                "content": {
                    "parts": [
                        {
                            "text": (
                                '{\n'
                                '  "category": "Plumbing",\n'
                                '  "problemSummary": "Kitchen sink drain connection leaking into cabinet.",\n'
                                '  "urgency": "Medium",\n'
                                '  "needsMoreInformation": false,\n'
                                '  "followUpQuestions": [],\n'
                                '  "confidence": 0.85,\n'
                                '  "scope": "Inspect P-trap and tighten drain flange connection.",\n'
                                '  "visionStatus": "used",\n'
                                f'  "attachmentIdsUsed": ["{att_id}"],\n'
                                '  "visualObservations": [\n'
                                f'    {{"attachmentId": "{att_id}", "observation": "Moisture visible at PVC pipe joint."}}\n'
                                '  ],\n'
                                '  "visualLimitations": [],\n'
                                '  "visualAmbiguityResolved": true,\n'
                                '  "textImageConflict": false\n'
                                '}'
                            )
                        }
                    ]
                }
            }
        ]
    }

    gemini_route = respx.post(GEMINI_ENDPOINT).mock(
        return_value=httpx.Response(200, json=multimodal_gemini_payload)
    )

    req_payload = {
        "requestId": str(uuid4()),
        "agentName": "ProblemUnderstandingAgent",
        "operation": "analyze-problem",
        "input": {
            "serviceRequestId": str(uuid4()),
            "description": "The kitchen sink pipe underneath the basin is leaking continuously.",
            "locationText": "Colombo 07",
            "categoryHint": "Plumbing",
            "clarificationHistory": [],
            "visualEvidence": [
                {
                    "attachmentId": att_id,
                    "contentType": "image/jpeg",
                    "dataBase64": b64_img,
                    "width": 320,
                    "height": 240,
                }
            ],
        },
    }

    response = await api_client.post("/agent/execute", json=req_payload)
    assert response.status_code == 200

    data = response.json()
    assert data["success"] is True
    assert data["errorMessage"] is None

    result = data["result"]
    assert result["category"] == "Plumbing"
    assert result["visionStatus"] == "used"
    assert result["needsMoreInformation"] is False
    assert len(result["visualObservations"]) == 1
    assert result["visualObservations"][0]["attachmentId"] == att_id

    # Verify Gemini received image inlineData part
    sent_request = gemini_route.calls.last.request
    sent_json = httpx.Response(200, content=sent_request.content).json()
    parts = sent_json["contents"][0]["parts"]
    image_parts = [p for p in parts if "inline_data" in p]
    assert len(image_parts) == 1
    assert image_parts[0]["inline_data"]["mime_type"] == "image/jpeg"


# ==============================================================================
# TEST D: Gemini HTTP 429 Rate Limit
# ==============================================================================

# Purpose: Scenario D - Checks that an HTTP 429 rate limit triggers bounded retry and propagates a transient error without fabricating AI results.
# Why: Prevents the system from inventing fake diagnoses when upstream LLM capacity is exhausted.
# Input/Condition: Gemini API returns HTTP 429 Too Many Requests on all attempts.
# Pass condition: HTTP 200 response envelope with success=False, result=None, errorMessage containing "Transient provider error" and "429", exactly 2 calls made.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_c1_controlled_scenarios.py::test_scenario_d_gemini_http_429_rate_limit
@pytest.mark.asyncio
@respx.mock
async def test_scenario_d_gemini_http_429_rate_limit(api_client: AsyncClient):
    """
    Scenario D: Gemini returns HTTP 429 Too Many Requests (Rate limit / Quota).
    Expected:
    - Retries respect bounded retry configuration.
    - Classifies as TransientProviderError with HTTP 429.
    - Does NOT fabricate an AI result with needsMoreInformation=True.
    - Response has success=False, result=None, errorMessage containing HTTP 429.
    """
    gemini_route = respx.post(GEMINI_ENDPOINT).mock(
        return_value=httpx.Response(429, json={"error": {"code": 429, "message": "Resource exhausted"}})
    )

    req_payload = {
        "requestId": str(uuid4()),
        "agentName": "ProblemUnderstandingAgent",
        "operation": "analyze-problem",
        "input": {
            "serviceRequestId": str(uuid4()),
            "description": "The kitchen sink pipe is leaking continuously.",
            "clarificationHistory": [],
            "visualEvidence": [],
        },
    }

    response = await api_client.post("/agent/execute", json=req_payload)
    assert response.status_code == 200

    data = response.json()
    assert data["success"] is False
    assert data["result"] is None
    assert "Transient provider error" in data["errorMessage"]
    assert "429" in data["errorMessage"]

    # Verify attempts were bounded (2 attempts as configured in test_settings)
    assert gemini_route.call_count == 2


# ==============================================================================
# TEST E: Gemini HTTP 503 Service Unavailable
# ==============================================================================

# Purpose: Scenario E - Checks that an HTTP 503 Service Unavailable from the LLM triggers bounded retries and reports a transient failure.
# Why: Guarantees transient outage classification so backend recovery mechanisms can reschedule or retry.
# Input/Condition: Gemini API returns HTTP 503 on all attempts.
# Pass condition: HTTP 200 response envelope with success=False, result=None, errorMessage containing "Transient provider error" and "503", exactly 2 calls made.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_c1_controlled_scenarios.py::test_scenario_e_gemini_http_503_service_unavailable
@pytest.mark.asyncio
@respx.mock
async def test_scenario_e_gemini_http_503_service_unavailable(api_client: AsyncClient):
    """
    Scenario E: Gemini returns HTTP 503 Service Unavailable (e.g. high demand).
    Expected:
    - Retries respect bounded retry configuration.
    - Classifies as TransientProviderError with HTTP 503.
    - Does NOT fabricate a successful result or false clarification.
    - Error is propagated to caller as success=False, result=None.
    """
    gemini_route = respx.post(GEMINI_ENDPOINT).mock(
        return_value=httpx.Response(503, json={"error": {"code": 503, "message": "Model is experiencing high demand"}})
    )

    req_payload = {
        "requestId": str(uuid4()),
        "agentName": "ProblemUnderstandingAgent",
        "operation": "analyze-problem",
        "input": {
            "serviceRequestId": str(uuid4()),
            "description": "The kitchen sink pipe is leaking continuously.",
            "clarificationHistory": [],
            "visualEvidence": [],
        },
    }

    response = await api_client.post("/agent/execute", json=req_payload)
    assert response.status_code == 200

    data = response.json()
    assert data["success"] is False
    assert data["result"] is None
    assert "Transient provider error" in data["errorMessage"]
    assert "503" in data["errorMessage"]

    assert gemini_route.call_count == 2


# ==============================================================================
# TEST F: Gemini successful response mapping
# ==============================================================================

# Purpose: Scenario F - Checks that a valid structured Gemini response is accurately deserialized and audit metadata recorded.
# Why: Verifies compliance of output fields, metadata execution times, and tool audits with the platform contract.
# Input/Condition: Valid Electrical breaker problem description; mocked Gemini returns structured Electrical result.
# Pass condition: success=True, category="Electrical", urgency="High", confidence=0.92, 3 tool executions audited in metadata.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_c1_controlled_scenarios.py::test_scenario_f_gemini_successful_structured_response
@pytest.mark.asyncio
@respx.mock
async def test_scenario_f_gemini_successful_structured_response(api_client: AsyncClient):
    """
    Scenario F: Valid structured Gemini response is parsed and mapped.
    Expected:
    - Correct deserialization of problem fields (category, urgency, confidence, scope).
    - Metadata records duration, agentName, and tool executions.
    - Response conforms exactly to AgentExecutionResponse schema.
    """
    gemini_body = {
        "candidates": [
            {
                "content": {
                    "parts": [
                        {
                            "text": (
                                '{\n'
                                '  "category": "Electrical",\n'
                                '  "problemSummary": "Main circuit breaker tripping intermittently.",\n'
                                '  "urgency": "High",\n'
                                '  "needsMoreInformation": false,\n'
                                '  "followUpQuestions": [],\n'
                                '  "confidence": 0.92,\n'
                                '  "scope": "Inspect distribution board and test circuit loads.",\n'
                                '  "safetyAdvisories": ["Do not force breaker switch if it resists."]\n'
                                '}'
                            )
                        }
                    ]
                }
            }
        ]
    }

    respx.post(GEMINI_ENDPOINT).mock(return_value=httpx.Response(200, json=gemini_body))

    req_payload = {
        "requestId": str(uuid4()),
        "agentName": "ProblemUnderstandingAgent",
        "operation": "analyze-problem",
        "input": {
            "serviceRequestId": str(uuid4()),
            "description": "Main circuit breaker trips whenever the oven is turned on.",
            "locationText": "Nugegoda",
            "categoryHint": "Electrical",
            "clarificationHistory": [],
            "visualEvidence": [],
        },
    }

    response = await api_client.post("/agent/execute", json=req_payload)
    assert response.status_code == 200

    data = response.json()
    assert data["success"] is True
    assert data["result"]["category"] == "Electrical"
    assert data["result"]["urgency"] == "High"
    assert data["result"]["confidence"] == 0.92
    assert data["metadata"]["agentName"] == "ProblemUnderstandingAgent"
    assert len(data["metadata"]["toolExecutions"]) == 3


# ==============================================================================
# TEST G: Existing clarification followed by customer response
# ==============================================================================

# Purpose: Scenario G - Checks that answering a clarification question allows the agent to reach high confidence without looping.
# Why: Prevents infinite clarification loops; once the customer provides the needed detail, needsMoreInformation must become False.
# Input/Condition: Brief text + round 1 clarification answer describing exact pipe joint leak; mocked Gemini returns resolved Plumbing.
# Pass condition: success=True, needsMoreInformation=False, category="Plumbing", and clarification answer is verified in outgoing prompt.
# Run from repository root: & agent-services/problem-understanding-agent/.venv/Scripts/python.exe -m pytest agent-services/problem-understanding-agent/tests/test_c1_controlled_scenarios.py::test_scenario_g_clarification_history_reevaluation
@pytest.mark.asyncio
@respx.mock
async def test_scenario_g_clarification_history_reevaluation(api_client: AsyncClient):
    """
    Scenario G: Previous clarification round exists, and customer has now answered.
    Expected:
    - Customer answer from clarification round 1 is incorporated into evidence.
    - Agent evaluates sufficiency with the additional information.
    - Request transitions to needsMoreInformation=False (not stuck in clarification loop).
    """
    gemini_body = {
        "candidates": [
            {
                "content": {
                    "parts": [
                        {
                            "text": (
                                '{\n'
                                '  "category": "Plumbing",\n'
                                '  "problemSummary": "Under-sink kitchen drain pipe connection leak.",\n'
                                '  "urgency": "Medium",\n'
                                '  "needsMoreInformation": false,\n'
                                '  "followUpQuestions": [],\n'
                                '  "confidence": 0.85,\n'
                                '  "scope": "Repair connection joint between sink and drain pipe.",\n'
                                '  "safetyAdvisories": []\n'
                                '}'
                            )
                        }
                    ]
                }
            }
        ]
    }

    gemini_route = respx.post(GEMINI_ENDPOINT).mock(return_value=httpx.Response(200, json=gemini_body))

    req_payload = {
        "requestId": str(uuid4()),
        "agentName": "ProblemUnderstandingAgent",
        "operation": "analyze-problem",
        "input": {
            "serviceRequestId": str(uuid4()),
            "description": "Kitchen pipe leaking.",
            "locationText": "Colombo 03",
            "clarificationHistory": [
                {
                    "round": 1,
                    "question": "Where exactly is the pipe leaking and is water pooling?",
                    "answer": "The leak is right under the basin at the drain pipe joint, water collecting in cabinet.",
                }
            ],
            "visualEvidence": [],
        },
    }

    response = await api_client.post("/agent/execute", json=req_payload)
    assert response.status_code == 200

    data = response.json()
    assert data["success"] is True
    assert data["result"]["needsMoreInformation"] is False
    assert data["result"]["category"] == "Plumbing"

    # Confirm clarification history was passed to the LLM
    sent_request = gemini_route.calls.last.request
    sent_body = sent_request.content.decode("utf-8")
    assert "Round 1:" in sent_body
    assert "right under the basin at the drain pipe joint" in sent_body

