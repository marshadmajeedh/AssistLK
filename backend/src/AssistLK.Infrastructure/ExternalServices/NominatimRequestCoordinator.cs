using System.Diagnostics;
using AssistLK.Application.Locations.DTOs;

namespace AssistLK.Infrastructure.ExternalServices;

/// <summary>
/// Singleton shared by every typed client in the backend. The university demo must
/// run ONE backend process; multiple replicas require a shared external limiter.
/// Public policy: https://operations.osmfoundation.org/policies/nominatim/
/// </summary>
public sealed class NominatimRequestCoordinator : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, (long SavedAt, ReverseGeocodeResponse? Result)> _cache = new();
    private long? _lastCompleted;

    public async Task<ReverseGeocodeResponse?> ExecuteAsync(string key,
        Func<Task<ReverseGeocodeResponse?>> send, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_cache.TryGetValue(key, out var cached) &&
                Stopwatch.GetElapsedTime(cached.SavedAt) < TimeSpan.FromMinutes(15))
                return cached.Result;

            // Wait after COMPLETION (including failures), not merely reservation.
            // This conservatively guarantees >=1s between actual outbound starts.
            while (_lastCompleted is { } completed)
            {
                var remaining = TimeSpan.FromSeconds(1) - Stopwatch.GetElapsedTime(completed);
                if (remaining <= TimeSpan.Zero) break;
                await Task.Delay(remaining, cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            ReverseGeocodeResponse? result;
            try { result = await send(); }
            finally { _lastCompleted = Stopwatch.GetTimestamp(); }

            // Exact-coordinate preview cache only; no raw JSON, persistence, prefetch,
            // periodic refresh or failure caching. Bound memory for the demo workload.
            if (_cache.Count >= 128) _cache.Remove(_cache.MinBy(entry => entry.Value.SavedAt).Key);
            _cache[key] = (Stopwatch.GetTimestamp(), result);
            return result;
        }
        finally { _gate.Release(); }
    }

    public void Dispose() => _gate.Dispose();
}
