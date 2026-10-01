class CompletionRecordModel {
  final String? proofOfWorkImageUrl;
  final String summaryNotes;

  const CompletionRecordModel({
    this.proofOfWorkImageUrl,
    this.summaryNotes = '',
  });

  factory CompletionRecordModel.fromJson(Map<String, dynamic> json) {
    return CompletionRecordModel(
      proofOfWorkImageUrl: json['proofOfWorkImageUrl'] as String?,
      summaryNotes:
          (json['summaryNotes'] ?? json['workSummary']) as String? ?? '',
    );
  }
}