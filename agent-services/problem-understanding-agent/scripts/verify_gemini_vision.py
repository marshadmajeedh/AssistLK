"""Explicit opt-in live check. Uses a synthetic service-problem JPEG, never customer photos."""
import argparse
import asyncio
import base64
import json
import logging
import os
from pathlib import Path
import sys
import time
from uuid import UUID

SERVICE_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SERVICE_ROOT))


async def verify() -> int:
    from app.config import Settings
    from app.graphs.problem_graph import create_problem_understanding_graph
    from app.providers.gemini_provider import GeminiProvider
    from app.schemas.visual_evidence import VisualEvidence

    settings = Settings(_env_file=SERVICE_ROOT / ".env")
    if not settings.GOOGLE_API_KEY or not settings.GOOGLE_API_KEY.strip():
        print("NOT PERFORMED: GOOGLE_API_KEY is unavailable.")
        return 2
    # Freshly encoded quality-85 RGB JPEG, 768x512, no EXIF/GPS/comments or personal content.
    evidence = VisualEvidence(attachmentId=UUID("b102b721-4b3a-4d5b-8877-189c33c9c42f"),
        contentType="image/jpeg", width=768, height=512,
        dataBase64=base64.b64encode((SERVICE_ROOT / "scripts/fixtures/synthetic_sink_leak.jpg").read_bytes()).decode())
    provider = GeminiProvider(settings.GOOGLE_API_KEY, model=settings.GEMINI_MODEL,
        timeout_seconds=settings.LLM_TIMEOUT_SECONDS, max_attempts=settings.LLM_MAX_ATTEMPTS)
    started = time.perf_counter()
    try:
        state = await create_problem_understanding_graph(provider).ainvoke({
            "description": "There is a problem below my kitchen sink. Please assess the visible condition.",
            "location_text": "Colombo", "visual_evidence": [evidence]})
        output = state["output"]
        assert output["visionStatus"] == "used"
        assert output["attachmentIdsUsed"] == [str(evidence.attachment_id)]
        assert output["visualObservations"]
        assert all(o["attachmentId"] == str(evidence.attachment_id) for o in output["visualObservations"])
        assert any(word in " ".join(o["observation"] for o in output["visualObservations"]).lower()
                   for word in ("water", "droplet", "puddle", "drip", "moisture"))
        print(json.dumps({"status": "PASS", "provider": "gemini", "model": settings.GEMINI_MODEL,
            "elapsedSeconds": round(time.perf_counter() - started, 2), "result": output}, indent=2))
        return 0
    except Exception as error:
        # No exception repr, provider body, credentials or image payload in output.
        print(json.dumps({"status": "BLOCKED OR FAILED", "errorType": type(error).__name__,
            "elapsedSeconds": round(time.perf_counter() - started, 2), "model": settings.GEMINI_MODEL}))
        return 1


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run-live", action="store_true", help="Authorize a real Gemini API request (may incur cost).")
    args = parser.parse_args()
    if not args.run_live or os.environ.get("ASSISTLK_RUN_LIVE_VISION") != "1":
        print("NOT PERFORMED: requires --run-live and ASSISTLK_RUN_LIVE_VISION=1.")
        return 2
    # Never export transient LangGraph state through optional tracing integrations.
    os.environ["LANGCHAIN_TRACING_V2"] = "false"
    os.environ["LANGSMITH_TRACING"] = "false"
    logging.basicConfig(level=logging.WARNING)
    return asyncio.run(verify())


if __name__ == "__main__":
    raise SystemExit(main())
