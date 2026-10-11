"""Deterministic offline simulation provider for testing and fallback without API keys."""
import html
import re
from app.providers.base import BaseLLMProvider, LLMProviderResult
from app.schemas.visual_evidence import VisualEvidence


def extract_customer_description(prompt: str) -> str:
    """Extracts decoded text between <customer_description> tags."""
    match = re.search(r"<customer_description>(.*?)</customer_description>", prompt, re.DOTALL | re.IGNORECASE)
    if match:
        return html.unescape(match.group(1).strip())
    return prompt


class OfflineSimulationProvider(BaseLLMProvider):
    """
    Deterministic offline heuristic provider.

    Reproduces the verified test-simulation logic from C# GeminiService.SimulateOfflineReasoning
    without contacting external network or third-party APIs.
    """

    def __init__(self, model_name: str = "offline-heuristic-v1") -> None:
        self._model_name = model_name

    @property
    def provider_name(self) -> str:
        return "offline"

    @property
    def model_name(self) -> str:
        return self._model_name

    async def generate_problem_understanding(
        self, prompt: str, system_instruction: str,
        visual_evidence: list[VisualEvidence] | None = None,
    ) -> LLMProviderResult:
        result = await self._generate_text_result(prompt, system_instruction)
        return result.model_copy(update={"vision_status": "unsupported" if visual_evidence else "not_requested"})

    async def _generate_text_result(
        self,
        prompt: str,
        system_instruction: str,
    ) -> LLMProviderResult:
        desc = extract_customer_description(prompt)
        lower = desc.lower().strip()

        # Purely ambiguous / unclassifiable descriptions
        if (
            "broken please fix" in lower
            or "it is broken please fix" in lower
            or ("broken" in lower and not any(k in lower for k in ["pipe", "tap", "car", "fridge", "refrigerator", "appliance", "sink"]))
        ):
            return LLMProviderResult(
                category="Unclassified",
                problemSummary="Possible service issue. Insufficient information provided to determine category.",
                urgency="Unknown",
                needsMoreInformation=True,
                followUpQuestions=[
                    "Could you describe the equipment, system, or fixture affected?",
                    "What specific symptoms are occurring?",
                ],
                confidence=0.2,
                additionalInformation={"Simulation": "True"},
            )

        # Single word or vague appliance
        if lower in ("refrigerator", "appliance is broken", "my appliance is broken"):
            return LLMProviderResult(
                category="Appliance Repair",
                problemSummary="Possible refrigeration or domestic appliance fault.",
                urgency="Unknown",
                needsMoreInformation=True,
                followUpQuestions=[
                    "Could you describe the appliance, system, or vehicle that is affected?",
                    "What specific symptoms are occurring?",
                ],
                confidence=0.4,
                additionalInformation={"Simulation": "True"},
            )

        # Plumbing
        if any(k in lower for k in ["pipe", "leak", "sink", "tap", "plumb", "flood", "drain", "drip"]):
            is_urgent = any(k in lower for k in ["burst", "flood", "heavily", "badly", "gushing"])
            is_drip = "drip" in lower or "slowly" in lower
            urgency = "High" if is_urgent else ("Low" if is_drip else "Medium")
            summary = (
                "Possible burst pipe or water flooding. Immediate professional attention may be required."
                if is_urgent
                else ("Possible dripping tap or minor pipe leak." if is_drip else "Possible water leakage or plumbing fault.")
            )
            return LLMProviderResult(
                category="Plumbing",
                problemSummary=summary,
                urgency=urgency,
                needsMoreInformation=False,
                followUpQuestions=[],
                confidence=0.9 if is_urgent else 0.85,
                additionalInformation={"Simulation": "True"},
            )

        # Electrical
        if any(k in lower for k in ["spark", "socket", "electric", "wire", "breaker", "burning", "smell"]):
            is_fire = "fire" in lower
            is_urgent = any(k in lower for k in ["spark", "burn", "smoke", "shock", "smell"])
            urgency = "Critical" if is_fire else ("High" if is_urgent else "Medium")
            return LLMProviderResult(
                category="Electrical",
                problemSummary="Possible electrical safety issue. Professional inspection is recommended to ensure safety.",
                urgency=urgency,
                needsMoreInformation=False,
                followUpQuestions=[],
                confidence=0.88,
                additionalInformation={"Simulation": "True"},
            )

        # Vehicle Repair
        if any(k in lower for k in ["car", "vehicle", "battery", "engine", "brake"]):
            is_accident = "accident" in lower or "crash" in lower
            is_urgent = is_accident or "stopped" in lower or "road" in lower or "start" in lower
            urgency = "Critical" if is_accident else ("High" if is_urgent else "Medium")
            summary = (
                "Possible battery or vehicle starting-system issue."
                if "start" in lower
                else "Possible vehicle mechanical fault."
            )
            return LLMProviderResult(
                category="Vehicle Repair",
                problemSummary=summary,
                urgency=urgency,
                needsMoreInformation=False,
                followUpQuestions=[],
                confidence=0.85,
                additionalInformation={"Simulation": "True"},
            )

        # Appliance Repair
        if any(k in lower for k in ["fridge", "refrigerator", "freezer", "washer", "dryer", "dishwasher", "appliance"]):
            summary = (
                "Possible refrigeration or cooling system fault."
                if any(k in lower for k in ["fridge", "refrigerator", "freezer"])
                else "Possible domestic appliance fault."
            )
            return LLMProviderResult(
                category="Appliance Repair",
                problemSummary=summary,
                urgency="Low",
                needsMoreInformation=False,
                followUpQuestions=[],
                confidence=0.82,
                additionalInformation={"Simulation": "True"},
            )

        # Default fallback
        return LLMProviderResult(
            category="Unclassified",
            problemSummary="Possible service issue. Category could not be determined with confidence.",
            urgency="Unknown",
            needsMoreInformation=True,
            followUpQuestions=["Could you provide more details about the problem?"],
            confidence=0.2,
            additionalInformation={"Simulation": "True"},
        )
