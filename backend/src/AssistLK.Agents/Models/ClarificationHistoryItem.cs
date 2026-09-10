namespace AssistLK.Agents.Models;

/// <summary>
/// Represents a historical clarification question and customer-provided answer
/// passed as non-authoritative customer evidence to the ProblemUnderstandingAgent.
/// </summary>
public class ClarificationHistoryItem
{
    public int Round { get; set; }

    public string Question { get; set; } = string.Empty;

    public string Answer { get; set; } = string.Empty;
}
