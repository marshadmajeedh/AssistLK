"""Provider Factory dynamically creating model providers based on environment configuration."""
import logging
from typing import Optional
import httpx
from app.config import Settings, get_settings
from app.providers.base import BaseLLMProvider, PermanentProviderError
from app.providers.azure_openai_provider import AzureOpenAIProvider
from app.providers.gemini_provider import GeminiProvider
from app.providers.offline_provider import OfflineSimulationProvider
from app.providers.openai_provider import OpenAIProvider

logger = logging.getLogger("provider_factory")


class ProviderFactory:
    """Factory creating configured LLM provider instances."""

    @staticmethod
    def create_provider(
        settings: Optional[Settings] = None,
        client: Optional[httpx.AsyncClient] = None,
    ) -> BaseLLMProvider:
        """
        Creates and returns a BaseLLMProvider instance according to application settings.

        Falls back to OfflineSimulationProvider if API keys are absent and
        ALLOW_OFFLINE_SIMULATION is True.
        """
        cfg = settings or get_settings()
        provider_type = (cfg.LLM_PROVIDER or "gemini").strip().lower()

        if provider_type == "offline":
            logger.info("Instantiating OfflineSimulationProvider (explicit configuration).")
            return OfflineSimulationProvider()

        if provider_type == "gemini":
            if not cfg.GOOGLE_API_KEY or not cfg.GOOGLE_API_KEY.strip():
                if cfg.ALLOW_OFFLINE_SIMULATION:
                    logger.info("GOOGLE_API_KEY missing. Engaging OfflineSimulationProvider (dev/test mode).")
                    return OfflineSimulationProvider(model_name="offline-gemini-sim")
                raise PermanentProviderError(
                    "GOOGLE_API_KEY is required for gemini provider when offline simulation is disabled."
                )

            return GeminiProvider(
                api_key=cfg.GOOGLE_API_KEY,
                model=cfg.GEMINI_MODEL,
                timeout_seconds=cfg.LLM_TIMEOUT_SECONDS,
                max_attempts=cfg.LLM_MAX_ATTEMPTS,
                client=client,
            )

        if provider_type == "openai":
            if not cfg.OPENAI_API_KEY or not cfg.OPENAI_API_KEY.strip():
                if cfg.ALLOW_OFFLINE_SIMULATION:
                    logger.info("OPENAI_API_KEY missing. Engaging OfflineSimulationProvider (dev/test mode).")
                    return OfflineSimulationProvider(model_name="offline-openai-sim")
                raise PermanentProviderError(
                    "OPENAI_API_KEY is required for openai provider when offline simulation is disabled."
                )

            return OpenAIProvider(
                api_key=cfg.OPENAI_API_KEY,
                model=cfg.OPENAI_MODEL,
                timeout_seconds=cfg.LLM_TIMEOUT_SECONDS,
                max_attempts=cfg.LLM_MAX_ATTEMPTS,
                client=client,
            )

        if provider_type == "azure_openai":
            if not cfg.AZURE_OPENAI_API_KEY or not cfg.AZURE_OPENAI_API_KEY.strip() or not cfg.AZURE_OPENAI_ENDPOINT or not cfg.AZURE_OPENAI_ENDPOINT.strip():
                if cfg.ALLOW_OFFLINE_SIMULATION:
                    logger.info("AZURE_OPENAI_API_KEY or AZURE_OPENAI_ENDPOINT missing. Engaging OfflineSimulationProvider (dev/test mode).")
                    return OfflineSimulationProvider(model_name="offline-azure-sim")
                raise PermanentProviderError(
                    "AZURE_OPENAI_API_KEY and AZURE_OPENAI_ENDPOINT are required for azure_openai provider when offline simulation is disabled."
                )

            return AzureOpenAIProvider(
                api_key=cfg.AZURE_OPENAI_API_KEY,
                endpoint=cfg.AZURE_OPENAI_ENDPOINT,
                deployment_name=cfg.AZURE_OPENAI_DEPLOYMENT_NAME,
                api_version=cfg.AZURE_OPENAI_API_VERSION,
                timeout_seconds=cfg.LLM_TIMEOUT_SECONDS,
                max_attempts=cfg.LLM_MAX_ATTEMPTS,
                client=client,
            )

        raise ValueError(f"Unsupported LLM_PROVIDER '{cfg.LLM_PROVIDER}'. Allowed: gemini, openai, azure_openai, offline.")
