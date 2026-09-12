"""ServiceKnowledgeTool - Safe domain knowledge and inspection guidance retrieval."""
from dataclasses import dataclass


@dataclass(frozen=True)
class ServiceKnowledgeData:
    """Output data returned by ServiceKnowledgeTool."""

    service_family: str
    safe_general_terminology: str
    recommends_professional_inspection: bool
    possible_missing_information: list[str]


def get_service_knowledge_tool(category: str, description: str = "") -> ServiceKnowledgeData:
    """
    Retrieves safe domain knowledge and general inspection advice for a given category.

    Strictly uses possibility language, never guarantees diagnoses, and provides no dangerous DIY instructions.
    """
    cat_lower = (category or "Unclassified").strip().lower()

    if cat_lower == "electrical":
        return ServiceKnowledgeData(
            service_family="Electrical Systems",
            safe_general_terminology=(
                "Possible electrical circuit, fixture, or wiring irregularity. "
                "Professional inspection is recommended to ensure safety."
            ),
            recommends_professional_inspection=True,
            possible_missing_information=[
                "Whether circuit breakers or safety switches have tripped.",
                "Whether any burning odor, smoke, or sparking has been observed.",
                "The specific fixtures, sockets, or appliances affected.",
            ],
        )

    if cat_lower == "plumbing":
        return ServiceKnowledgeData(
            service_family="Plumbing and Water Supply",
            safe_general_terminology=(
                "Possible water supply, drainage, or pipe fixture fault. "
                "Professional inspection is recommended to prevent water damage."
            ),
            recommends_professional_inspection=True,
            possible_missing_information=[
                "The specific location of the leak or fixture involved.",
                "Whether the main water shutoff valve has been isolated.",
                "Whether the water flow is a slow drip, continuous stream, or flooding.",
            ],
        )

    if cat_lower in ("vehicle repair", "vehicle"):
        return ServiceKnowledgeData(
            service_family="Automotive and Transport",
            safe_general_terminology=(
                "Possible mechanical, starting, or electrical fault in vehicle. "
                "Professional inspection is recommended before driving."
            ),
            recommends_professional_inspection=True,
            possible_missing_information=[
                "Vehicle make, model, and fuel type.",
                "Whether any warning lights or error indicators are illuminated on the dashboard.",
                "Whether the vehicle is safely parked or immobilized on a roadway.",
            ],
        )

    if cat_lower in ("appliance repair", "appliance"):
        return ServiceKnowledgeData(
            service_family="Domestic and Commercial Appliances",
            safe_general_terminology=(
                "Possible internal mechanical, electrical, or thermal fault in appliance. "
                "Professional inspection is recommended."
            ),
            recommends_professional_inspection=True,
            possible_missing_information=[
                "Appliance brand, model, and approximate age.",
                "Whether the unit powers on, displays error codes, or makes unusual noises.",
                "Whether the power supply to the unit is operational.",
            ],
        )

    return ServiceKnowledgeData(
        service_family="General Services",
        safe_general_terminology=(
            "General service issue. Professional inspection is recommended to diagnose the requirement."
        ),
        recommends_professional_inspection=False,
        possible_missing_information=[
            "More detailed description of the problem or symptoms.",
            "Type of property, equipment, or vehicle requiring service.",
        ],
    )
