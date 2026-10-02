# Quotation & Booking Agent — Component 3

Agentic AI service for AssistLK's **Quotation & Booking Management** (Component 3).
Implements the mandatory **human-in-the-loop approval** at `WAITING_FOR_CUSTOMER_APPROVAL`.

## Architecture

```
Customer / Provider -> ASP.NET Core -> This service (FastAPI + LangGraph)
  decision     <- ASP.NET validates and applies booking/rejection
```

ASP.NET owns authentication, authorization, quotation status, and booking
creation. Python validates the supplied quotation snapshot and pauses for the
customer decision; it never calls backend write endpoints or accesses PostgreSQL.

## Workflow

```
validate  →  approval_gate (interrupt)  →  finalize  →  END
```

- `validate` — deterministic business-rule checks (amounts > 0, items exist).
- `approval_gate` — **pauses** the graph via `interrupt()`. The AI cannot decide pricing.
- `finalize` — returns the human decision to ASP.NET. ASP.NET validates the
  quotation ID and decision, then creates the booking or rejects the quotation.

## Setup

```bash
python -m venv .venv
.venv\Scripts\activate          # Windows
pip install -r requirements.txt
copy .env.example .env
```

Set `INTERNAL_API_KEY` when service-to-service authentication is required. Use
the same value for ASP.NET `AgentServices:InternalApiKey`; leave both unset only
for isolated local development.

## Run

```bash
uvicorn app.main:app --reload --port 8002
```

Service will be available at `http://localhost:8002`. Open `/docs` for Swagger.

## Endpoints

| Method | Path | Purpose |
|---|---|---|
| `GET`  | `/health` | Liveness check |
| `POST` | `/workflows/start` | Start workflow for a quotation |
| `POST` | `/workflows/resume` | Resume after customer decision |

ASP.NET calls these endpoints; frontends should use the ASP.NET quotation API.
`POST /api/quotations/{id}/send-for-approval` returns a `threadId`. Include that
value as `threadId` in the authenticated customer's approve/reject request.
Workflow checkpoints are currently in memory and are lost when this service
restarts; use one agent process while a workflow is pending.

## Testing

```bash
pytest tests/
```

## Port

Uses **8002**. `problem-understanding-agent` uses 8001 — do not collide.