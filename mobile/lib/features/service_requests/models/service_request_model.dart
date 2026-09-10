import 'service_request_status.dart';
import 'service_request_urgency.dart';

class ServiceRequestModel {
  final String serviceRequestId;
  final String customerId;
  final String category;
  final String description;
  final String locationText;
  final double? latitude;
  final double? longitude;
  final ServiceRequestUrgency urgency;
  final ServiceRequestStatus status;
  final DateTime createdAt;
  final DateTime updatedAt;

  const ServiceRequestModel({
    required this.serviceRequestId,
    required this.customerId,
    required this.category,
    required this.description,
    required this.locationText,
    this.latitude,
    this.longitude,
    required this.urgency,
    required this.status,
    required this.createdAt,
    required this.updatedAt,
  });

  factory ServiceRequestModel.fromJson(Map<String, dynamic> json) {
    return ServiceRequestModel(
      serviceRequestId: json['serviceRequestId']?.toString() ?? '',
      customerId: json['customerId']?.toString() ?? '',
      category: json['category'] as String? ?? 'Unclassified',
      description: json['description'] as String? ?? '',
      locationText: json['locationText'] as String? ?? '',
      latitude: json['latitude'] != null ? (json['latitude'] as num).toDouble() : null,
      longitude: json['longitude'] != null ? (json['longitude'] as num).toDouble() : null,
      urgency: ServiceRequestUrgency.fromJson(json['urgency']),
      status: ServiceRequestStatus.fromJson(json['status']),
      createdAt: json['createdAt'] != null
          ? DateTime.parse(json['createdAt'] as String)
          : DateTime.now(),
      updatedAt: json['updatedAt'] != null
          ? DateTime.parse(json['updatedAt'] as String)
          : DateTime.now(),
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'serviceRequestId': serviceRequestId,
      'customerId': customerId,
      'category': category,
      'description': description,
      'locationText': locationText,
      'latitude': latitude,
      'longitude': longitude,
      'urgency': urgency.toJson(),
      'status': status.toJson(),
      'createdAt': createdAt.toIso8601String(),
      'updatedAt': updatedAt.toIso8601String(),
    };
  }

  ServiceRequestModel copyWith({
    String? serviceRequestId,
    String? customerId,
    String? category,
    String? description,
    String? locationText,
    double? latitude,
    double? longitude,
    ServiceRequestUrgency? urgency,
    ServiceRequestStatus? status,
    DateTime? createdAt,
    DateTime? updatedAt,
  }) {
    return ServiceRequestModel(
      serviceRequestId: serviceRequestId ?? this.serviceRequestId,
      customerId: customerId ?? this.customerId,
      category: category ?? this.category,
      description: description ?? this.description,
      locationText: locationText ?? this.locationText,
      latitude: latitude ?? this.latitude,
      longitude: longitude ?? this.longitude,
      urgency: urgency ?? this.urgency,
      status: status ?? this.status,
      createdAt: createdAt ?? this.createdAt,
      updatedAt: updatedAt ?? this.updatedAt,
    );
  }
}
