using System.Security.Claims;
using AssistLK.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AssistLK.Api.Hubs;

[Authorize]
public sealed class TrackingHub : Hub
{
    private readonly ITrackingAccessService _trackingAccessService;

    public TrackingHub(ITrackingAccessService trackingAccessService)
    {
        _trackingAccessService = trackingAccessService;
    }

    public async Task JoinJobTrackingGroup(string jobId)
    {
        var jobGuid = ParseJobId(jobId);
        var userId = GetUserId();
        var validation = await _trackingAccessService.ValidateAsync(
            jobGuid,
            userId,
            Context.ConnectionAborted);

        EnsureTrackingIsAllowed(validation);
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GetGroupName(jobGuid),
            Context.ConnectionAborted);
    }

    public async Task UpdateLocation(string jobId, double lat, double lng)
    {
        if (double.IsNaN(lat) || double.IsInfinity(lat) || lat is < -90 or > 90 ||
            double.IsNaN(lng) || double.IsInfinity(lng) || lng is < -180 or > 180)
        {
            throw new HubException("Coordinates are outside the valid range.");
        }

        var jobGuid = ParseJobId(jobId);
        var userId = GetUserId();
        var validation = await _trackingAccessService.ValidateAsync(
            jobGuid,
            userId,
            Context.ConnectionAborted);

        EnsureTrackingIsAllowed(validation);

        await Clients.Group(GetGroupName(jobGuid)).SendAsync(
            "ReceiveLocationUpdate",
            new
            {
                JobId = jobGuid,
                Latitude = lat,
                Longitude = lng,
                UpdatedAtUtc = DateTime.UtcNow
            },
            Context.ConnectionAborted);
    }

    private static string GetGroupName(Guid jobId) => $"Job_{jobId}";

    private static Guid ParseJobId(string jobId)
    {
        if (!Guid.TryParse(jobId, out var parsedJobId))
        {
            throw new HubException("The service job ID is invalid.");
        }

        return parsedJobId;
    }

    private Guid GetUserId()
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId))
        {
            throw new HubException("The authenticated user ID is invalid.");
        }

        return userId;
    }

    private static void EnsureTrackingIsAllowed(TrackingAccessResult validation)
    {
        if (!validation.HasAccess)
        {
            throw new HubException("You are not allowed to track this service job.");
        }

        if (!validation.IsOnTheWay)
        {
            throw new HubException("Live tracking is only available while the job is OnTheWay.");
        }
    }
}