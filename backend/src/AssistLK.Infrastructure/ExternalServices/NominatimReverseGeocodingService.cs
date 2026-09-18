using System.Globalization;
using System.Net;
using System.Text.Json;
using AssistLK.Application.Common.Exceptions;
using AssistLK.Application.Interfaces;
using AssistLK.Application.Locations.DTOs;

namespace AssistLK.Infrastructure.ExternalServices;

public sealed class NominatimReverseGeocodingService(HttpClient client,
    LocationGeocodingOptions options, NominatimRequestCoordinator coordinator) : ILocationGeocodingService
{
    public async Task<ReverseGeocodeResponse?> ReverseGeocodeAsync(
        decimal latitude, decimal longitude, CancellationToken cancellationToken = default)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
            throw new ArgumentException("Coordinates are outside the valid range.");
        var url = options.NominatimBaseUrl.TrimEnd('/') + "/reverse?lat=" +
            latitude.ToString("G29", CultureInfo.InvariantCulture) + "&lon=" +
            longitude.ToString("G29", CultureInfo.InvariantCulture) + "&format=jsonv2&addressdetails=1";
        return await coordinator.ExecuteAsync(url, async () =>
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd(options.UserAgent);
                // One attempt only. All requests, including failures, share the limiter.
                using var response = await client.SendAsync(request, cancellationToken);
                if (response.StatusCode == HttpStatusCode.NotFound) return null;
                if (!response.IsSuccessStatusCode) throw new LocationGeocodingUnavailableException();
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object) throw new JsonException();
                if (root.TryGetProperty("error", out _)) return null;
                var formatted = Text(root, "display_name");
                if (formatted == null) return null;
                var address = root.TryGetProperty("address", out var value) ? value : default;
                if (address.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null or JsonValueKind.Object))
                    throw new JsonException();
                string? Part(params string[] names) => address.ValueKind == JsonValueKind.Object
                    ? names.Select(name => Text(address, name)).FirstOrDefault(part => part != null) : null;
                // Ordered field-name fallbacks, independent of JSON property order.
                // County is deliberately not promoted to city; all parts are optional.
                return new ReverseGeocodeResponse(formatted,
                    Part("road", "pedestrian", "residential"),
                    Part("neighbourhood", "suburb", "quarter"),
                    Part("city", "town", "village", "municipality"),
                    Part("state", "province"), Part("postcode"), Part("country"),
                    // Existing placeId is a transient opaque identifier, never persisted.
                    root.TryGetProperty("place_id", out var id) && id.ValueKind == JsonValueKind.Number
                        ? id.GetInt64().ToString(CultureInfo.InvariantCulture) : Text(root, "place_id"),
                    // Nominatim locates a nearby OSM object, not an accuracy guarantee.
                    "Approximate");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            { throw new LocationGeocodingUnavailableException(); }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or FormatException)
            { throw new LocationGeocodingUnavailableException(); }
        }, cancellationToken);
    }

    private static string? Text(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        var text = value.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
