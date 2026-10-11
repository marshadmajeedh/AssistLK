namespace AssistLK.Application.Locations.DTOs;

public record ForwardGeocodeCandidate(
    string DisplayAddress,
    decimal Latitude,
    decimal Longitude,
    string? PlaceId = null,
    string Source = "OpenStreetMap");
