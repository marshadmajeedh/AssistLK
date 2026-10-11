import 'package:flutter/material.dart';

import '../../../shared/theme/app_colors.dart';
import '../../../shared/theme/app_radius.dart';
import '../../../shared/theme/app_spacing.dart';
import '../../../shared/theme/app_text_styles.dart';
import '../models/quotation_model.dart';

/// Displays the LLM-generated risk assessment attached to a quotation.
/// This is the Agentic AI evidence shown to the customer before they approve.
class RiskAssessmentCard extends StatelessWidget {
  final QuotationRiskAssessment assessment;

  const RiskAssessmentCard({super.key, required this.assessment});

  @override
  Widget build(BuildContext context) {
    if (!assessment.hasData) return const SizedBox.shrink();

    final (label, accent, surface, icon) = _visualFor(assessment.riskLevel);

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(
        color: surface,
        borderRadius: BorderRadius.circular(AppRadius.large),
        border: Border.all(color: accent.withValues(alpha: 0.35)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Header row
          Row(
            children: [
              Container(
                width: 36,
                height: 36,
                decoration: BoxDecoration(
                  color: accent.withValues(alpha: 0.15),
                  borderRadius: BorderRadius.circular(AppRadius.medium),
                ),
                child: Icon(Icons.auto_awesome_rounded, size: 20, color: accent),
              ),
              const SizedBox(width: AppSpacing.sm),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'AI Risk Assessment',
                      style: AppTextStyles.cardHeading,
                    ),
                    const SizedBox(height: 2),
                    Text(
                      'Reviewed by AssistLK AI before you decide',
                      style: AppTextStyles.small.copyWith(
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ],
                ),
              ),
              if (assessment.confidence != null)
                _ConfidencePill(confidence: assessment.confidence!),
            ],
          ),
          const SizedBox(height: AppSpacing.sm),

          // Risk level + recommendation badges
          Wrap(
            spacing: AppSpacing.xs,
            runSpacing: AppSpacing.xs,
            children: [
              _Pill(
                label: 'Risk: $label',
                background: accent.withValues(alpha: 0.15),
                foreground: accent,
                icon: icon,
              ),
              if (assessment.recommendation != null)
                _Pill(
                  label: _recommendationLabel(assessment.recommendation!),
                  background: AppColors.primarySurface,
                  foreground: AppColors.primary,
                  icon: Icons.lightbulb_outline_rounded,
                ),
            ],
          ),

          // Rationale
          if (assessment.rationale != null &&
              assessment.rationale!.trim().isNotEmpty) ...[
            const SizedBox(height: AppSpacing.md),
            Text(
              assessment.rationale!,
              style: AppTextStyles.body.copyWith(height: 1.4),
            ),
          ],

          // Suggested concerns
          if (assessment.suggestedConcerns.isNotEmpty) ...[
            const SizedBox(height: AppSpacing.md),
            Text(
              'Things to consider',
              style: AppTextStyles.small.copyWith(
                fontWeight: FontWeight.w700,
                color: AppColors.textPrimary,
              ),
            ),
            const SizedBox(height: AppSpacing.xs),
            ...assessment.suggestedConcerns.map(
              (c) => Padding(
                padding: const EdgeInsets.only(bottom: 4),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Padding(
                      padding: EdgeInsets.only(top: 6),
                      child: Icon(
                        Icons.circle,
                        size: 6,
                        color: AppColors.textSecondary,
                      ),
                    ),
                    const SizedBox(width: AppSpacing.sm),
                    Expanded(
                      child: Text(c, style: AppTextStyles.body),
                    ),
                  ],
                ),
              ),
            ),
          ],

          // Model + tokens footer
          if (assessment.model != null ||
              (assessment.totalTokens ?? 0) > 0) ...[
            const SizedBox(height: AppSpacing.sm),
            const Divider(height: 1, color: AppColors.border),
            const SizedBox(height: AppSpacing.sm),
            Row(
              children: [
                const Icon(
                  Icons.smart_toy_outlined,
                  size: 14,
                  color: AppColors.textSecondary,
                ),
                const SizedBox(width: 4),
                Expanded(
                  child: Text(
                    _footerText(assessment),
                    style: AppTextStyles.small.copyWith(
                      color: AppColors.textSecondary,
                      fontSize: 11,
                    ),
                  ),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }

  // ------------------------------------------------------------------

  String _recommendationLabel(String r) {
    switch (r.toLowerCase()) {
      case 'approve':
        return 'Recommendation: proceed';
      case 'reject':
        return 'Recommendation: decline';
      case 'request_clarification':
        return 'Recommendation: ask for clarification';
      default:
        return 'Recommendation: $r';
    }
  }

  (String, Color, Color, IconData) _visualFor(String? level) {
    switch ((level ?? '').toLowerCase()) {
      case 'low':
        return (
          'Low',
          AppColors.success,
          AppColors.successSurface,
          Icons.verified_rounded,
        );
      case 'high':
        return (
          'High',
          AppColors.error,
          const Color(0xFFFEF2F2),
          Icons.warning_amber_rounded,
        );
      case 'medium':
      default:
        return (
          'Medium',
          AppColors.warning,
          AppColors.warningSurface,
          Icons.info_outline_rounded,
        );
    }
  }

  String _footerText(QuotationRiskAssessment a) {
    final model = a.model ?? 'AI';
    final tokens = a.totalTokens ?? 0;
    if (tokens > 0) return 'Generated by $model • $tokens tokens';
    return 'Generated by $model';
  }
}

class _Pill extends StatelessWidget {
  final String label;
  final Color background;
  final Color foreground;
  final IconData icon;

  const _Pill({
    required this.label,
    required this.background,
    required this.foreground,
    required this.icon,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.sm,
        vertical: 5,
      ),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(AppRadius.small),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 13, color: foreground),
          const SizedBox(width: 4),
          Text(
            label,
            style: TextStyle(
              color: foreground,
              fontSize: 12,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
      ),
    );
  }
}

class _ConfidencePill extends StatelessWidget {
  final double confidence;

  const _ConfidencePill({required this.confidence});

  @override
  Widget build(BuildContext context) {
    final percent = (confidence * 100).round();
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.sm,
        vertical: 4,
      ),
      decoration: BoxDecoration(
        color: AppColors.primarySurface,
        borderRadius: BorderRadius.circular(AppRadius.small),
      ),
      child: Text(
        '$percent%',
        style: const TextStyle(
          color: AppColors.primary,
          fontSize: 12,
          fontWeight: FontWeight.w700,
        ),
      ),
    );
  }
}