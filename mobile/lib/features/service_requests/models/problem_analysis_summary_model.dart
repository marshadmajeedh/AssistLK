class ProblemAnalysisSummaryModel {
  final String id;
  final String detectedProblem;
  final double confidence;
  final String agentName;
  final DateTime createdAt;

  const ProblemAnalysisSummaryModel({
    required this.id,
    required this.detectedProblem,
    required this.confidence,
    required this.agentName,
    required this.createdAt,
  });

  factory ProblemAnalysisSummaryModel.fromJson(Map<String, dynamic> json) {
    return ProblemAnalysisSummaryModel(
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
      'detectedProblem': detectedProblem,
      'confidence': confidence,
      'agentName': agentName,
      'createdAt': createdAt.toIso8601String(),
    };
  }
}
