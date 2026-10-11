enum AnalysisVisionStatus {
  notRequested,
  used,
  unsupported,
  failed;

  static AnalysisVisionStatus parse(Object? value) => switch (value) {
    'used' => used,
    'unsupported' => unsupported,
    'failed' => failed,
    _ => notRequested,
  };

  String get value => switch (this) {
    notRequested => 'not_requested',
    used => 'used',
    unsupported => 'unsupported',
    failed => 'failed',
  };
}

class AnalysisPhotoObservation {
  final String attachmentId;
  final String observation;
  const AnalysisPhotoObservation(this.attachmentId, this.observation);
}

/// Safe detail metadata only; image bytes use the existing authenticated endpoint.
class AnalysisVisualEvidence {
  final AnalysisVisionStatus status;
  final List<String> attachmentIdsUsed;
  final List<AnalysisPhotoObservation> observations;
  final List<String> limitations;
  const AnalysisVisualEvidence({
    this.status = AnalysisVisionStatus.notRequested,
    this.attachmentIdsUsed = const [],
    this.observations = const [],
    this.limitations = const [],
  });

  factory AnalysisVisualEvidence.fromJson(Object? value) {
    if (value is! Map) return const AnalysisVisualEvidence();
    final status = AnalysisVisionStatus.parse(value['visionStatus']);
    final ids = value['attachmentIdsUsed'] is List
        ? (value['attachmentIdsUsed'] as List)
              .whereType<String>()
              .take(3)
              .toList()
        : <String>[];
    final observations = <AnalysisPhotoObservation>[];
    if (status == AnalysisVisionStatus.used && value['observations'] is List) {
      for (final item in value['observations'] as List) {
        if (item is Map &&
            item['attachmentId'] is String &&
            ids.contains(item['attachmentId']) &&
            item['observation'] is String &&
            (item['observation'] as String).trim().isNotEmpty &&
            (item['observation'] as String).length <= 240 &&
            observations.length < 5 &&
            observations
                    .where((o) => o.attachmentId == item['attachmentId'])
                    .length <
                2) {
          observations.add(
            AnalysisPhotoObservation(item['attachmentId'], item['observation']),
          );
        }
      }
    }
    return AnalysisVisualEvidence(
      status: status,
      attachmentIdsUsed: status == AnalysisVisionStatus.used
          ? List.unmodifiable(ids)
          : const [],
      observations: List.unmodifiable(observations),
      limitations: value['limitations'] is List
          ? List.unmodifiable(
              (value['limitations'] as List)
                  .whereType<String>()
                  .where((s) => s.trim().isNotEmpty && s.length <= 240)
                  .take(3),
            )
          : const [],
    );
  }

  Map<String, dynamic> toJson() => {
    'visionStatus': status.value,
    'attachmentIdsUsed': attachmentIdsUsed,
    'observations': observations
        .map(
          (o) => {'attachmentId': o.attachmentId, 'observation': o.observation},
        )
        .toList(),
    'limitations': limitations,
  };
}
