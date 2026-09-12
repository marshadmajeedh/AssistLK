namespace AssistLK.Infrastructure.ExternalServices;

public class GoogleMapsOptions
{
    public const string SectionName = "GoogleMaps";
    public string ApiKey { get; set; } = string.Empty;
    public string ReverseGeocodingBaseUrl { get; set; } = "https://maps.googleapis.com/maps/api/geocode/json";
    public int TimeoutSeconds { get; set; } = 5;

    public void Validate()
    {
        // Never send the credential to a configurable arbitrary host or redirect target.
        if (ReverseGeocodingBaseUrl != "https://maps.googleapis.com/maps/api/geocode/json")
            throw new InvalidOperationException("GoogleMaps reverse geocoding URL must be the supported HTTPS Google endpoint.");
        if (TimeoutSeconds is < 1 or > 30)
            throw new InvalidOperationException("GoogleMaps timeout must be between 1 and 30 seconds.");
        // An absent key disables only location preview, not the existing application.
    }
}
