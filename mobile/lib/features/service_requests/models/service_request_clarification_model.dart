class ServiceRequestClarificationModel {
  final String id;
  final int clarificationRound;
  final int sequence;
  final String question;
  final String? answer;
  final DateTime? answeredAt;
  final DateTime? supersededAt;

  const ServiceRequestClarificationModel({
    required this.id,
    required this.clarificationRound,
    required this.sequence,
    required this.question,
    this.answer,
    this.answeredAt,
    this.supersededAt,
  });

  bool get isAnswered => answer != null && answer!.trim().isNotEmpty;
  bool get isSuperseded => supersededAt != null;
  bool get isActionable => !isAnswered && !isSuperseded;

  factory ServiceRequestClarificationModel.fromJson(Map<String, dynamic> json) {
    return ServiceRequestClarificationModel(
      id: json['id']?.toString() ?? '',
      clarificationRound: (json['clarificationRound'] as num?)?.toInt() ?? 1,
      sequence: (json['sequence'] as num?)?.toInt() ?? 1,
      question: json['question'] as String? ?? '',
      answer: json['answer'] as String?,
      answeredAt: json['answeredAt'] != null
          ? DateTime.parse(json['answeredAt'] as String)
          : null,
      supersededAt: json['supersededAt'] != null
          ? DateTime.parse(json['supersededAt'] as String)
          : null,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'clarificationRound': clarificationRound,
      'sequence': sequence,
      'question': question,
      'answer': answer,
      'answeredAt': answeredAt?.toIso8601String(),
      'supersededAt': supersededAt?.toIso8601String(),
    };
  }

  ServiceRequestClarificationModel copyWith({
    String? id,
    int? clarificationRound,
    int? sequence,
    String? question,
    String? answer,
    DateTime? answeredAt,
    DateTime? supersededAt,
  }) {
    return ServiceRequestClarificationModel(
      id: id ?? this.id,
      clarificationRound: clarificationRound ?? this.clarificationRound,
      sequence: sequence ?? this.sequence,
      question: question ?? this.question,
      answer: answer ?? this.answer,
      answeredAt: answeredAt ?? this.answeredAt,
      supersededAt: supersededAt ?? this.supersededAt,
    );
  }
}
