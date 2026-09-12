"""LocationExtractionTool - Deterministic geospatial coordinate and text normalizer."""
from dataclasses import dataclass


@dataclass(frozen=True)
class LocationExtractionData:
    """Output data returned by LocationExtractionTool."""

    normalized_location: str | None
    latitude: float | None
    longitude: float | None
    has_coordinates: bool


def extract_location_tool(
    location_text: str | None,
    latitude: float | None,
    longitude: float | None,
) -> LocationExtractionData:
    """
    Extracts and normalizes location text and geographic coordinates.

    Rules:
    - Normalizes non-empty string by stripping whitespace.
    - Validates latitude in [-90, 90] and longitude in [-180, 180].
    - Both coordinates must be present and in valid range.
    - Partial or out-of-range coordinates are discarded.
    - Never invents coordinates from free-form text.
    """
    normalized_location: str | None = None
    if location_text is not None:
        trimmed = location_text.strip()
        if trimmed:
            normalized_location = trimmed

    has_coordinates = False
    valid_lat: float | None = None
    valid_lon: float | None = None

    if latitude is not None and longitude is not None:
        try:
            lat = float(latitude)
            lon = float(longitude)
            if -90.0 <= lat <= 90.0 and -180.0 <= lon <= 180.0:
                valid_lat = round(lat, 6)
                valid_lon = round(lon, 6)
                has_coordinates = True
        except (ValueError, TypeError):
            pass

    return LocationExtractionData(
        normalized_location=normalized_location,
        latitude=valid_lat,
        longitude=valid_lon,
        has_coordinates=has_coordinates,
    )
