class ForwardGeocodeCandidate {
  final String displayAddress;
  final double latitude;
  final double longitude;
  final String? placeId;
  final String source;

  const ForwardGeocodeCandidate({
    required this.displayAddress,
    required this.latitude,
    required this.longitude,
    this.placeId,
    this.source = 'OpenStreetMap',
  });

  factory ForwardGeocodeCandidate.fromJson(Map<String, dynamic> json) {
    return ForwardGeocodeCandidate(
      displayAddress: (json['displayAddress'] as String?)?.trim() ?? '',
      latitude: (json['latitude'] as num).toDouble(),
      longitude: (json['longitude'] as num).toDouble(),
      placeId: json['placeId']?.toString(),
      source: (json['source'] as String?) ?? 'OpenStreetMap',
    );
  }

  Map<String, dynamic> toJson() => {
    'displayAddress': displayAddress,
    'latitude': latitude,
    'longitude': longitude,
    if (placeId != null) 'placeId': placeId,
    'source': source,
  };
}
