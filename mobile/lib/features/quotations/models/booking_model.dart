/// Confirmed booking returned by POST /api/quotations/{id}/approve.
class BookingModel {
  final int id;
  final int quotationId;
  final String customerId;
  final String providerId;
  final String status; // "Confirmed" (extended enum values possible)
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

/// Summary row returned by GET /api/provider/bookings.
/// Used to render the provider's "My Bookings" tab.
class ProviderBookingSummaryModel {
  final String id;
  /// The service request that this booking/quotation belongs to.
  /// Needed to create a quotation when the row is a "Pending Quote".
  final String? serviceRequestId;
  final String providerId;
  final String customerName;
  final String serviceTitle;
  final String locationText;
  final double amount;
  final String statusLabel;
  final int statusCode;
  final DateTime? scheduledAt;
  final bool isCompleted;
  final bool isPendingQuote;

  const ProviderBookingSummaryModel({
    required this.id,
    this.serviceRequestId,
    required this.providerId,
    required this.customerName,
    required this.serviceTitle,
    required this.locationText,
    required this.amount,
    required this.statusLabel,
    required this.statusCode,
    this.scheduledAt,
    required this.isCompleted,
    required this.isPendingQuote,
  });

  factory ProviderBookingSummaryModel.fromJson(Map<String, dynamic> json) {
    return ProviderBookingSummaryModel(
      id: json['id']?.toString() ?? '',
      serviceRequestId: json['serviceRequestId']?.toString(),
      providerId: json['providerId']?.toString() ?? '',
      customerName: json['customerName']?.toString() ?? 'Customer',
      serviceTitle: json['serviceTitle']?.toString() ?? '',
      locationText: json['locationText']?.toString() ?? '',
      amount: (json['amount'] as num?)?.toDouble() ?? 0.0,
      statusLabel: json['statusLabel']?.toString() ?? 'CONFIRMED',
      statusCode: (json['statusCode'] as num?)?.toInt() ?? 0,
      scheduledAt: json['scheduledAt'] != null
          ? DateTime.tryParse(json['scheduledAt'].toString())
          : null,
      isCompleted: json['isCompleted'] as bool? ?? false,
      isPendingQuote: json['isPendingQuote'] as bool? ?? false,
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'serviceRequestId': serviceRequestId,
        'providerId': providerId,
        'customerName': customerName,
        'serviceTitle': serviceTitle,
        'locationText': locationText,
        'amount': amount,
        'statusLabel': statusLabel,
        'statusCode': statusCode,
        'scheduledAt': scheduledAt?.toIso8601String(),
        'isCompleted': isCompleted,
        'isPendingQuote': isPendingQuote,
      };
}