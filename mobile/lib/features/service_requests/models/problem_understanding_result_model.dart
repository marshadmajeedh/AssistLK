import 'service_request_status.dart';
import 'service_request_urgency.dart';

class ProblemUnderstandingResultModel {
  final String workflowId;
  final String executionId;
  final String serviceRequestId;
  final ServiceRequestStatus status;
  final String category;
  final String problemSummary;
  final ServiceRequestUrgency urgency;
  final double confidence;
  final bool needsMoreInformation;
  // Backend currently does not persist followUpQuestions.
  // Stored temporarily until customer completes clarification.
  final List<String> followUpQuestions;

  const ProblemUnderstandingResultModel({
    required this.workflowId,
    required this.executionId,
    required this.serviceRequestId,
    required this.status,
    required this.category,
    required this.problemSummary,
    required this.urgency,
    required this.confidence,
    required this.needsMoreInformation,
    required this.followUpQuestions,
  });

  factory ProblemUnderstandingResultModel.fromJson(Map<String, dynamic> json) {
    return ProblemUnderstandingResultModel(
      workflowId: json['workflowId']?.toString() ?? '',
      executionId: json['executionId']?.toString() ?? '',
      serviceRequestId: json['serviceRequestId']?.toString() ?? '',
      status: ServiceRequestStatus.fromJson(json['status']),
      category: json['category'] as String? ?? 'Unclassified',
      problemSummary: json['problemSummary'] as String? ?? '',
      urgency: ServiceRequestUrgency.fromJson(json['urgency']),
      confidence: json['confidence'] != null
          ? (json['confidence'] as num).toDouble()
          : 0.0,
      needsMoreInformation: json['needsMoreInformation'] as bool? ?? false,
      followUpQuestions: (json['followUpQuestions'] as List<dynamic>?)
              ?.map((e) => e.toString())
              .toList() ??
          const [],
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'workflowId': workflowId,
      'executionId': executionId,
      'serviceRequestId': serviceRequestId,
      'status': status.toJson(),
      'category': category,
      'problemSummary': problemSummary,
      'urgency': urgency.toJson(),
      'confidence': confidence,
      'needsMoreInformation': needsMoreInformation,
      'followUpQuestions': followUpQuestions,
    };
  }

  ProblemUnderstandingResultModel copyWith({
    String? workflowId,
    String? executionId,
    String? serviceRequestId,
    ServiceRequestStatus? status,
    String? category,
    String? problemSummary,
    ServiceRequestUrgency? urgency,
    double? confidence,
    bool? needsMoreInformation,
    List<String>? followUpQuestions,
  }) {
    return ProblemUnderstandingResultModel(
      workflowId: workflowId ?? this.workflowId,
      executionId: executionId ?? this.executionId,
      serviceRequestId: serviceRequestId ?? this.serviceRequestId,
      status: status ?? this.status,
      category: category ?? this.category,
      problemSummary: problemSummary ?? this.problemSummary,
      urgency: urgency ?? this.urgency,
      confidence: confidence ?? this.confidence,
      needsMoreInformation: needsMoreInformation ?? this.needsMoreInformation,
      followUpQuestions: followUpQuestions ?? this.followUpQuestions,
    );
  }
}
