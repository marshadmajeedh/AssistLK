"""Configuration module for Problem Understanding Agent Service."""
from functools import lru_cache
from typing import Literal
from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """Application settings read from environment variables or .env file."""

    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore",
    )

    SERVICE_NAME: str = Field(
        default="problem-understanding-agent",
        description="Canonical identifier of this agent microservice.",
    )
    HOST: str = Field(default="127.0.0.1", description="Internal binding host.")
    PORT: int = Field(default=8001, description="Internal service listening port.")
    ENVIRONMENT: str = Field(default="development", description="Runtime environment.")

    # Provider Selection: "gemini" | "openai" | "offline"
    LLM_PROVIDER: Literal["gemini", "openai", "offline"] = Field(
        default="gemini",
        description="Selected LLM provider backend.",
    )

    # Google Gemini Configuration
    GOOGLE_API_KEY: str | None = Field(
        default=None,
        description="Google Generative Language API Key.",
    )
    # Must preserve the currently verified model: gemini-3.6-flash
    GEMINI_MODEL: str = Field(
        default="gemini-3.6-flash",
        description="Google Gemini model identifier.",
    )

    # OpenAI Configuration
    OPENAI_API_KEY: str | None = Field(
        default=None,
        description="OpenAI API Key.",
    )
    OPENAI_MODEL: str = Field(
        default="gpt-4o-mini",
        description="OpenAI model identifier.",
    )

    # Resilience & Timeouts (Python owns LLM retry behavior)
    LLM_TIMEOUT_SECONDS: float = Field(
        default=15.0,
        ge=1.0,
        le=60.0,
        description="Per-attempt timeout for LLM provider API calls.",
    )
    LLM_MAX_ATTEMPTS: int = Field(
        default=2,
        ge=1,
        le=3,
        description="Maximum attempts (1 initial + retries) for transient LLM errors.",
    )

    # Fallback / Offline Simulation
    ALLOW_OFFLINE_SIMULATION: bool = Field(
        default=True,
        description="Enable deterministic heuristic simulation when API keys are absent.",
    )

    # Internal Service Authentication (empty = no auth required in dev)
    INTERNAL_API_KEY: str | None = Field(
        default=None,
        description="Optional shared secret key for internal service-to-service validation.",
    )


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    """Returns cached settings singleton instance."""
    return Settings()
