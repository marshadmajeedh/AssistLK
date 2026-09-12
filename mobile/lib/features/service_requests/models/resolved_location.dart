class ResolvedLocation {
  final String formattedAddress;
  final String? street, neighborhood, city, province, postalCode, country;
  final String? placeId, resolutionLevel;

  const ResolvedLocation({
    required this.formattedAddress,
    this.street,
    this.neighborhood,
    this.city,
    this.province,
    this.postalCode,
    this.country,
    this.placeId,
    this.resolutionLevel,
  });

  factory ResolvedLocation.fromJson(Map<String, dynamic> json) {
    final address = json['formattedAddress'] as String?;
    if (address == null || address.trim().isEmpty) {
      throw const FormatException('No address returned.');
    }
    return ResolvedLocation(
      formattedAddress: address.trim(),
      street: json['street'] as String?,
      neighborhood: json['neighborhood'] as String?,
      city: json['city'] as String?,
      province: json['province'] as String?,
      postalCode: json['postalCode'] as String?,
      country: json['country'] as String?,
      placeId: json['placeId'] as String?,
      resolutionLevel: json['resolutionLevel'] as String?,
    );
  }
}
