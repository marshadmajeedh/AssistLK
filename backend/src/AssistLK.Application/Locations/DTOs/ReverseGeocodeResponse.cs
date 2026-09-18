namespace AssistLK.Application.Locations.DTOs;

public record ReverseGeocodeResponse(
    string FormattedAddress,
    string? Street,
    string? Neighborhood,
    string? City,
    string? Province,
    string? PostalCode,
    string? Country,
    string? PlaceId,
    string? ResolutionLevel);
