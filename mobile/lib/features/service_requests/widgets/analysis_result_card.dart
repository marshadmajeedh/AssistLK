import '../models/analysis_visual_evidence.dart';
import 'photo_insights.dart';

import 'package:flutter/material.dart';

import '../../../../shared/theme/app_colors.dart';
import '../../../../shared/theme/app_radius.dart';
import '../../../../shared/theme/app_spacing.dart';
import '../../../../shared/theme/app_text_styles.dart';
import '../../../../shared/widgets/app_card.dart';
import '../models/canonical_service_category.dart';
import '../models/problem_understanding_result_model.dart';
import '../models/service_request_status.dart';
import 'urgency_chip.dart';

class AnalysisResultCard extends StatelessWidget {
  final ProblemUnderstandingResultModel analysis;
  final AnalysisVisualEvidence visualEvidence;
  final bool hasPhotos;
  final String? categoryHint;
  final ServiceRequestStatus? status;

  const AnalysisResultCard({
    super.key,
    required this.analysis,
    this.visualEvidence = const AnalysisVisualEvidence(),
    this.hasPhotos = false,
    this.categoryHint,
    this.status,
  });

  @override
  Widget build(BuildContext context) {
    final confidencePct = analysis.confidence > 1.0
        ? analysis.confidence.toStringAsFixed(0)
        : (analysis.confidence * 100).toStringAsFixed(0);

    final effectiveStatus = status ?? analysis.status;
    final isAllowedStatus =
        effectiveStatus == ServiceRequestStatus.analyzed ||
        effectiveStatus == ServiceRequestStatus.readyForMatching;

    final hasHint = categoryHint != null && categoryHint!.trim().isNotEmpty;
    final hasAuthoritativeCategory =
        analysis.category.isNotEmpty && analysis.category != 'Unclassified';

    final canonicalHint = hasHint
        ? CanonicalServiceCategory.fromCanonicalOrDisplayName(categoryHint)
                  ?.canonicalName ??
              categoryHint!.trim()
        : null;
    final canonicalCategory = hasAuthoritativeCategory
        ? CanonicalServiceCategory.fromCanonicalOrDisplayName(analysis.category)
                  ?.canonicalName ??
              analysis.category.trim()
        : null;

    final isMismatch =
        isAllowedStatus &&
        hasHint &&
        hasAuthoritativeCategory &&
        canonicalHint != canonicalCategory;

    final friendlyHint =
        CanonicalServiceCategory.fromCanonicalOrDisplayName(categoryHint)
            ?.displayName ??
        (categoryHint == null ? 'Let AssistLK AI identify' : categoryHint!);

    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Header: AI Analysis Label and Confidence
          Wrap(
            spacing: AppSpacing.sm,
            runSpacing: AppSpacing.xs,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              const ExcludeSemantics(
                child: Icon(
                  Icons.auto_awesome_rounded,
                  color: AppColors.secondary,
                  size: 20,
                ),
              ),
              const Text(
                'AssistLK AI Analysis',
                style: AppTextStyles.cardHeading,
              ),
              Text(
                '$confidencePct% Confidence',
                style: AppTextStyles.small.copyWith(color: AppColors.secondary),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.md),

          // Customer Preference
          Wrap(
            crossAxisAlignment: WrapCrossAlignment.center,
            spacing: AppSpacing.sm,
            runSpacing: AppSpacing.xs,
            children: [
              const Text(
                'Your preference:',
                style: TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w500,
                  color: AppColors.textSecondary,
                ),
              ),
              Text(
                friendlyHint,
                style: const TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w600,
                  color: AppColors.textPrimary,
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.sm),

          // AI Classification
          Wrap(
            crossAxisAlignment: WrapCrossAlignment.center,
            spacing: AppSpacing.sm,
            runSpacing: AppSpacing.xs,
            children: [
              const Text(
                'AssistLK AI classification:',
                style: TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w500,
                  color: AppColors.textSecondary,
                ),
              ),
              Text(
                analysis.category.isEmpty ? 'Unclassified' : analysis.category,
                style: const TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w600,
                  color: AppColors.textPrimary,
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.sm),

          // Urgency
          Wrap(
            crossAxisAlignment: WrapCrossAlignment.center,
            spacing: AppSpacing.sm,
            runSpacing: AppSpacing.xs,
            children: [
              const Text(
                'Urgency:',
                style: TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w500,
                  color: AppColors.textSecondary,
                ),
              ),
              UrgencyChip(urgency: analysis.urgency),
            ],
          ),
          const SizedBox(height: AppSpacing.md),

          // Neutral Mismatch Banner
          if (isMismatch) ...[
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(AppSpacing.md),
              decoration: BoxDecoration(
                color: AppColors.primary.withValues(alpha: 0.06),
                borderRadius: BorderRadius.circular(AppRadius.medium),
                border: Border.all(
                  color: AppColors.primary.withValues(alpha: 0.25),
                ),
              ),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Icon(
                    Icons.info_outline_rounded,
                    color: AppColors.primary,
                    size: 20,
                  ),
                  const SizedBox(width: AppSpacing.sm),
                  Expanded(
                    child: Text(
                      'AssistLK AI identified a different service category based on your problem description.',
                      style: AppTextStyles.body.copyWith(
                        color: AppColors.textPrimary,
                        fontSize: 13,
                      ),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: AppSpacing.md),
          ],

          // Problem Summary
          const Text(
            'Problem Summary',
            style: TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w600,
              color: AppColors.textSecondary,
            ),
          ),
          const SizedBox(height: AppSpacing.xs),
          Container(
            width: double.infinity,
            padding: const EdgeInsets.all(AppSpacing.sm + 4),
            decoration: BoxDecoration(
              color: AppColors.background,
              borderRadius: BorderRadius.circular(AppRadius.small),
              border: Border.all(color: AppColors.border),
            ),
            child: Text(
              analysis.problemSummary.isEmpty
                  ? 'No summary provided.'
                  : analysis.problemSummary,
              style: AppTextStyles.body,
            ),
          ),
          PhotoInsights(evidence: visualEvidence, hasPhotos: hasPhotos),
        ],
      ),
    );
  }
}
