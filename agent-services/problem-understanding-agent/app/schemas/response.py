"""Response schemas mirroring .NET ProblemUnderstandingOutput semantics without field duplication."""
from typing import Any, Literal
from uuid import UUID
from pydantic import BaseModel, ConfigDict, Field, field_validator
from app.schemas.visual_result import VisualResultFields

CanonicalCategory = Literal[
    "Plumbing",
    "Electrical",
    "Vehicle Repair",
    "Appliance Repair",
    "Unclassified",
]

ServiceUrgency = Literal[
    "Unknown",
    "Low",
    "Medium",
    "High",
    "Critical",
]


class ProblemUnderstandingOutputDto(VisualResultFields):
    """The single authoritative structured analysis result for Problem Understanding."""

    model_config = ConfigDict(populate_by_name=True)

    category: CanonicalCategory = Field(
        default="Unclassified",
        description="The service category inferred from the customer's description.",
    )
    problem_summary: str = Field(
        alias="problemSummary",
        min_length=1,
        max_length=1000,
        description="Concise, uncertainty-aware summary of the inferred problem.",
    )
    urgency: ServiceUrgency = Field(
        default="Unknown",
        description="The urgency level determined by the agent.",
    )
    needs_more_information: bool = Field(
        default=False,
        alias="needsMoreInformation",
        description="True when description is ambiguous or lacks necessary detail.",
    )
    follow_up_questions: list[str] = Field(
        default_factory=list,
        alias="followUpQuestions",
        description="Follow-up clarification questions (max 3) when more info is needed.",
    )
    confidence: float = Field(
        default=0.5,
        description="Normalized confidence score in range [0.0, 1.0].",
    )
    extracted_location: str | None = Field(
        default=None,
        alias="extractedLocation",
        max_length=500,
        description="Normalized location text extracted from input if available.",
    )
    additional_information: dict[str, str] = Field(
        default_factory=dict,
        alias="additionalInformation",
        description="Supplemental safe structured key-value metadata.",
    )

    @field_validator("follow_up_questions", mode="before")
    @classmethod
    def validate_follow_up_questions(cls, v: Any) -> list[str]:
        if not isinstance(v, list):
            return []
        truncated = [str(q).strip()[:500] for q in v if str(q).strip()][:3]
        return truncated

    @field_validator("confidence", mode="before")
    @classmethod
    def validate_confidence(cls, v: Any) -> float:
        try:
            val = float(v)
            clamped = max(0.0, min(1.0, val))
            return round(clamped, 2)
        except (ValueError, TypeError):
            return 0.5


class ToolExecutionAuditDto(BaseModel):
    """Safe audit summary of deterministic tool execution (no raw payloads)."""

    model_config = ConfigDict(populate_by_name=True)

    tool: str = Field(description="Name of the executed deterministic tool.")
    success: bool = Field(description="Whether tool execution succeeded.")
    duration_ms: int = Field(
        alias="durationMs",
        ge=0,
        description="Execution duration in milliseconds.",
    )


class ExecutionMetadataDto(BaseModel):
    """Safe execution metadata exposed alongside the authoritative result."""

    model_config = ConfigDict(populate_by_name=True)

    agent_name: str = Field(
        default="ProblemUnderstandingAgent",
        alias="agentName",
        description="Canonical identifier of executing agent.",
    )
    provider: str = Field(
        description="Active LLM provider backend (gemini, openai, offline).",
    )
    degraded: bool = Field(
        default=False,
        description="True if output was produced via safety degradation/fallback.",
    )
    duration_ms: int = Field(
        default=0,
        alias="durationMs",
        ge=0,
        description="Total agent execution duration in milliseconds.",
    )
    tool_executions: list[ToolExecutionAuditDto] = Field(
        default_factory=list,
        alias="toolExecutions",
        description="Safe audit summaries of tools called during execution.",
    )


class AgentExecutionResponse(BaseModel):
    """Standard internal agent response wire envelope containing the single authoritative result."""

    model_config = ConfigDict(populate_by_name=True)

    request_id: UUID = Field(
        alias="requestId",
        description="Echoed correlation ID matching the incoming request.",
    )
    success: bool = Field(
        description="True if agent execution completed without unrecoverable errors.",
    )
    result: ProblemUnderstandingOutputDto | None = Field(
        default=None,
        description="The single authoritative problem understanding deliverable.",
    )
    error_message: str | None = Field(
        default=None,
        alias="errorMessage",
        description="Sanitized failure description if success is false.",
    )
    metadata: ExecutionMetadataDto = Field(
        description="Safe execution and audit metadata.",
    )
