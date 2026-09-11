"""FastAPI application entrypoint for the Problem Understanding Agent Service."""
import logging
import time
from typing import Any
from fastapi import Depends, FastAPI, Header, HTTPException, Request, status
from app.config import Settings, get_settings
from app.graphs.problem_graph import create_problem_understanding_graph
from app.providers.factory import ProviderFactory
from app.schemas.request import AgentExecutionRequest
from app.schemas.response import (
    AgentExecutionResponse,
    ExecutionMetadataDto,
    ProblemUnderstandingOutputDto,
    ToolExecutionAuditDto,
)

# Configure logging to never log prompt bodies, tokens, or raw secrets
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
)
logger = logging.getLogger("problem_understanding_agent")

app = FastAPI(
    title="AssistLK Problem Understanding Agent Service",
    description="Internal Agentic AI service for Component 1 problem reasoning and classification.",
    version="1.0.0",
    docs_url=None,  # Disable Swagger UI in internal production
    redoc_url=None,
)


def verify_internal_auth(
    settings: Settings = Depends(get_settings),
    x_api_key: str | None = Header(default=None, alias="X-Api-Key"),
    x_internal_api_key: str | None = Header(default=None, alias="X-Internal-Api-Key"),
) -> None:
    """Validates internal service-to-service key if configured."""
    required_key = settings.INTERNAL_API_KEY
    if not required_key or not required_key.strip():
        # Open in development when key is not configured
        return

    supplied_key = x_internal_api_key or x_api_key
    if not supplied_key or supplied_key.strip() != required_key.strip():
        logger.warning("Unauthorized internal request: invalid or missing internal API key.")
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid or missing internal service API key.",
        )


@app.get("/health", status_code=status.HTTP_200_OK)
async def health_check(settings: Settings = Depends(get_settings)) -> dict[str, Any]:
    """Health check endpoint confirming service status and active provider."""
    provider = ProviderFactory.create_provider(settings)
    return {
        "status": "healthy",
        "service": settings.SERVICE_NAME,
        "environment": settings.ENVIRONMENT,
        "provider": provider.provider_name,
        "model": provider.model_name,
    }


@app.post(
    "/agent/execute",
    response_model=AgentExecutionResponse,
    status_code=status.HTTP_200_OK,
    dependencies=[Depends(verify_internal_auth)],
)
async def execute_agent(
    request: AgentExecutionRequest,
    settings: Settings = Depends(get_settings),
) -> AgentExecutionResponse:
    """
    Executes the Problem Understanding LangGraph agent on the incoming request payload.

    Returns the single authoritative problem understanding result with safe audit metadata.
    """
    t0 = time.perf_counter()
    logger.info("Executing ProblemUnderstandingAgent for requestId: %s", request.request_id)

    provider = ProviderFactory.create_provider(settings)
    graph = create_problem_understanding_graph(provider)

    inp = request.input
    initial_state = {
        "request_id": str(request.request_id),
        "service_request_id": str(inp.service_request_id),
        "description": inp.description,
        "location_text": inp.location_text,
        "latitude": inp.latitude,
        "longitude": inp.longitude,
        "category_hint": inp.category_hint,
        "clarification_history": [item.model_dump() for item in inp.clarification_history],
        "tool_executions": [],
    }

    try:
        final_state = await graph.ainvoke(initial_state)

        output_raw = final_state.get("output") or {}
        output_dto = ProblemUnderstandingOutputDto.model_validate(output_raw)

        elapsed_ms = max(1, int((time.perf_counter() - t0) * 1000))

        raw_tools = final_state.get("tool_executions") or []
        tool_audits = [
            ToolExecutionAuditDto(
                tool=t["tool"],
                success=t["success"],
                durationMs=t.get("durationMs", 1),
            )
            for t in raw_tools
        ]

        metadata = ExecutionMetadataDto(
            agentName=request.agent_name,
            provider=provider.provider_name,
            degraded=final_state.get("degraded", False),
            durationMs=elapsed_ms,
            toolExecutions=tool_audits,
        )

        return AgentExecutionResponse(
            requestId=request.request_id,
            success=True,
            result=output_dto,
            errorMessage=None,
            metadata=metadata,
        )

    except Exception as ex:
        elapsed_ms = max(1, int((time.perf_counter() - t0) * 1000))
        logger.error("Agent execution unrecoverable failure: %s", ex, exc_info=True)

        metadata = ExecutionMetadataDto(
            agentName=request.agent_name,
            provider=provider.provider_name,
            degraded=True,
            durationMs=elapsed_ms,
            toolExecutions=[],
        )

        return AgentExecutionResponse(
            requestId=request.request_id,
            success=False,
            result=None,
            errorMessage=f"Agent execution encountered an unrecoverable error: {type(ex).__name__}",
            metadata=metadata,
        )


# Direct alias endpoint for convenience
@app.post(
    "/internal/v1/problem-understanding/analyze",
    response_model=AgentExecutionResponse,
    status_code=status.HTTP_200_OK,
    dependencies=[Depends(verify_internal_auth)],
)
async def analyze_alias(
    request: AgentExecutionRequest,
    settings: Settings = Depends(get_settings),
) -> AgentExecutionResponse:
    """Alias for /agent/execute targeting internal v1 path."""
    return await execute_agent(request, settings)
