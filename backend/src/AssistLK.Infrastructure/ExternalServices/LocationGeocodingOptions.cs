using System.Net.Http.Headers;

namespace AssistLK.Infrastructure.ExternalServices;

public sealed class LocationGeocodingOptions
{
    public const string SectionName = "LocationGeocoding";
    public string Provider { get; set; } = "Nominatim";
    public string NominatimBaseUrl { get; set; } = "https://nominatim.openstreetmap.org";
    public int TimeoutSeconds { get; set; } = 5;
    public string UserAgent { get; set; } = "AssistLK-SE3090/1.0";

    public void Validate()
    {
        if (Provider != "Nominatim")
            throw new InvalidOperationException("Unsupported LocationGeocoding provider.");
        if (!Uri.TryCreate(NominatimBaseUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length != 0 ||
            uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new InvalidOperationException("LocationGeocoding base URL must be HTTPS without credentials, query or fragment.");
        if (TimeoutSeconds is < 1 or > 30)
            throw new InvalidOperationException("LocationGeocoding timeout must be between 1 and 30 seconds.");
        if (string.IsNullOrWhiteSpace(UserAgent) || !UserAgent.StartsWith("AssistLK", StringComparison.Ordinal) ||
            !ProductInfoHeaderValue.TryParse(UserAgent, out var product) || product.Product?.Version == null)
            throw new InvalidOperationException("LocationGeocoding UserAgent must identify AssistLK with a product version.");
    }
}
