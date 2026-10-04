/// Confirmed booking returned by POST /api/quotations/{id}/approve.
class BookingModel {
  final int id;
  final int quotationId;
  final String customerId;
  final String providerId;
  final String status;         // "Confirmed" (extended enum values possible)
  final DateTime scheduledAt;
  final String? locationText;
  final double? latitude;
  final double? longitude;
  final DateTime createdAt;
  final DateTime updatedAt;

  const BookingModel({
    required this.id,
    required this.quotationId,
    required this.customerId,
    required this.providerId,
    required this.status,
    required this.scheduledAt,
    this.locationText,
    this.latitude,
    this.longitude,
    required this.createdAt,
    required this.updatedAt,
  });

  factory BookingModel.fromJson(Map<String, dynamic> json) {
    return BookingModel(
      id: (json['id'] as num?)?.toInt() ?? 0,
      quotationId: (json['quotationId'] as num?)?.toInt() ?? 0,
      customerId: json['customerId']?.toString() ?? '',
      providerId: json['providerId']?.toString() ?? '',
      status: json['status'] as String? ?? 'Confirmed',
      scheduledAt: json['scheduledAt'] != null
          ? DateTime.parse(json['scheduledAt'] as String)
          : DateTime.now(),
      locationText: json['locationText'] as String?,
      latitude: (json['latitude'] as num?)?.toDouble(),
      longitude: (json['longitude'] as num?)?.toDouble(),
      createdAt: json['createdAt'] != null
          ? DateTime.parse(json['createdAt'] as String)
          : DateTime.now(),
      updatedAt: json['updatedAt'] != null
          ? DateTime.parse(json['updatedAt'] as String)
          : DateTime.now(),
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'quotationId': quotationId,
        'customerId': customerId,
        'providerId': providerId,
        'status': status,
        'scheduledAt': scheduledAt.toIso8601String(),
        'locationText': locationText,
        'latitude': latitude,
        'longitude': longitude,
        'createdAt': createdAt.toIso8601String(),
        'updatedAt': updatedAt.toIso8601String(),
      };
}