namespace AssistLK.Application.Common.Exceptions;

// Deliberately excludes upstream details and inner exceptions containing request URLs.
public sealed class LocationGeocodingUnavailableException : Exception
{
    public LocationGeocodingUnavailableException()
        : base("Location lookup is temporarily unavailable. Please enter the location manually.") { }
}
