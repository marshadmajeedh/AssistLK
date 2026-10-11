namespace AssistLK.Application.Interfaces;

public interface ITrackingAccessService
{
    Task<TrackingAccessResult> ValidateAsync(
        Guid jobId,
        Guid userId,
        CancellationToken cancellationToken = default);
}

public sealed record TrackingAccessResult(
    bool HasAccess,
    bool IsOnTheWay);