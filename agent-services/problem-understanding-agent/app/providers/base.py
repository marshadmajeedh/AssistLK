"""Base LLM Provider interface and result models."""
from abc import ABC, abstractmethod
from pydantic import BaseModel, Field


class LLMProviderResult(BaseModel):
    """Raw structured result returned by foundational model providers."""

    category: str = "Unclassified"
    problem_summary: str = Field(alias="problemSummary")
    urgency: str = "Unknown"
    needs_more_information: bool = Field(default=False, alias="needsMoreInformation")
    follow_up_questions: list[str] = Field(default_factory=list, alias="followUpQuestions")
    confidence: float = 0.5
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
        """Implementation capability, not vendor marketing. All adapters are text-only in Phase 3."""
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
    ) -> LLMProviderResult:
        """
        Executes reasoning call against the underlying provider.

        Must respect caller cancellation and raise TransientProviderError
        or PermanentProviderError on failure.
        """
        pass
