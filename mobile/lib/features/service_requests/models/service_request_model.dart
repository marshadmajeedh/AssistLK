import 'problem_analysis_summary_model.dart';
import 'service_request_clarification_model.dart';
import 'service_request_status.dart';
import 'service_request_urgency.dart';

class ServiceRequestModel {
  static const Object _sentinel = Object();

  final String serviceRequestId;
  final String customerId;
  final String? categoryHint;
  final String category;
  final String description;
  final String locationText;
  final double? latitude;
  final double? longitude;
  final ServiceRequestUrgency urgency;
  final ServiceRequestStatus status;
  final DateTime createdAt;
  final DateTime updatedAt;
  final ProblemAnalysisSummaryModel? latestAnalysis;
  final List<ServiceRequestClarificationModel> clarifications;

  const ServiceRequestModel({
    required this.serviceRequestId,
    required this.customerId,
    this.categoryHint,
    required this.category,
    required this.description,
    required this.locationText,
    this.latitude,
    this.longitude,
    required this.urgency,
    required this.status,
    required this.createdAt,
    required this.updatedAt,
    this.latestAnalysis,
    this.clarifications = const [],
  });

  int get currentClarificationRound => clarifications.isEmpty
      ? 0
      : clarifications
          .map((c) => c.clarificationRound)
          .reduce((a, b) => a > b ? a : b);

  List<ServiceRequestClarificationModel> get currentRoundClarifications =>
      clarifications
          .where((c) => c.clarificationRound == currentClarificationRound)
          .toList();

  bool get currentRoundIsFullyAnswered =>
      currentRoundClarifications.isNotEmpty &&
      currentRoundClarifications.every((c) => c.isAnswered);

  List<ServiceRequestClarificationModel> get pendingQuestions =>
      currentRoundClarifications.where((c) => c.isActionable).toList();

  bool get hasReachedMaxRounds => currentClarificationRound >= 2;

  // Round 2 existing is not proof that its answers have been analyzed.
  // Use persisted server timestamps so reloads and failed attempts are safe.
  bool get hasCompletedFinalClarificationAnalysis {
    final analyzedAt = latestAnalysis?.createdAt;
    if (!hasReachedMaxRounds ||
        status != ServiceRequestStatus.awaitingInformation ||
        analyzedAt == null || pendingQuestions.isNotEmpty) {
      return false;
    }
    return currentRoundClarifications.every((question) {
      final resolvedAt = question.answeredAt ?? question.supersededAt;
      return resolvedAt != null && analyzedAt.isAfter(resolvedAt);
    });
  }

  factory ServiceRequestModel.fromJson(Map<String, dynamic> json) {
    return ServiceRequestModel(
      serviceRequestId: json['serviceRequestId']?.toString() ?? '',
      customerId: json['customerId']?.toString() ?? '',
      categoryHint: json['categoryHint'] as String?,
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
      latestAnalysis: json['latestAnalysis'] != null
          ? ProblemAnalysisSummaryModel.fromJson(
              Map<String, dynamic>.from(json['latestAnalysis'] as Map))
          : null,
      clarifications: (json['clarifications'] as List<dynamic>?)
              ?.map((e) => ServiceRequestClarificationModel.fromJson(
                  Map<String, dynamic>.from(e as Map)))
              .toList() ??
          const [],
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'serviceRequestId': serviceRequestId,
      'customerId': customerId,
      'categoryHint': categoryHint,
      'category': category,
      'description': description,
      'locationText': locationText,
      'latitude': latitude,
      'longitude': longitude,
      'urgency': urgency.toJson(),
      'status': status.toJson(),
      'createdAt': createdAt.toIso8601String(),
      'updatedAt': updatedAt.toIso8601String(),
      'latestAnalysis': latestAnalysis?.toJson(),
      'clarifications': clarifications.map((c) => c.toJson()).toList(),
    };
  }

  ServiceRequestModel copyWith({
    String? serviceRequestId,
    String? customerId,
    Object? categoryHint = _sentinel,
    String? category,
    String? description,
    String? locationText,
    double? latitude,
    double? longitude,
    ServiceRequestUrgency? urgency,
    ServiceRequestStatus? status,
    DateTime? createdAt,
    DateTime? updatedAt,
    Object? latestAnalysis = _sentinel,
    List<ServiceRequestClarificationModel>? clarifications,
  }) {
    return ServiceRequestModel(
      serviceRequestId: serviceRequestId ?? this.serviceRequestId,
      customerId: customerId ?? this.customerId,
      categoryHint: identical(categoryHint, _sentinel)
          ? this.categoryHint
          : categoryHint as String?,
      category: category ?? this.category,
      description: description ?? this.description,
      locationText: locationText ?? this.locationText,
      latitude: latitude ?? this.latitude,
      longitude: longitude ?? this.longitude,
      urgency: urgency ?? this.urgency,
      status: status ?? this.status,
      createdAt: createdAt ?? this.createdAt,
      updatedAt: updatedAt ?? this.updatedAt,
      latestAnalysis: identical(latestAnalysis, _sentinel)
          ? this.latestAnalysis
          : latestAnalysis as ProblemAnalysisSummaryModel?,
      clarifications: clarifications ?? this.clarifications,
    );
  }
}
