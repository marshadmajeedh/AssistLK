using AssistLK.Agents.Abstractions;
using AssistLK.Agents.Clients;
using AssistLK.Agents.Core;
using AssistLK.Agents.DTOs;
using AssistLK.Agents.Models;
using AssistLK.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssistLK.Agents.Adapters;

/// <summary>
/// External agent adapter that delegates problem understanding to the internal
/// Python FastAPI + LangGraph agent service over HTTP while exposing the exact IAgent contract.
/// </summary>
public sealed class ExternalProblemUnderstandingAgentAdapter : IAgent
{
    private static readonly HashSet<string> AllowedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Plumbing",
        "Electrical",
        "Vehicle Repair",
        "Appliance Repair",
        "Unclassified"
    };

    private readonly IProblemUnderstandingClient _client;
    private readonly ILogger<ExternalProblemUnderstandingAgentAdapter> _logger;

    public ExternalProblemUnderstandingAgentAdapter(
        IProblemUnderstandingClient client,
        ILogger<ExternalProblemUnderstandingAgentAdapter>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? NullLogger<ExternalProblemUnderstandingAgentAdapter>.Instance;
    }

    /// <inheritdoc />
    public string Name => "ProblemUnderstandingAgent";

    /// <inheritdoc />
    public async Task<AgentResult> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var input = ResolveInput(context);
        var description = input?.Description ?? context.Input ?? string.Empty;

        // Gracefully handle empty or whitespace descriptions without remote round-trip
        if (string.IsNullOrWhiteSpace(description))
        {
            context.Data["ToolCalls"] = 0;
            context.Data["ToolCallCount"] = 0;

            var emptyOutput = new ProblemUnderstandingOutput
            {
                Category = "Unclassified",
                ProblemSummary = "Insufficient information provided to determine the problem.",
                Urgency = ServiceRequestUrgency.Unknown,
                NeedsMoreInformation = true,
                FollowUpQuestions = new[] { "Could you describe the problem you are experiencing?" },
                Confidence = 0.0m,
                ExtractedLocation = input?.LocationText,
                AdditionalInformation = new Dictionary<string, string>()
            };

            return new AgentResult
            {
                Success = true,
                Message = "Problem analysis incomplete. Additional information required.",
                Data = emptyOutput,
                NextAction = "AwaitingInformation"
            };
        }

        try
        {
            // 1. Build wire request DTO matching Python Pydantic contract
            var requestDto = new AgentExecutionRequestDto
            {
                RequestId = context.WorkflowId != Guid.Empty ? context.WorkflowId : Guid.NewGuid(),
                AgentName = Name,
                Operation = "analyze-problem",
                Input = new ProblemUnderstandingInputPayloadDto
                {
                    ServiceRequestId = input?.ServiceRequestId ?? Guid.NewGuid(),
                    Description = description,
                    VisualEvidence = context.VisualEvidence,
                    LocationText = input?.LocationText,
                    Latitude = (double?)input?.Latitude,
                    Longitude = (double?)input?.Longitude,
                    CategoryHint = input?.CategoryHint,
                    ClarificationHistory = input?.ClarificationHistory?
                        .Select(c => new ClarificationHistoryItemPayloadDto
                        {
                            Round = c.Round,
                            Question = c.Question,
                            Answer = c.Answer
                        })
                        .ToList() ?? new List<ClarificationHistoryItemPayloadDto>()
                }
            };

            // 2. Dispatch to Python service via typed client
            var response = await _client.ExecuteAsync(requestDto, cancellationToken);

            // 3. Handle explicit transport/service failure
            if (!response.Success)
            {
                _logger.LogWarning(
                    "Python agent service returned failure for request {RequestId}: {ErrorMessage}",
                    requestDto.RequestId,
                    response.ErrorMessage);

                return new AgentResult
                {
                    Success = false,
                    Message = response.ErrorMessage ?? "Python agent execution failed."
                };
            }

            // 4. Validate result presence
            if (response.Result == null)
            {
                _logger.LogWarning(
                    "Python agent service returned success=true without result payload for request {RequestId}",
                    requestDto.RequestId);

                return new AgentResult
                {
                    Success = false,
                    Message = "Python agent service response was missing the authoritative result."
                };
            }

            var rawResult = response.Result;

            // 5. Authoritative validation
            // 5a. Canonical category validation
            if (string.IsNullOrWhiteSpace(rawResult.Category) || !AllowedCategories.Contains(rawResult.Category.Trim()))
            {
                _logger.LogWarning(
                    "Python agent service returned unrecognized category '{Category}' for request {RequestId}",
                    rawResult.Category,
                    requestDto.RequestId);

                return new AgentResult
                {
                    Success = false,
                    Message = $"Agent produced unrecognized category: '{rawResult.Category}'."
                };
            }

            // Normalize category casing
            var canonicalCategory = AllowedCategories.First(c => c.Equals(rawResult.Category.Trim(), StringComparison.OrdinalIgnoreCase));

            // 5b. Urgency parsing
            if (!Enum.TryParse<ServiceRequestUrgency>(rawResult.Urgency, ignoreCase: true, out var urgency))
            {
                _logger.LogWarning(
                    "Python agent service returned unrecognized urgency '{Urgency}' for request {RequestId}",
                    rawResult.Urgency,
                    requestDto.RequestId);

                return new AgentResult
                {
                    Success = false,
                    Message = $"Agent produced unrecognized urgency level: '{rawResult.Urgency}'."
                };
            }

            // 5c. Confidence range validation [0, 1]
            if (rawResult.Confidence < 0.0m || rawResult.Confidence > 1.0m)
            {
                _logger.LogWarning(
                    "Python agent service returned invalid confidence {Confidence} outside [0, 1] for request {RequestId}",
                    rawResult.Confidence,
                    requestDto.RequestId);

                return new AgentResult
                {
                    Success = false,
                    Message = $"Agent produced invalid confidence score: {rawResult.Confidence}."
                };
            }

            // 5d. Problem summary non-empty validation
            if (string.IsNullOrWhiteSpace(rawResult.ProblemSummary))
            {
                _logger.LogWarning(
                    "Python agent service returned empty problemSummary for request {RequestId}",
                    requestDto.RequestId);

                return new AgentResult
                {
                    Success = false,
                    Message = "Agent produced an empty problem summary."
                };
            }

            // 5e. Follow-up questions validation & bounds (max 3, max length 500)
            var sanitizedQuestions = (rawResult.FollowUpQuestions ?? new List<string>())
                .Where(q => !string.IsNullOrWhiteSpace(q))
                .Select(q => q.Trim().Length <= 500 ? q.Trim() : q.Trim().Substring(0, 500))
                .Take(3)
                .ToList();

            // 6. Map safe metadata into AgentContext and output
            var toolCount = response.Metadata?.ToolExecutions?.Count ?? 0;
            context.Data["ToolCalls"] = toolCount;
            context.Data["ToolCallCount"] = toolCount;

            var isDegraded = response.Metadata?.Degraded ?? false;
            context.Data["Degraded"] = isDegraded;

            if (!string.IsNullOrWhiteSpace(response.Metadata?.Provider))
            {
                context.Data["Provider"] = response.Metadata.Provider;
            }

            if (response.Metadata?.DurationMs > 0)
            {
                context.Data["AgentDurationMs"] = response.Metadata.DurationMs;
            }

            // Prepare safe additional information
            var additionalInfo = new Dictionary<string, string>(
                rawResult.AdditionalInformation ?? new Dictionary<string, string>());

            if (isDegraded && !additionalInfo.ContainsKey("Degraded"))
            {
                additionalInfo["Degraded"] = "True";
            }

            if (!string.IsNullOrWhiteSpace(response.Metadata?.Provider) && !additionalInfo.ContainsKey("Provider"))
            {
                additionalInfo["Provider"] = response.Metadata.Provider;
            }

            // 7. Assemble final domain output
            var output = new ProblemUnderstandingOutput
            {
                Category = canonicalCategory,
                ProblemSummary = rawResult.ProblemSummary.Trim(),
                Urgency = urgency,
                NeedsMoreInformation = rawResult.NeedsMoreInformation,
                FollowUpQuestions = sanitizedQuestions,
                Confidence = rawResult.Confidence,
                ExtractedLocation = rawResult.ExtractedLocation,
                AdditionalInformation = additionalInfo
            };

            return new AgentResult
            {
                Success = true,
                Message = output.NeedsMoreInformation
                    ? "Problem analysis incomplete. Additional information required."
                    : "Problem analysis completed.",
                Data = output,
                NextAction = output.NeedsMoreInformation
                    ? "AwaitingInformation"
                    : "Analysed"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected failure during external agent adapter execution for workflow {WorkflowId}: {Message}",
                context.WorkflowId,
                ex.Message);

            // Strict rule: Never attempt automatic fallback to C# agent on Python failure.
            // Return safe AgentResult failure and allow workflow recovery to restore state.
            return new AgentResult
            {
                Success = false,
                Message = $"External agent execution failed: {ex.Message}"
            };
        }
    }

    private static ProblemUnderstandingInput? ResolveInput(AgentContext context)
    {
        if (context.Data.TryGetValue(nameof(ProblemUnderstandingInput), out var raw) &&
            raw is ProblemUnderstandingInput typed)
        {
            return typed;
        }

        return null;
    }
}
