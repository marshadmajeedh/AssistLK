using System;
using System.Threading;
using System.Threading.Tasks;

namespace AssistLK.Application.Services.Providers;

public class MatchingExecutionResult
{
    public bool Success { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ThreadId { get; set; }
    public string? Message { get; set; }
    public Guid? MatchingExecutionId { get; set; }
    public TokenUsageDto? TokensConsumed { get; set; }
}

public interface IProviderMatchingCoordinator
{
    Task<MatchingExecutionResult> ExecuteMatchForRequestAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default);
}
