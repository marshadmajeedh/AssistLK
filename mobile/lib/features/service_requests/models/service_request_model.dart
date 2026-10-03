import 'location_source.dart';
import 'completion_record_model.dart';
import 'problem_analysis_summary_model.dart';
import 'service_request_clarification_model.dart';
import 'service_request_status.dart';
import 'service_request_urgency.dart';

class ServiceRequestModel {
  static const Object _sentinel = Object();

  final String serviceRequestId;
  final String? serviceJobId;
  final String customerId;
  final String? categoryHint;
  final String category;
  final String description;
  final String locationText;
  final LocationSource locationSource;
  final double? latitude;
  final double? longitude;
  final ServiceRequestUrgency urgency;
  final ServiceRequestStatus status;
  final DateTime? readyForMatchingAtUtc;
  final DateTime? matchingExpiresAtUtc;
  final bool isMatchingEligible;
  final DateTime createdAt;
  final DateTime updatedAt;
  final ProblemAnalysisSummaryModel? latestAnalysis;
  final CompletionRecordModel? completionRecord;
  final bool hasFeedback;
  final int? feedbackRating;
  final String? feedbackComment;
  final List<ServiceRequestClarificationModel> clarifications;

  const ServiceRequestModel({
    required this.serviceRequestId,
    this.serviceJobId,
    required this.customerId,
    this.categoryHint,
    required this.category,
    required this.description,
    required this.locationText,
    this.locationSource = LocationSource.manual,
    this.latitude,
    this.longitude,
    required this.urgency,
    required this.status,
    this.readyForMatchingAtUtc,
    this.matchingExpiresAtUtc,
    this.isMatchingEligible = false,
    required this.createdAt,
    required this.updatedAt,
    this.latestAnalysis,
    this.completionRecord,
    this.hasFeedback = false,
    this.feedbackRating,
    this.feedbackComment,
    this.clarifications = const [],
  });

