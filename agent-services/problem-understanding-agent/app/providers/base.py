"""Base LLM Provider interface and result models."""
from abc import ABC, abstractmethod
from typing import Annotated
from pydantic import Field
from app.schemas.visual_result import VisualResultFields
from app.schemas.visual_evidence import VisualEvidence
from app.schemas.response import CanonicalCategory, ServiceUrgency


class LLMProviderResult(VisualResultFields):
    """Raw structured result returned by foundational model providers."""

    category: CanonicalCategory = "Unclassified"
    problem_summary: str = Field(alias="problemSummary", min_length=1, max_length=1000)
    urgency: ServiceUrgency = "Unknown"
    needs_more_information: bool = Field(default=False, alias="needsMoreInformation")
    follow_up_questions: list[Annotated[str, Field(min_length=1, max_length=500)]] = Field(default_factory=list, alias="followUpQuestions", max_length=3)
    confidence: float = Field(default=0.5, ge=0, le=1, allow_inf_nan=False)
    text_image_conflict: bool = Field(default=False, alias="textImageConflict", strict=True)
    visual_ambiguity_resolved: bool = Field(default=False, alias="visualAmbiguityResolved", strict=True)
    additional_information: dict[str, str] = Field(
        default_factory=dict, alias="additionalInformation"
    )

    model_config = {"populate_by_name": True}


class ProviderError(Exception):
    """Base exception for LLM provider failures."""

    def __init__(self, message: str, status_code: int | None = None) -> None:
        super().__init__(message)
        self.status_code = status_code


class TransientProviderError(ProviderError):
    """Transient errors eligible for bounded retry (rate limit, 503, timeout)."""
    pass


class PermanentProviderError(ProviderError):
    """Fatal errors that must fail fast without retry (auth, bad request, model not found)."""
    pass


class BaseLLMProvider(ABC):
    """Abstract interface decoupling agent reasoning graph from model vendors."""

    @property
    def supports_images(self) -> bool:
        """Implementation capability, not vendor marketing. Only adapters with implemented image payloads enable this."""
        return False

    @property
    @abstractmethod
    def provider_name(self) -> str:
        """Name of the provider backend."""
        pass

    @property
    @abstractmethod
    def model_name(self) -> str:
        """Model identifier being invoked."""
        pass

    @abstractmethod
    async def generate_problem_understanding(
        self,
        prompt: str,
        system_instruction: str,
        visual_evidence: list[VisualEvidence] | None = None,
    ) -> LLMProviderResult:
        """
        Executes reasoning call against the underlying provider.

        Must respect caller cancellation and raise TransientProviderError
        or PermanentProviderError on failure.
        """
        pass
