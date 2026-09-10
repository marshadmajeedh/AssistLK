class SubmitClarificationAnswersDto {
  final int clarificationRound;
  final List<ClarificationAnswerSubmissionItemDto> answers;

  const SubmitClarificationAnswersDto({
    required this.clarificationRound,
    required this.answers,
  });

  Map<String, dynamic> toJson() {
    return {
      'clarificationRound': clarificationRound,
      'answers': answers.map((a) => a.toJson()).toList(),
    };
  }
}

class ClarificationAnswerSubmissionItemDto {
  final String clarificationId;
  final String answer;

  const ClarificationAnswerSubmissionItemDto({
    required this.clarificationId,
    required this.answer,
  });

  Map<String, dynamic> toJson() {
    return {
      'clarificationId': clarificationId,
      'answer': answer,
    };
  }
}
