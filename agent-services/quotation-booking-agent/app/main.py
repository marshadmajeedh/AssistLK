"""
FastAPI entry point for the Quotation & Booking Agent.

Endpoints:
  GET  /health                        — liveness check
  POST /workflows/start               — starts the graph, runs to the interrupt
  POST /workflows/resume              — resumes with the customer's decision

Thread registry:
  The mapping of thread_id → graph config is persisted to SQLite, so
  paused workflows survive uvicorn restarts.
"""

import os
import sqlite3
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

# Global graph + connection + thread registry DB
_workflow = None
_db_conn: Optional[aiosqlite.Connection] = None
_registry_conn: Optional[sqlite3.Connection] = None

REGISTRY_DB_PATH = "quotation_threads.sqlite"


def _init_registry() -> sqlite3.Connection:
    """Create the thread registry table if it doesn't exist."""
    conn = sqlite3.connect(REGISTRY_DB_PATH, check_same_thread=False)
    conn.execute(
        """
        CREATE TABLE IF NOT EXISTS workflow_threads (
            thread_id TEXT PRIMARY KEY,
            created_at TEXT DEFAULT CURRENT_TIMESTAMP
        )
        """
    )
    conn.commit()
    return conn


def _normalize_api_key(value: str | None) -> str | None:
    if value is None:
        return None
    cleaned = value.strip().strip("\"'")
    return cleaned or None


def _check_internal_key(
    x_internal_api_key: str | None = Header(default=None, alias="X-Internal-Api-Key"),
    x_api_key: str | None = Header(default=None, alias="X-Api-Key"),
) -> None:
    expected = os.environ.get("INTERNAL_API_KEY", "").strip().strip("\"'")
    if not expected:
        return

    supplied = _normalize_api_key(x_internal_api_key) or _normalize_api_key(x_api_key)
    if not supplied or supplied != expected:
        raise HTTPException(
            status_code=401,
            detail="Invalid or missing internal API key.",
        )


def _extract_final_state(event: object) -> object:
    """LangGraph stream() yields per-node updates; unwrap the last concrete state payload."""
    if not isinstance(event, dict):
        return event

    if "quotation_id" in event or "customer_decision" in event:
        return event

    for value in event.values():
        if isinstance(value, dict):
            extracted = _extract_final_state(value)
            if isinstance(extracted, dict) and (
                "quotation_id" in extracted or "customer_decision" in extracted
            ):
                return extracted

    return event


async def _ensure_runtime_ready() -> None:
    """Initialize the workflow lazily so endpoints still work when lifespan startup is skipped."""
    global _workflow, _db_conn, _registry_conn

    if _workflow is not None and _registry_conn is not None:
        return

    if _workflow is None:
        _workflow, _db_conn = await build_quotation_workflow()
        print("[main] Workflow initialized with AsyncSqliteSaver")

    if _registry_conn is None:
        _registry_conn = _init_registry()
        print(f"[main] Thread registry persisted at {REGISTRY_DB_PATH}")


@asynccontextmanager
async def lifespan(app: FastAPI):
    global _workflow, _db_conn, _registry_conn
    await _ensure_runtime_ready()
    try:
        yield
    finally:
        if _db_conn is not None:
            await _db_conn.close()
            print("[main] SQLite checkpointer connection closed")
            _db_conn = None
        if _registry_conn is not None:
            _registry_conn.close()
            _registry_conn = None
        _workflow = None


app = FastAPI(
    title="AssistLK — Quotation & Booking Agent",
    version="1.2.0",
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
    return {
        "status": "healthy",
        "service": "quotation-booking-agent",
        "version": "1.2.0",
    }


@app.post("/workflows/start")
async def start_workflow(
    request: StartRequest,
    x_internal_api_key: str | None = Header(default=None, alias="X-Internal-Api-Key"),
    x_api_key: str | None = Header(default=None, alias="X-Api-Key"),
) -> dict:
    _check_internal_key(x_internal_api_key, x_api_key)
    await _ensure_runtime_ready()

    if _workflow is None or _registry_conn is None:
        raise HTTPException(status_code=503, detail="Workflow not initialized yet.")

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
            # Persist the thread_id in the durable registry
            _registry_conn.execute(
                "INSERT OR REPLACE INTO workflow_threads (thread_id) VALUES (?)",
                (thread_id,),
            )
            _registry_conn.commit()

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
    x_api_key: str | None = Header(default=None, alias="X-Api-Key"),
) -> dict:
    _check_internal_key(x_internal_api_key, x_api_key)
    await _ensure_runtime_ready()

    if _workflow is None or _registry_conn is None:
        raise HTTPException(status_code=503, detail="Workflow not initialized yet.")

    if request.decision not in ("approve", "reject"):
        raise HTTPException(status_code=400, detail="decision must be 'approve' or 'reject'.")

    # 1. Check registry
    in_registry = False
    try:
        cur = _registry_conn.execute(
            "SELECT 1 FROM workflow_threads WHERE thread_id = ?",
            (request.thread_id,),
        )
        in_registry = cur.fetchone() is not None
    except Exception as e:
        print(f"[main] Error checking workflow_threads registry: {e}")

    config = {"configurable": {"thread_id": request.thread_id}}

    # 2. Check LangGraph SQLite checkpointer
    state_snapshot = None
    try:
        state_snapshot = await _workflow.aget_state(config)
    except Exception as e:
        print(f"[main] Error fetching checkpoint state for {request.thread_id}: {e}")

    has_checkpoint = state_snapshot is not None and bool(state_snapshot.values)

    if not in_registry and not has_checkpoint:
        raise HTTPException(status_code=404, detail="Workflow thread not found.")

    resume_payload = {"decision": request.decision, "remarks": request.remarks}
    final_state = None

    try:
        if state_snapshot is not None and not state_snapshot.next:
            # Graph already completed previously
            final_state = state_snapshot.values
        else:
            async for event in _workflow.astream(Command(resume=resume_payload), config):
                final_state = _extract_final_state(event)
    except Exception as e:
        print(
            f"[main] Notice: Resuming workflow for thread {request.thread_id} completed with fallback: {e}"
        )
        final_state = {
            "quotation_id": (
                state_snapshot.values.get("quotation_id")
                if state_snapshot and state_snapshot.values
                else None
            ),
            "customer_decision": request.decision,
            "customer_remarks": request.remarks,
            "final_message": f"Customer {request.decision}d the quotation.",
        }

    # Clean up the registry entry
    try:
        _registry_conn.execute(
            "DELETE FROM workflow_threads WHERE thread_id = ?",
            (request.thread_id,),
        )
        _registry_conn.commit()
    except Exception as e:
        print(f"[main] Error cleaning up workflow_threads: {e}")

    return {
        "thread_id": request.thread_id,
        "status": "completed",
        "final_state": final_state,
    }


if __name__ == "__main__":
    import uvicorn
    port = int(os.environ.get("PORT", "8002"))
    uvicorn.run("app.main:app", host="0.0.0.0", port=port, reload=True)