  bool get isMatchingExpired {
    if (status != ServiceRequestStatus.readyForMatching) return false;
    if (matchingExpiresAtUtc == null) return false;
    return DateTime.now().toUtc().isAfter(matchingExpiresAtUtc!);
  }

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
        analyzedAt == null ||
        pendingQuestions.isNotEmpty) {
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
      serviceJobId: json['serviceJobId']?.toString(),
      customerId: json['customerId']?.toString() ?? '',
      categoryHint: json['categoryHint'] as String?,
      category: json['category'] as String? ?? 'Unclassified',
      description: json['description'] as String? ?? '',
      locationSource: LocationSource.fromJson(json['locationSource']),
      locationText: json['locationText'] as String? ?? '',
      latitude: json['latitude'] != null
          ? (json['latitude'] as num).toDouble()
          : null,
      longitude: json['longitude'] != null
          ? (json['longitude'] as num).toDouble()
          : null,
      urgency: ServiceRequestUrgency.fromJson(json['urgency']),
      status: ServiceRequestStatus.fromJson(json['status']),
      readyForMatchingAtUtc: json['readyForMatchingAtUtc'] != null
          ? DateTime.parse(json['readyForMatchingAtUtc'] as String)
          : null,
      matchingExpiresAtUtc: json['matchingExpiresAtUtc'] != null
          ? DateTime.parse(json['matchingExpiresAtUtc'] as String)
          : null,
      isMatchingEligible: json['isMatchingEligible'] as bool? ?? false,
      createdAt: json['createdAt'] != null
          ? DateTime.parse(json['createdAt'] as String)
          : DateTime.now(),
      updatedAt: json['updatedAt'] != null
          ? DateTime.parse(json['updatedAt'] as String)
          : DateTime.now(),
      latestAnalysis: json['latestAnalysis'] != null
          ? ProblemAnalysisSummaryModel.fromJson(
              Map<String, dynamic>.from(json['latestAnalysis'] as Map),
            )
          : null,
      completionRecord: json['completionRecord'] != null
          ? CompletionRecordModel.fromJson(
              Map<String, dynamic>.from(json['completionRecord'] as Map),
            )
          : null,
          hasFeedback: json['hasFeedback'] == true,
          feedbackRating: (json['feedbackRating'] as num?)?.toInt(),
          feedbackComment: json['feedbackComment'] as String?,
      clarifications:
          (json['clarifications'] as List<dynamic>?)
              ?.map(
                (e) => ServiceRequestClarificationModel.fromJson(
                  Map<String, dynamic>.from(e as Map),
                ),
              )
              .toList() ??
          const [],
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'serviceRequestId': serviceRequestId,
      'serviceJobId': serviceJobId,
      'customerId': customerId,
      'categoryHint': categoryHint,
      'category': category,
      'description': description,
      'locationSource': locationSource.value,
      'locationText': locationText,
      'latitude': latitude,
      'longitude': longitude,
      'urgency': urgency.toJson(),
      'status': status.toJson(),
      'readyForMatchingAtUtc': readyForMatchingAtUtc?.toIso8601String(),
      'matchingExpiresAtUtc': matchingExpiresAtUtc?.toIso8601String(),
      'isMatchingEligible': isMatchingEligible,
      'createdAt': createdAt.toIso8601String(),
      'updatedAt': updatedAt.toIso8601String(),
      'latestAnalysis': latestAnalysis?.toJson(),
      'completionRecord': completionRecord == null
          ? null
          : {
              'proofOfWorkImageUrl': completionRecord!.proofOfWorkImageUrl,
              'summaryNotes': completionRecord!.summaryNotes,
            },
      'hasFeedback': hasFeedback,
      'feedbackRating': feedbackRating,
      'feedbackComment': feedbackComment,
      'clarifications': clarifications.map((c) => c.toJson()).toList(),
    };
  }

  ServiceRequestModel copyWith({
    String? serviceRequestId,
    String? serviceJobId,
    String? customerId,
    Object? categoryHint = _sentinel,
    String? category,
    String? description,
    String? locationText,
    LocationSource? locationSource,
    double? latitude,
    double? longitude,
    ServiceRequestUrgency? urgency,
    ServiceRequestStatus? status,
    Object? readyForMatchingAtUtc = _sentinel,
    Object? matchingExpiresAtUtc = _sentinel,
    bool? isMatchingEligible,
    DateTime? createdAt,
    DateTime? updatedAt,
    Object? latestAnalysis = _sentinel,
    Object? completionRecord = _sentinel,
    bool? hasFeedback,
    Object? feedbackRating = _sentinel,
    Object? feedbackComment = _sentinel,
    List<ServiceRequestClarificationModel>? clarifications,
  }) {
    return ServiceRequestModel(
      serviceRequestId: serviceRequestId ?? this.serviceRequestId,
      serviceJobId: serviceJobId ?? this.serviceJobId,
      customerId: customerId ?? this.customerId,
      categoryHint: identical(categoryHint, _sentinel)
          ? this.categoryHint
          : categoryHint as String?,
      category: category ?? this.category,
      description: description ?? this.description,
      locationText: locationText ?? this.locationText,
      locationSource: locationSource ?? this.locationSource,
      latitude: latitude ?? this.latitude,
      longitude: longitude ?? this.longitude,
      urgency: urgency ?? this.urgency,
      status: status ?? this.status,
      readyForMatchingAtUtc: identical(readyForMatchingAtUtc, _sentinel)
          ? this.readyForMatchingAtUtc
          : readyForMatchingAtUtc as DateTime?,
      matchingExpiresAtUtc: identical(matchingExpiresAtUtc, _sentinel)
          ? this.matchingExpiresAtUtc
          : matchingExpiresAtUtc as DateTime?,
      isMatchingEligible: isMatchingEligible ?? this.isMatchingEligible,
      createdAt: createdAt ?? this.createdAt,
      updatedAt: updatedAt ?? this.updatedAt,
      latestAnalysis: identical(latestAnalysis, _sentinel)
          ? this.latestAnalysis
          : latestAnalysis as ProblemAnalysisSummaryModel?,
      completionRecord: identical(completionRecord, _sentinel)
          ? this.completionRecord
          : completionRecord as CompletionRecordModel?,
      hasFeedback: hasFeedback ?? this.hasFeedback,
      feedbackRating: identical(feedbackRating, _sentinel)
          ? this.feedbackRating
          : feedbackRating as int?,
      feedbackComment: identical(feedbackComment, _sentinel)
          ? this.feedbackComment
          : feedbackComment as String?,
      clarifications: clarifications ?? this.clarifications,
    );
  }
}
