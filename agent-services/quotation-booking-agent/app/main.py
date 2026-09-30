"""
FastAPI entry point for the Quotation & Booking Agent.

Endpoints:
  GET  /health                        — liveness check
  POST /workflows/start               — starts the graph, runs to the interrupt
  POST /workflows/resume              — resumes with the customer's decision

Security:
  If the INTERNAL_API_KEY environment variable is set, every request to
  /workflows/* must include an X-Internal-Api-Key header matching it.
  For local development, leave INTERNAL_API_KEY empty and the check is
  skipped (no authentication).
"""

import os
import uuid
from contextlib import asynccontextmanager
from typing import Optional
from uuid import UUID

import aiosqlite
from dotenv import load_dotenv
from fastapi import FastAPI, HTTPException, Header
from pydantic import BaseModel
from langgraph.types import Command

from .agent import build_quotation_workflow

load_dotenv()

# Global graph + connection — initialized once at startup
_workflow = None
_db_conn: Optional[aiosqlite.Connection] = None

# In-memory thread registry (MVP). Migrate to Redis/PostgreSQL for scale.
_WORKFLOWS: dict[str, dict] = {}


def _check_internal_key(x_internal_api_key: str | None) -> None:
    """Validate the X-Internal-Api-Key header when INTERNAL_API_KEY is set."""
    expected = os.environ.get("INTERNAL_API_KEY", "")
    if expected and x_internal_api_key != expected:
        raise HTTPException(
            status_code=401,
            detail="Invalid or missing internal API key.",
        )


async def _ensure_workflow_initialized() -> None:
    """Initialize the workflow lazily when the app is created without lifespan."""
    global _workflow, _db_conn
    if _workflow is not None:
        return
    _workflow, _db_conn = await build_quotation_workflow()


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Initialize the workflow (AsyncSqliteSaver) at startup."""
    global _workflow, _db_conn
    _workflow, _db_conn = await build_quotation_workflow()
    print("[main] Workflow initialized with AsyncSqliteSaver")
    try:
        yield
    finally:
        if _db_conn is not None:
            await _db_conn.close()
            print("[main] SQLite connection closed")


app = FastAPI(
    title="AssistLK — Quotation & Booking Agent",
    version="1.1.0",
    description="Component 3 Agentic AI: validate → human approval → booking.",
    lifespan=lifespan,
)


class StartRequest(BaseModel):
    quotation_id: int
    service_request_id: Optional[UUID] = None
    provider_id: Optional[UUID] = None
    items: list[dict] = []
    notes: Optional[str] = None


class ResumeRequest(BaseModel):
    thread_id: str
    decision: str
    remarks: Optional[str] = None


@app.get("/health")
async def health() -> dict:
    return {"status": "healthy", "service": "quotation-booking-agent", "version": "1.1.0"}


@app.post("/workflows/start")
async def start_workflow(
    request: StartRequest,
    x_internal_api_key: str | None = Header(default=None, alias="X-Internal-Api-Key"),
) -> dict:
    _check_internal_key(x_internal_api_key)
    await _ensure_workflow_initialized()

    thread_id = str(uuid.uuid4())
    config = {"configurable": {"thread_id": thread_id}}

    initial_state = {
        "quotation_id": request.quotation_id,
        "service_request_id": str(request.service_request_id) if request.service_request_id else None,
        "provider_id": str(request.provider_id) if request.provider_id else None,
        "items": request.items,
        "notes": request.notes,
    }

    async for event in _workflow.astream(initial_state, config):
        if "__interrupt__" in event:
            _WORKFLOWS[thread_id] = config
            payload = event["__interrupt__"][0].value
            return {
                "thread_id": thread_id,
                "status": "waiting_for_approval",
                "approval_request": payload,
            }

    return {"thread_id": thread_id, "status": "completed_without_interrupt"}


@app.post("/workflows/resume")
async def resume_workflow(
    request: ResumeRequest,
    x_internal_api_key: str | None = Header(default=None, alias="X-Internal-Api-Key"),
) -> dict:
    _check_internal_key(x_internal_api_key)
    await _ensure_workflow_initialized()

    config = _WORKFLOWS.get(request.thread_id)
    if not config:
        raise HTTPException(status_code=404, detail="Workflow thread not found.")

    if request.decision not in ("approve", "reject"):
        raise HTTPException(status_code=400, detail="decision must be 'approve' or 'reject'.")

    resume_payload = {"decision": request.decision, "remarks": request.remarks}

    final_state = None
    async for event in _workflow.astream(Command(resume=resume_payload), config):
        final_state = event

    state_snapshot = await _workflow.aget_state(config)
    if state_snapshot is not None:
        final_state = state_snapshot.values

    del _WORKFLOWS[request.thread_id]

    return {
        "thread_id": request.thread_id,
        "status": "completed",
        "final_state": final_state,
    }


if __name__ == "__main__":
    import uvicorn
    port = int(os.environ.get("PORT", "8002"))
    uvicorn.run("app.main:app", host="0.0.0.0", port=port, reload=True)