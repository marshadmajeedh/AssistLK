namespace AssistLK.Agents.Abstractions;

/// <summary>
/// Abstraction for Google Gemini LLM reasoning service.
/// </summary>
public interface IGeminiService
{
    /// <summary>
    /// Generates content using Google Gemini model given a user prompt and optional system instruction.
    /// </summary>
    /// <param name="prompt">The user prompt containing the problem description.</param>
    /// <param name="systemInstruction">Optional system instruction defining the agent's role, rules, and output schema.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw text/JSON response from the model, or null if generation failed.</returns>
    Task<string?> GenerateContentAsync(
        string prompt,
        string? systemInstruction = null,
        CancellationToken cancellationToken = default);
}
