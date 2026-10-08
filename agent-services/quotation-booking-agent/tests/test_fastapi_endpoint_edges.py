"""
HTTP edge-case tests for the FastAPI endpoints.

Maps to SE3110 AI sub-items:
  - task-completion testing
  - business-rule compliance at the HTTP boundary
"""

import asyncio

import httpx
import pytest

from app.main import app


def _payload(quotation_id=99):
    return {
        "quotation_id": quotation_id,
        "service_request_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "provider_id": "e5f340e6-fd9c-4d49-8da5-78e858bce614",
        "items": [{"description": "Repair", "amount": 60, "quantity": 2}],
    }


async def _client():
    transport = httpx.ASGITransport(app=app)
    return httpx.AsyncClient(transport=transport, base_url="http://test")


# ------------------------------------------------------------------
# TC-C3-AI-21 — /health returns 200 and healthy
# ------------------------------------------------------------------
def test_health_endpoint():
    async def run():
        async with await _client() as client:
            r = await client.get("/health")
            assert r.status_code == 200
            assert r.json()["status"] == "healthy"

    asyncio.run(run())


# ------------------------------------------------------------------
# TC-C3-AI-22 — /workflows/resume with unknown thread returns 404
# ------------------------------------------------------------------
def test_resume_unknown_thread_returns_404(monkeypatch):
    monkeypatch.delenv("INTERNAL_API_KEY", raising=False)

    async def run():
        async with await _client() as client:
            r = await client.post(
                "/workflows/resume",
                json={"thread_id": "missing-thread", "decision": "approve"},
            )
            assert r.status_code == 404

    asyncio.run(run())


# ------------------------------------------------------------------
# TC-C3-AI-23 — /workflows/resume with invalid decision returns 400
# ------------------------------------------------------------------
def test_resume_invalid_decision_returns_400(monkeypatch):
    monkeypatch.delenv("INTERNAL_API_KEY", raising=False)

    async def run():
        async with await _client() as client:
            started = await client.post("/workflows/start", json=_payload())
            thread_id = started.json()["thread_id"]

            r = await client.post(
                "/workflows/resume",
                json={"thread_id": thread_id, "decision": "auto_approve"},
            )
            assert r.status_code == 400
            assert "approve" in r.json()["detail"] or "reject" in r.json()["detail"]

    asyncio.run(run())


# ------------------------------------------------------------------
# TC-C3-AI-24 — Missing X-Internal-Api-Key returns 401
# ------------------------------------------------------------------
def test_missing_internal_key_returns_401(monkeypatch):
    monkeypatch.setenv("INTERNAL_API_KEY", "secret-key")

    async def run():
        async with await _client() as client:
            r = await client.post("/workflows/start", json=_payload())
            assert r.status_code == 401

    asyncio.run(run())


# ------------------------------------------------------------------
# TC-C3-AI-25 — Wrong X-Internal-Api-Key returns 401
# ------------------------------------------------------------------
def test_wrong_internal_key_returns_401(monkeypatch):
    monkeypatch.setenv("INTERNAL_API_KEY", "secret-key")

    async def run():
        async with await _client() as client:
            r = await client.post(
                "/workflows/start",
                json=_payload(),
                headers={"X-Internal-Api-Key": "wrong-key"},
            )
            assert r.status_code == 401

    asyncio.run(run())


# ------------------------------------------------------------------
# TC-C3-AI-26 — Correct X-Internal-Api-Key returns 200
# ------------------------------------------------------------------
def test_correct_internal_key_returns_200(monkeypatch):
    monkeypatch.setenv("INTERNAL_API_KEY", "secret-key")

    async def run():
        async with await _client() as client:
            r = await client.post(
                "/workflows/start",
                json=_payload(),
                headers={"X-Internal-Api-Key": "secret-key"},
            )
            assert r.status_code == 200
            assert r.json()["status"] == "waiting_for_approval"

    asyncio.run(run())