using System.Globalization;
using AssistLK.Agents.Abstractions;

namespace AssistLK.Agents.Tools;

public sealed record LocationExtractionData(
    string? NormalizedLocation,
    decimal? Latitude,
    decimal? Longitude,
    bool HasCoordinates);

/// <summary>
/// Tool to extract and normalize location information and coordinates.
/// Does NOT invent coordinates from free-form text.
/// </summary>
public sealed class LocationExtractionTool : IAgentTool
{
    public string Name => "LocationExtractionTool";

    public string Description =>
        "Extracts and normalizes location information and geographic coordinates from request input.";

    public Task<ToolResult> ExecuteAsync(
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        string? normalizedLocation = null;
        if (parameters.TryGetValue("locationText", out var locObj) && locObj != null)
        {
            var locStr = locObj.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(locStr))
            {
                normalizedLocation = locStr;
            }
        }

        decimal? latitude = TryParseDecimal(parameters, "latitude");
        decimal? longitude = TryParseDecimal(parameters, "longitude");

        bool hasCoordinates = false;

        // If coordinates are provided, validate ranges (-90 to 90 for lat, -180 to 180 for lon).
        // Must have both to be valid coordinates.
        if (latitude.HasValue && longitude.HasValue)
        {
            if (latitude.Value >= -90m && latitude.Value <= 90m &&
                longitude.Value >= -180m && longitude.Value <= 180m)
            {
                hasCoordinates = true;
            }
            else
            {
                // Invalid ranges: discard rather than inventing or keeping corrupted coordinates
                latitude = null;
                longitude = null;
            }
        }
        else
        {
            // Partial coordinates are discarded
            latitude = null;
            longitude = null;
        }

        return Task.FromResult(new ToolResult
        {
            Success = true,
            Message = "Location extracted.",
            Data = new LocationExtractionData(
                NormalizedLocation: normalizedLocation,
                Latitude: latitude,
                Longitude: longitude,
                HasCoordinates: hasCoordinates)
        });
    }

    private static decimal? TryParseDecimal(Dictionary<string, object> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out var val) || val == null)
            return null;

        if (val is decimal dec)
            return dec;

        if (val is double dbl)
            return (decimal)dbl;

        if (val is float flt)
            return (decimal)flt;

        if (val is int i)
            return (decimal)i;

        if (val is long l)
            return (decimal)l;

        if (decimal.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return null;
    }
}
