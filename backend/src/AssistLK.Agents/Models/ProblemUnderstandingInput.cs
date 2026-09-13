namespace AssistLK.Agents.Models;

/// <summary>
/// Typed input for the ProblemUnderstandingAgent.
/// The workflow adapter places this object into AgentContext.Data
/// before calling ExecuteAsync.
/// AgentContext.Input may additionally carry Description as a
/// plain string for compatibility with the shared agent framework.
/// </summary>
public class ProblemUnderstandingInput
{
    /// <summary>
    /// Small internal audit metadata; never sent to the Python wire contract.
    /// </summary>
    public long? EvidenceRevision { get; set; }

    /// <summary>The unique identifier of the ServiceRequest being analysed.</summary>
    public Guid ServiceRequestId { get; set; }

    /// <summary>
    /// The customer's natural-language description of their problem.
    /// </summary>
    public string Description { get; set; }
        = string.Empty;

    /// <summary>
    /// Human-readable location text supplied by the customer.
    /// </summary>
    public string LocationText { get; set; }
        = string.Empty;

    /// <summary>
    /// Optional GPS latitude coordinate.
    /// </summary>
    public decimal? Latitude { get; set; }

    /// <summary>
    /// Optional GPS longitude coordinate.
    /// </summary>
    public decimal? Longitude { get; set; }

    /// <summary>
    /// Optional customer service category preference / initial belief.
    /// Non-authoritative context for the agent.
    /// </summary>
    public string? CategoryHint { get; set; }

    /// <summary>
    /// Historical clarification rounds and answers provided by the customer.
    /// Supplied as non-authoritative customer evidence.
    /// </summary>
    public IReadOnlyList<ClarificationHistoryItem> ClarificationHistory { get; set; }
        = Array.Empty<ClarificationHistoryItem>();
}
