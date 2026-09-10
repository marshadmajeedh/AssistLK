import 'package:flutter/material.dart';

import '../../../../shared/theme/app_colors.dart';
import '../../../../shared/theme/app_radius.dart';
import '../../../../shared/theme/app_spacing.dart';
import '../../../../shared/theme/app_text_styles.dart';
import '../../../../shared/widgets/app_button.dart';
import '../../../../shared/widgets/app_card.dart';

class ClarificationSection extends StatelessWidget {
  final List<String> followUpQuestions;
  final VoidCallback onEditDetails;
  final VoidCallback onReanalyze;
  final bool isReanalyzing;

  const ClarificationSection({
    super.key,
    required this.followUpQuestions,
    required this.onEditDetails,
    required this.onReanalyze,
    this.isReanalyzing = false,
  });

  @override
  Widget build(BuildContext context) {
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Header with warning icon
          Row(
            children: [
              Container(
                padding: const EdgeInsets.all(AppSpacing.xs + 2),
                decoration: BoxDecoration(
                  color: AppColors.warning.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(AppRadius.small),
                ),
                child: const Icon(
                  Icons.help_outline_rounded,
                  size: 18,
                  color: AppColors.warning,
                ),
              ),
              const SizedBox(width: AppSpacing.sm),
              const Expanded(
                child: Text(
                  'Clarification Needed',
                  style: AppTextStyles.cardHeading,
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.sm),

          Text(
            'To ensure the best match with service providers, please review the following follow-up questions:',
            style: AppTextStyles.body.copyWith(
              color: AppColors.textSecondary,
            ),
          ),
          const SizedBox(height: AppSpacing.md),

          // Follow-up questions list
          if (followUpQuestions.isNotEmpty) ...[
            ...followUpQuestions.asMap().entries.map((entry) {
              final index = entry.key + 1;
              final question = entry.value;

              return Padding(
                padding: const EdgeInsets.only(bottom: AppSpacing.sm),
                child: Container(
                  padding: const EdgeInsets.all(AppSpacing.sm + 2),
                  decoration: BoxDecoration(
                    color: AppColors.background,
                    borderRadius: BorderRadius.circular(AppRadius.small),
                    border: Border.all(color: AppColors.border),
                  ),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Container(
                        width: 22,
                        height: 22,
                        alignment: Alignment.center,
                        decoration: const BoxDecoration(
                          color: AppColors.primary,
                          shape: BoxShape.circle,
                        ),
                        child: Text(
                          '$index',
                          style: const TextStyle(
                            fontSize: 11,
                            fontWeight: FontWeight.w700,
                            color: Colors.white,
                          ),
                        ),
                      ),
                      const SizedBox(width: AppSpacing.sm),
                      Expanded(
                        child: Text(
                          question,
                          style: AppTextStyles.body,
                        ),
                      ),
                    ],
                  ),
                ),
              );
            }),
          ] else ...[
            Container(
              padding: const EdgeInsets.all(AppSpacing.sm),
              decoration: BoxDecoration(
                color: AppColors.background,
                borderRadius: BorderRadius.circular(AppRadius.small),
              ),
              child: const Text(
                'Please provide more details in your request description.',
                style: AppTextStyles.body,
              ),
            ),
          ],
          const SizedBox(height: AppSpacing.md),

          // Action Buttons: Edit Details & Re-analyze
          Row(
            children: [
              Expanded(
                child: OutlinedButton(
                  onPressed: isReanalyzing ? null : onEditDetails,
                  style: OutlinedButton.styleFrom(
                    padding: const EdgeInsets.symmetric(vertical: 14),
                    side: const BorderSide(color: AppColors.primary),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(AppRadius.medium),
                    ),
                  ),
                  child: const Text(
                    'Edit Details',
                    style: TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w600,
                      color: AppColors.primary,
                    ),
                  ),
                ),
              ),
              const SizedBox(width: AppSpacing.md),
              Expanded(
                child: AppButton(
                  text: 'Re-analyze',
                  onPressed: onReanalyze,
                  isLoading: isReanalyzing,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
