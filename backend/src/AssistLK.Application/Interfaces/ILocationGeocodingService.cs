using AssistLK.Application.Locations.DTOs;

namespace AssistLK.Application.Interfaces;

public interface ILocationGeocodingService
{
    // Null means no useful address. This operation never persists location data.
    Task<ReverseGeocodeResponse?> ReverseGeocodeAsync(
        decimal latitude, decimal longitude, CancellationToken cancellationToken = default);
}
