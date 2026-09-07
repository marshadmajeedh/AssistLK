using AssistLK.Agents.Abstractions;

namespace AssistLK.Agents.Tools;

public sealed record ServiceKnowledgeData(
    string ServiceFamily,
    string SafeGeneralTerminology,
    bool RecommendsProfessionalInspection,
    IReadOnlyList<string> PossibleMissingInformation);

/// <summary>
/// Tool to retrieve safe domain knowledge and general inspection guidance for a service category.
/// Strictly uses possibility language, never guarantees diagnoses, and provides no dangerous DIY instructions.
/// </summary>
public sealed class ServiceKnowledgeTool : IAgentTool
{
    public string Name => "ServiceKnowledgeTool";

    public string Description =>
        "Retrieves safe domain knowledge and general inspection advice for a given service category.";

    public Task<ToolResult> ExecuteAsync(
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        string category = "Unclassified";
        if (parameters.TryGetValue("category", out var catObj) && catObj != null)
        {
            var catStr = catObj.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(catStr))
            {
                category = catStr;
            }
        }

        string description = string.Empty;
        if (parameters.TryGetValue("description", out var descObj) && descObj != null)
        {
            description = descObj.ToString() ?? string.Empty;
        }

        var lower = description.ToLowerInvariant();

        ServiceKnowledgeData data = category switch
        {
            "Electrical" => new ServiceKnowledgeData(
                ServiceFamily: "Electrical Systems",
                SafeGeneralTerminology: "Possible electrical circuit, fixture, or wiring irregularity. Professional inspection is recommended to ensure safety.",
                RecommendsProfessionalInspection: true,
                PossibleMissingInformation: new[]
                {
                    "Whether circuit breakers or safety switches have tripped.",
                    "Whether any burning odor, smoke, or sparking has been observed.",
                    "The specific fixtures, sockets, or appliances affected."
                }),

            "Plumbing" => new ServiceKnowledgeData(
                ServiceFamily: "Plumbing and Water Supply",
                SafeGeneralTerminology: "Possible water supply, drainage, or pipe fixture fault. Professional inspection is recommended to prevent water damage.",
                RecommendsProfessionalInspection: true,
                PossibleMissingInformation: new[]
                {
                    "The specific location of the leak or fixture involved.",
                    "Whether the main water shutoff valve has been isolated.",
                    "Whether the water flow is a slow drip, continuous stream, or flooding."
                }),

            "Vehicle Repair" => new ServiceKnowledgeData(
                ServiceFamily: "Automotive and Transport",
                SafeGeneralTerminology: "Possible mechanical, starting, or electrical fault in vehicle. Professional inspection is recommended before driving.",
                RecommendsProfessionalInspection: true,
                PossibleMissingInformation: new[]
                {
                    "Vehicle make, model, and fuel type.",
                    "Whether any warning lights or error indicators are illuminated on the dashboard.",
                    "Whether the vehicle is safely parked or immobilized on a roadway."
                }),

            "Appliance Repair" => new ServiceKnowledgeData(
                ServiceFamily: "Domestic and Commercial Appliances",
                SafeGeneralTerminology: "Possible internal mechanical, electrical, or thermal fault in appliance. Professional inspection is recommended.",
                RecommendsProfessionalInspection: true,
                PossibleMissingInformation: new[]
                {
                    "Appliance brand, model, and approximate age.",
                    "Whether the unit powers on, displays error codes, or makes unusual noises.",
                    "Whether the power supply to the unit is operational."
                }),

            _ => new ServiceKnowledgeData(
                ServiceFamily: "General Services",
                SafeGeneralTerminology: "General service issue. Professional inspection is recommended to diagnose the requirement.",
                RecommendsProfessionalInspection: false,
                PossibleMissingInformation: new[]
                {
                    "More detailed description of the problem or symptoms.",
                    "Type of property, equipment, or vehicle requiring service."
                })
        };

        return Task.FromResult(new ToolResult
        {
            Success = true,
            Message = "Service knowledge retrieved.",
            Data = data
        });
    }
}
