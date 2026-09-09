import 'package:flutter/material.dart';

import '../../../../shared/theme/app_colors.dart';
import '../../../../shared/theme/app_radius.dart';
import '../../../../shared/theme/app_spacing.dart';
import '../../../../shared/theme/app_text_styles.dart';
import '../../../../shared/widgets/app_card.dart';
import '../models/problem_understanding_result_model.dart';
import 'urgency_chip.dart';

class AnalysisResultCard extends StatelessWidget {
  final ProblemUnderstandingResultModel analysis;

  const AnalysisResultCard({
    super.key,
    required this.analysis,
  });

  @override
  Widget build(BuildContext context) {
    final confidencePct = analysis.confidence > 1.0
        ? analysis.confidence.toStringAsFixed(0)
        : (analysis.confidence * 100).toStringAsFixed(0);

    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Header: AI Analysis Label and Confidence
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Row(
                children: [
                  Container(
                    padding: const EdgeInsets.all(AppSpacing.xs + 2),
                    decoration: BoxDecoration(
                      color: AppColors.secondary.withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(AppRadius.small),
                    ),
                    child: const Icon(
                      Icons.auto_awesome_rounded,
                      size: 16,
                      color: AppColors.secondary,
                    ),
                  ),
                  const SizedBox(width: AppSpacing.sm),
                  const Text(
                    'AI Analysis Result',
                    style: AppTextStyles.cardHeading,
                  ),
                ],
              ),
              Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: AppSpacing.sm,
                  vertical: AppSpacing.xs,
                ),
                decoration: BoxDecoration(
                  color: AppColors.secondary.withValues(alpha: 0.08),
                  borderRadius: BorderRadius.circular(AppRadius.pill),
                ),
                child: Text(
                  '$confidencePct% Confidence',
                  style: const TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w600,
                    color: AppColors.secondary,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.md),

          // Category
          Row(
            crossAxisAlignment: CrossAxisAlignment.center,
            children: [
              const Text(
                'Category:',
                style: TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w500,
                  color: AppColors.textSecondary,
                ),
              ),
              const SizedBox(width: AppSpacing.sm),
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
          Row(
            crossAxisAlignment: CrossAxisAlignment.center,
            children: [
              const Text(
                'Urgency:',
                style: TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w500,
                  color: AppColors.textSecondary,
                ),
              ),
              const SizedBox(width: AppSpacing.sm),
              UrgencyChip(urgency: analysis.urgency),
            ],
          ),
          const SizedBox(height: AppSpacing.md),

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
        ],
      ),
    );
  }
}
