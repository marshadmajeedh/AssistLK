import 'quotation_item_model.dart';
import 'quotation_status.dart';

/// Optional LLM-generated risk assessment returned by the Python agent.
class QuotationRiskAssessment {
  final String? riskLevel;         // "low" | "medium" | "high"
  final double? confidence;        // 0.0 .. 1.0
  final String? rationale;
  final List<String> suggestedConcerns;
  final String? recommendation;    // "approve" | "request_clarification" | "reject"
  final String? model;             // e.g. "gemini-3.8-flash"
  final int? promptTokens;
  final int? completionTokens;
  final int? totalTokens;

  const QuotationRiskAssessment({
    this.riskLevel,
    this.confidence,
    this.rationale,
    this.suggestedConcerns = const [],
    this.recommendation,
    this.model,
    this.promptTokens,
    this.completionTokens,
    this.totalTokens,
  });

  factory QuotationRiskAssessment.fromJson(Map<String, dynamic> json) {
    return QuotationRiskAssessment(
      riskLevel: json['riskLevel'] as String?,
      confidence: (json['confidence'] as num?)?.toDouble(),
      rationale: json['rationale'] as String?,
      suggestedConcerns: (json['suggestedConcerns'] as List<dynamic>?)
              ?.map((e) => e.toString())
              .toList() ??
          const [],
      recommendation: json['recommendation'] as String?,
      model: json['model'] as String?,
      promptTokens: (json['promptTokens'] as num?)?.toInt(),
      completionTokens: (json['completionTokens'] as num?)?.toInt(),
      totalTokens: (json['totalTokens'] as num?)?.toInt(),
    );
  }

  Map<String, dynamic> toJson() => {
        'riskLevel': riskLevel,
        'confidence': confidence,
        'rationale': rationale,
        'suggestedConcerns': suggestedConcerns,
        'recommendation': recommendation,
        'model': model,
        'promptTokens': promptTokens,
        'completionTokens': completionTokens,
        'totalTokens': totalTokens,
      };

  bool get hasData =>
      riskLevel != null || rationale != null || suggestedConcerns.isNotEmpty;
}

/// Full quotation as returned by GET /api/quotations/{id}.
class QuotationModel {
  static const Object _sentinel = Object();

  final int id;
  final String serviceRequestId;
  final String providerId;
  final QuotationStatus status;
  final double totalAmount;
  final List<QuotationItemModel> items;
  final String? workflowThreadId;
  final DateTime createdAt;
  final DateTime updatedAt;

  /// Only populated in the `/send-for-approval` response.
  final QuotationRiskAssessment? riskAssessment;

  const QuotationModel({
    required this.id,
    required this.serviceRequestId,
    required this.providerId,
    required this.status,
    required this.totalAmount,
    required this.items,
    this.workflowThreadId,
    required this.createdAt,
    required this.updatedAt,
    this.riskAssessment,
  });

  bool get isPendingApproval => status.isPendingApproval;

  /// A quotation is approvable only if the workflow thread has been issued
  /// (i.e., the provider has triggered /send-for-approval at least once).
  bool get canApprove => status.isPendingApproval && workflowThreadId != null;

  factory QuotationModel.fromJson(Map<String, dynamic> json) {
    return QuotationModel(
      id: (json['id'] as num?)?.toInt() ?? 0,
      serviceRequestId: json['serviceRequestId']?.toString() ?? '',
      providerId: json['providerId']?.toString() ?? '',
      status: QuotationStatus.fromJson(json['status']),
      totalAmount: (json['totalAmount'] as num?)?.toDouble() ?? 0.0,
      items: (json['items'] as List<dynamic>?)
              ?.map(
                (e) => QuotationItemModel.fromJson(
                  Map<String, dynamic>.from(e as Map),
                ),
              )
              .toList() ??
          const [],
      workflowThreadId: json['workflowThreadId'] as String?,
      createdAt: json['createdAt'] != null
          ? DateTime.parse(json['createdAt'] as String)
          : DateTime.now(),
      updatedAt: json['updatedAt'] != null
          ? DateTime.parse(json['updatedAt'] as String)
          : DateTime.now(),
    );
  }

  QuotationModel copyWith({
    int? id,
    String? serviceRequestId,
    String? providerId,
    QuotationStatus? status,
    double? totalAmount,
    List<QuotationItemModel>? items,
    Object? workflowThreadId = _sentinel,
    DateTime? createdAt,
    DateTime? updatedAt,
    Object? riskAssessment = _sentinel,
  }) {
    return QuotationModel(
      id: id ?? this.id,
      serviceRequestId: serviceRequestId ?? this.serviceRequestId,
      providerId: providerId ?? this.providerId,
      status: status ?? this.status,
      totalAmount: totalAmount ?? this.totalAmount,
      items: items ?? this.items,
      workflowThreadId: identical(workflowThreadId, _sentinel)
          ? this.workflowThreadId
          : workflowThreadId as String?,
      createdAt: createdAt ?? this.createdAt,
      updatedAt: updatedAt ?? this.updatedAt,
      riskAssessment: identical(riskAssessment, _sentinel)
          ? this.riskAssessment
          : riskAssessment as QuotationRiskAssessment?,
    );
  }
}