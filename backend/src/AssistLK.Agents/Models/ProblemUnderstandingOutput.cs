using AssistLK.Domain.Enums;

namespace AssistLK.Agents.Models;

/// <summary>
/// Structured analysis result produced by the ProblemUnderstandingAgent.
/// All fields are safe for direct presentation to the customer.
/// No chain-of-thought, private reasoning, or internal rationale is included.
/// </summary>
public class ProblemUnderstandingOutput
{
    /// <summary>
    /// The service category inferred from the customer's description.
    /// Uses stable semantic values such as "Plumbing", "Electrical",
    /// "Vehicle Repair", "Appliance Repair".
    /// Remains "Unclassified" when the category cannot be determined
    /// with sufficient confidence.
    /// </summary>
    public string Category { get; set; }
        = "Unclassified";

    /// <summary>
    /// A concise, uncertainty-aware summary of the inferred problem.
    /// Uses language such as "Possible water leakage or plumbing fault."
    /// Never states a guaranteed diagnosis.
    /// </summary>
    public string ProblemSummary { get; set; }
        = string.Empty;

    /// <summary>
    /// The urgency level determined by the agent based on the described
    /// problem. The customer does not supply this value.
    /// Remains Unknown only when information is genuinely insufficient
    /// to form a conservative estimate.
    /// </summary>
    public ServiceRequestUrgency Urgency { get; set; }
        = ServiceRequestUrgency.Unknown;

    /// <summary>
    /// True when the agent determines the customer's description does not
    /// contain enough information to produce a confident analysis.
    /// </summary>
    public bool NeedsMoreInformation { get; set; }

    /// <summary>
    /// Relevant, concise follow-up questions to ask the customer when
    /// NeedsMoreInformation is true. Empty when analysis is sufficient.
    /// Bounded to a small number (1–3) of questions.
    /// Questions must not instruct the customer to perform dangerous
    /// repair activities.
    /// </summary>
    public IReadOnlyList<string> FollowUpQuestions { get; set; }
        = Array.Empty<string>();

    /// <summary>
    /// Normalised confidence value in the range [0, 1].
    /// </summary>
    public decimal Confidence { get; set; }

    /// <summary>
    /// The location text extracted or confirmed from the customer input,
    /// if determinable. Null when no location information is available.
    /// </summary>
    public string? ExtractedLocation { get; set; }

    /// <summary>
    /// Additional structured information gathered during analysis.
    /// Used for extensible, non-breaking supplemental data.
    /// </summary>
    public IReadOnlyDictionary<string, string> AdditionalInformation { get; set; }
        = new Dictionary<string, string>();
}
