"""
Shared pytest fixtures for the Quotation & Booking Agent.

All fixtures are deterministic:
  - No real LLM is called.
  - No real .NET backend is called.
  - No database is touched.
"""

import asyncio
import json
from typing import Any

import pytest


# ------------------------------------------------------------------
# Event loop — one loop per test session so LangGraph locks and
# aiosqlite connections stay on the same loop across tests.
# ------------------------------------------------------------------
@pytest.fixture(scope="session")
def event_loop():
    loop = asyncio.new_event_loop()
    yield loop
    loop.close()


# ------------------------------------------------------------------
# Environment isolation
# ------------------------------------------------------------------
@pytest.fixture(autouse=True)
def clean_agent_env(monkeypatch):
    for key in (
        "GOOGLE_API_KEY",
        "OPENAI_API_KEY",
        "GEMINI_MODEL",
        "OPENAI_MODEL",
        "INTERNAL_API_KEY",
        "SERVICE_BEARER_TOKEN",
        "BACKEND_API_URL",
    ):
        monkeypatch.delenv(key, raising=False)
    yield


# ------------------------------------------------------------------
# Replace the SQLite checkpointer with an in-memory one for tests.
#
# This prevents LangGraph's AsyncSqliteSaver from binding asyncio locks
# and an aiosqlite connection to the first event loop that touches it,
# which otherwise breaks tests that call asyncio.run() repeatedly.
# ------------------------------------------------------------------
@pytest.fixture(autouse=True)
def patch_checkpointer(monkeypatch):
    import app.agent as agent_module
    import app.main as main_module
    from langgraph.checkpoint.memory import InMemorySaver
    from langgraph.graph import StateGraph, START, END
    from app.state import QuotationState

    async def _build_with_memory():
        builder = StateGraph(QuotationState)
        builder.add_node("validate", agent_module.validate_node)
        builder.add_node("analyze_quotation", agent_module.analyze_quotation_node)
        builder.add_node("approval_gate", agent_module.approval_gate_node)
        builder.add_node("finalize", agent_module.finalize_node)

        builder.add_edge(START, "validate")
        builder.add_conditional_edges(
            "validate",
            agent_module._route_after_validation,
            {"analyze_quotation": "analyze_quotation", "end": END},
        )
        builder.add_edge("analyze_quotation", "approval_gate")
        builder.add_edge("approval_gate", "finalize")
        builder.add_edge("finalize", END)

        graph = builder.compile(checkpointer=InMemorySaver())

        class _NullConn:
            async def close(self):
                pass

        return graph, _NullConn()

    monkeypatch.setattr(
        agent_module,
        "build_quotation_workflow",
        _build_with_memory,
    )

    # Force the FastAPI app to rebuild the workflow lazily on the next
    # request. This ensures the memory-backed graph is used by /workflows/*.
    main_module._workflow = None
    main_module._db_conn = None

    yield


# ------------------------------------------------------------------
# Fake LLM
# ------------------------------------------------------------------
class _FakeResponse:
    def __init__(self, content: str, prompt_tokens: int = 100, completion_tokens: int = 50):
        self.content = content
        self.response_metadata = {
            "token_usage": {
                "prompt_tokens": prompt_tokens,
                "completion_tokens": completion_tokens,
                "total_tokens": prompt_tokens + completion_tokens,
            }
        }


class _FakeChatModel:
    def __init__(self, response_text: str, model_name: str = "fake-gemini"):
        self._response_text = response_text
        self.model = model_name

    async def ainvoke(self, messages: Any):
        return _FakeResponse(self._response_text)


class _RaisingChatModel:
    def __init__(self, exc: Exception, model_name: str = "raising-gemini"):
        self._exc = exc
        self.model = model_name

    async def ainvoke(self, messages: Any):
        raise self._exc


VALID_RISK_JSON = json.dumps(
    {
        "risk_level": "low",
        "confidence": 0.87,
        "rationale": "Pricing is consistent with the category median.",
        "suggested_concerns": ["Price slightly above median"],
        "recommendation": "approve",
    }
)

HIGH_RISK_JSON = json.dumps(
    {
        "risk_level": "high",
        "confidence": 0.91,
        "rationale": "Total is well above the market range for this category.",
        "suggested_concerns": [
            "Total is 3x the median",
            "No itemized breakdown of labour",
        ],
        "recommendation": "reject",
    }
)


@pytest.fixture
def valid_risk_model():
    return _FakeChatModel(VALID_RISK_JSON)


@pytest.fixture
def high_risk_model():
    return _FakeChatModel(HIGH_RISK_JSON)


@pytest.fixture
def malformed_json_model():
    return _FakeChatModel("this is not valid JSON at all")


@pytest.fixture
def fenced_json_model():
    return _FakeChatModel("```json\n" + VALID_RISK_JSON + "\n```")


@pytest.fixture
def raising_model():
    return _RaisingChatModel(RuntimeError("simulated LLM outage"))


@pytest.fixture
def timeout_model():
    return _RaisingChatModel(TimeoutError("simulated LLM timeout"))


@pytest.fixture
def patch_chat_model(monkeypatch):
    def _patch(model):
        import app.agent as agent_module
        monkeypatch.setattr(agent_module, "get_chat_model", lambda: model)
        return model

    return _patch


@pytest.fixture
def patch_get_quotation(monkeypatch):
    def _patch(return_value=None, raise_exc=None):
        import app.agent as agent_module

        async def _fake(quotation_id: int):
            if raise_exc:
                raise raise_exc
            return return_value or {"totalAmount": 100.0}

        monkeypatch.setattr(agent_module, "get_quotation", _fake)

    return _patch