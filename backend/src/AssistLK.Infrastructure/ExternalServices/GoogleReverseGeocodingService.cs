using System.Globalization;
using System.Text.Json;
using AssistLK.Application.Common.Exceptions;
using AssistLK.Application.Interfaces;
using AssistLK.Application.Locations.DTOs;

namespace AssistLK.Infrastructure.ExternalServices;

public sealed class GoogleReverseGeocodingService(HttpClient client, GoogleMapsOptions options)
    : ILocationGeocodingService
{
    public async Task<ReverseGeocodeResponse?> ReverseGeocodeAsync(
        decimal latitude, decimal longitude, CancellationToken cancellationToken = default)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
            throw new ArgumentException("Coordinates are outside the valid range.");
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            throw new LocationGeocodingUnavailableException();

        try
        {
            var coordinates = latitude.ToString(CultureInfo.InvariantCulture) + "," +
                longitude.ToString(CultureInfo.InvariantCulture);
            var url = options.ReverseGeocodingBaseUrl + "?latlng=" + coordinates +
                "&key=" + Uri.EscapeDataString(options.ApiKey);
            // One attempt only: no implicit retries or quota amplification for this preview.
            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode) throw new LocationGeocodingUnavailableException();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            var status = Text(root, "status");
            if (status == "ZERO_RESULTS") return null;
            if (status != "OK") throw new LocationGeocodingUnavailableException();
            if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                throw new LocationGeocodingUnavailableException();

            // Preserve Google's result ordering; choose the first nonblank formatted address.
            foreach (var result in results.EnumerateArray())
            {
                var address = Text(result, "formatted_address");
                if (address == null) continue;
                var components = new Dictionary<string, string>();
                if (result.TryGetProperty("address_components", out var items) && items.ValueKind != JsonValueKind.Null)
                    foreach (var item in items.EnumerateArray())
                    {
                        var name = Text(item, "long_name");
                        if (name == null || !item.TryGetProperty("types", out var types)) continue;
                        foreach (var type in types.EnumerateArray())
                            if (type.GetString() is { } key) components.TryAdd(key, name);
                    }
                string? Component(params string[] types) => types.Select(t => components.GetValueOrDefault(t)).FirstOrDefault(v => v != null);
                // Locality then postal town; do not mislabel arbitrary administrative districts as cities.
                // Neighborhood then the smallest supplied sublocality, then generic sublocality.
                var level = result.TryGetProperty("geometry", out var geometry) && geometry.ValueKind == JsonValueKind.Object
                    ? Text(geometry, "location_type") : null;
                level = level switch { "ROOFTOP" => "StreetAddress", "RANGE_INTERPOLATED" => "Interpolated",
                    "GEOMETRIC_CENTER" => "AreaCenter", "APPROXIMATE" => "Approximate", _ => null };
                return new(address, Component("route"),
                    Component("neighborhood", "sublocality_level_5", "sublocality_level_4", "sublocality_level_3", "sublocality_level_2", "sublocality_level_1", "sublocality"),
                    Component("locality", "postal_town"), Component("administrative_area_level_1"),
                    Component("postal_code"), Component("country"), Text(result, "place_id"), level);
            }
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new LocationGeocodingUnavailableException(); }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException)
        { throw new LocationGeocodingUnavailableException(); }
    }

    private static string? Text(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        var text = value.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
