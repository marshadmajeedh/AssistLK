"""
HTTP tools that call the ASP.NET Core API.

Architectural rule: agents NEVER touch PostgreSQL directly.
Every business action goes through an allow-listed backend endpoint.

Note on circular calls:
  The .NET side already transitions the quotation to
  WAITING_FOR_CUSTOMER_APPROVAL before invoking this workflow, and
  the .NET side also creates the Booking on /approve. Therefore this
  module exposes:
    - get_quotation               (used by validate_node)
    - send_quotation_for_approval (kept for standalone testing ONLY)
    - approve_quotation           (kept for standalone testing ONLY)
    - reject_quotation            (kept for standalone testing ONLY)

  agent.py calls only get_quotation to avoid the .NET → Python → .NET loop.
"""

import os

from dotenv import load_dotenv
import httpx

# Load .env BEFORE reading environment variables
load_dotenv()

BACKEND_URL = os.environ.get("BACKEND_API_URL", "http://localhost:5012").rstrip("/")
BEARER_TOKEN = os.environ.get("SERVICE_BEARER_TOKEN", "")

DEFAULT_TIMEOUT = 15.0


def _headers() -> dict[str, str]:
    headers = {"Content-Type": "application/json"}
    if BEARER_TOKEN:
        headers["Authorization"] = f"Bearer {BEARER_TOKEN}"
    return headers


async def get_quotation(quotation_id: int) -> dict:
    """Fetch a quotation by id (read-only)."""
    async with httpx.AsyncClient(timeout=DEFAULT_TIMEOUT) as client:
        r = await client.get(
            f"{BACKEND_URL}/api/quotations/{quotation_id}",
            headers=_headers(),
        )
        r.raise_for_status()
        return r.json()


async def send_quotation_for_approval(quotation_id: int) -> dict:
    """
    Move a quotation to WAITING_FOR_CUSTOMER_APPROVAL.
    DO NOT call this from agent.py — the .NET side already does this,
    and calling it again would create a circular call.
    Kept for standalone/manual testing.
    """
    async with httpx.AsyncClient(timeout=DEFAULT_TIMEOUT) as client:
        r = await client.post(
            f"{BACKEND_URL}/api/quotations/{quotation_id}/send-for-approval",
            headers=_headers(),
        )
        r.raise_for_status()
        return r.json()


async def approve_quotation(
    quotation_id: int,
    customer_remarks: str | None,
    thread_id: str,
) -> dict:
    """
    Approve a quotation via .NET. The .NET backend atomically creates a Booking.
    DO NOT call this from agent.py — the .NET side already calls this route
    when the customer approves through the API.
    """
    async with httpx.AsyncClient(timeout=DEFAULT_TIMEOUT) as client:
        r = await client.post(
            f"{BACKEND_URL}/api/quotations/{quotation_id}/approve",
            headers=_headers(),
            json={"customerRemarks": customer_remarks, "threadId": thread_id},
        )
        r.raise_for_status()
        return r.json()


async def reject_quotation(
    quotation_id: int,
    reason: str,
    thread_id: str,
) -> dict:
    """
    Reject a quotation via .NET.
    DO NOT call this from agent.py — the .NET side already calls this route
    when the customer rejects through the API.
    """
    async with httpx.AsyncClient(timeout=DEFAULT_TIMEOUT) as client:
        r = await client.post(
            f"{BACKEND_URL}/api/quotations/{quotation_id}/reject",
            headers=_headers(),
            json={"reason": reason, "threadId": thread_id},
        )
        r.raise_for_status()
        return r.json()


# Self-diagnostic — prints once at startup so you can confirm env vars loaded
print(f"[tools] BACKEND_URL = {BACKEND_URL}")
print(f"[tools] TOKEN present = {bool(BEARER_TOKEN)} (length={len(BEARER_TOKEN)})")