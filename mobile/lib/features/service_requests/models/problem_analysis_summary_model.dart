import 'analysis_visual_evidence.dart';

class ProblemAnalysisSummaryModel {
  final AnalysisVisualEvidence visualEvidence;
  final String id;
  final String detectedProblem;
  final double confidence;
  final String agentName;
  final DateTime createdAt;

  const ProblemAnalysisSummaryModel({
    required this.id,
    this.visualEvidence = const AnalysisVisualEvidence(),
    required this.detectedProblem,
    required this.confidence,
    required this.agentName,
    required this.createdAt,
  });

  factory ProblemAnalysisSummaryModel.fromJson(Map<String, dynamic> json) {
    return ProblemAnalysisSummaryModel(
      visualEvidence: AnalysisVisualEvidence.fromJson(json['visualEvidence']),
      id: json['id']?.toString() ?? '',
      detectedProblem: json['detectedProblem'] as String? ?? '',
      confidence: json['confidence'] != null
          ? (json['confidence'] as num).toDouble()
          : 0.0,
      agentName: json['agentName'] as String? ?? '',
      createdAt: json['createdAt'] != null
          ? DateTime.parse(json['createdAt'] as String)
          : DateTime.now(),
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'visualEvidence': visualEvidence.toJson(),
      'detectedProblem': detectedProblem,
      'confidence': confidence,
      'agentName': agentName,
      'createdAt': createdAt.toIso8601String(),
    };
  }
}
