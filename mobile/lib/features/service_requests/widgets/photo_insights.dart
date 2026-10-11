import 'package:flutter/material.dart';

import '../../../../shared/theme/app_colors.dart';
import '../../../../shared/theme/app_spacing.dart';
import '../../../../shared/theme/app_text_styles.dart';
import '../models/analysis_visual_evidence.dart';

/// Compact AI-assisted evidence; decorative icons do not add screen-reader noise.
class PhotoInsights extends StatelessWidget {
  const PhotoInsights({
    super.key,
    required this.evidence,
    this.hasPhotos = false,
  });
  final AnalysisVisualEvidence evidence;
  final bool hasPhotos;

  @override
  Widget build(BuildContext context) {
    if (evidence.status == AnalysisVisionStatus.notRequested ||
        (evidence.status == AnalysisVisionStatus.unsupported && !hasPhotos)) {
      return const SizedBox.shrink();
    }
    final message = switch (evidence.status) {
      AnalysisVisionStatus.unsupported =>
        'Photos were attached but were not used in this analysis.',
      AnalysisVisionStatus.failed =>
        'Photos could not be used in this analysis.',
      _ => 'Photo evidence used',
    };
    return Padding(
      padding: const EdgeInsets.only(top: AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Divider(color: AppColors.border),
          const SizedBox(height: AppSpacing.xs),
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const ExcludeSemantics(
                child: Icon(
                  Icons.photo_outlined,
                  size: 20,
                  color: AppColors.textSecondary,
                ),
              ),
              const SizedBox(width: AppSpacing.sm),
              Expanded(child: Text(message, style: AppTextStyles.body)),
            ],
          ),
          if (evidence.status == AnalysisVisionStatus.used) ...[
            for (final observation in evidence.observations)
              Padding(
                padding: const EdgeInsets.only(top: AppSpacing.sm),
                child: Text(observation.observation, style: AppTextStyles.body),
              ),
            if (evidence.limitations.isNotEmpty) ...[
              const SizedBox(height: AppSpacing.sm),
              const Text('Photo limitations', style: AppTextStyles.small),
              for (final limitation in evidence.limitations)
                Padding(
                  padding: const EdgeInsets.only(top: AppSpacing.xs),
                  child: Text(
                    limitation,
                    style: AppTextStyles.body.copyWith(
                      color: AppColors.textSecondary,
                    ),
                  ),
                ),
            ],
          ],
        ],
      ),
    );
  }
}
