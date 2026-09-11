import 'package:flutter/material.dart';

import '../../../../shared/theme/app_colors.dart';
import '../../../../shared/theme/app_radius.dart';
import '../../../../shared/theme/app_spacing.dart';
import '../../../../shared/theme/app_text_styles.dart';
import '../../../../shared/widgets/app_button.dart';
import '../../../../shared/widgets/app_card.dart';
import '../models/service_request_clarification_model.dart';

class ClarificationSection extends StatefulWidget {
  final List<String> followUpQuestions;
  final List<ServiceRequestClarificationModel>? clarifications;
  final VoidCallback onEditDetails;
  final VoidCallback onReanalyze;
  final Future<void> Function(int round, Map<String, String> answers)? onSubmitAnswers;
  final bool isReanalyzing;
  final bool isSubmitting;
  final bool hasReachedMaxRounds;

  const ClarificationSection({
    super.key,
    this.followUpQuestions = const [],
    this.clarifications,
    required this.onEditDetails,
    required this.onReanalyze,
    this.onSubmitAnswers,
    this.isReanalyzing = false,
    this.isSubmitting = false,
    this.hasReachedMaxRounds = false,
  });

  @override
  State<ClarificationSection> createState() => _ClarificationSectionState();
}

class _ClarificationSectionState extends State<ClarificationSection> {
  final _formKey = GlobalKey<FormState>();
  final Map<String, TextEditingController> _controllers = {};

  @override
  void initState() {
    super.initState();
    _syncControllers();
  }

  @override
  void didUpdateWidget(covariant ClarificationSection oldWidget) {
    super.didUpdateWidget(oldWidget);
    _syncControllers();
  }

  void _syncControllers() {
    if (widget.clarifications != null) {
      for (final c in widget.clarifications!) {
        if (!_controllers.containsKey(c.id)) {
          _controllers[c.id] = TextEditingController(text: c.answer ?? '');
        } else if (c.answer != null && _controllers[c.id]!.text.isEmpty) {
          _controllers[c.id]!.text = c.answer!;
        }
      }
    }
  }

  @override
  void dispose() {
    for (final controller in _controllers.values) {
      controller.dispose();
    }
    super.dispose();
  }

  Future<void> _handleSubmit(int round, List<ServiceRequestClarificationModel> pending) async {
    if (_formKey.currentState?.validate() != true) {
      return;
    }

    final answers = <String, String>{};
    for (final q in pending) {
      final text = _controllers[q.id]?.text.trim() ?? '';
      answers[q.id] = text;
    }

    if (widget.onSubmitAnswers != null) {
      await widget.onSubmitAnswers!(round, answers);
    } else {
      widget.onReanalyze();
    }
  }

  @override
  Widget build(BuildContext context) {
    final clarifications = widget.clarifications;

    // State 1: Structured clarifications provided
    if (clarifications != null && clarifications.isNotEmpty) {
      final currentRound = clarifications
          .map((c) => c.clarificationRound)
          .reduce((a, b) => a > b ? a : b);

      final roundClarifications = clarifications
          .where((c) => c.clarificationRound == currentRound)
          .toList();

      final pendingQuestions = roundClarifications
          .where((c) => c.isActionable)
          .toList();

      final isFullyAnswered = roundClarifications.isNotEmpty &&
          roundClarifications.every((c) => c.isAnswered);

      // Max rounds reached and re-analysis still needs more info
      if (widget.hasReachedMaxRounds && (isFullyAnswered || pendingQuestions.isEmpty)) {
        return _buildMaxRoundsReachedCard(clarifications);
      }

      // Fully answered current round, awaiting or re-analyzing
      if (isFullyAnswered) {
        return _buildFullyAnsweredCard(currentRound, roundClarifications);
      }

      // Active pending questions requiring answers
      if (pendingQuestions.isNotEmpty) {
        return _buildPendingQuestionsCard(currentRound, pendingQuestions);
      }
    }

    // State 2: Legacy fallback or strings list
    return _buildLegacyOrStringCard();
  }

  Widget _buildMaxRoundsReachedCard(List<ServiceRequestClarificationModel> clarifications) {
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                padding: const EdgeInsets.all(AppSpacing.xs + 2),
                decoration: BoxDecoration(
                  color: AppColors.warning.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(AppRadius.small),
                ),
                child: const Icon(
                  Icons.info_outline_rounded,
                  size: 18,
                  color: AppColors.warning,
                ),
              ),
              const SizedBox(width: AppSpacing.sm),
              const Expanded(
                child: Text(
                  'Further Details Needed',
                  style: AppTextStyles.cardHeading,
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.sm),
          Text(
            'Maximum clarification rounds (2 of 2) have been completed. Please edit your request description with more specific details so our AI can accurately classify your request.',
            style: AppTextStyles.body.copyWith(
              color: AppColors.textSecondary,
            ),
          ),
          const SizedBox(height: AppSpacing.md),
          OutlinedButton(
            onPressed: widget.isReanalyzing ? null : widget.onEditDetails,
            style: OutlinedButton.styleFrom(
              padding: const EdgeInsets.symmetric(vertical: 14),
              side: const BorderSide(color: AppColors.primary),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(AppRadius.medium),
              ),
              minimumSize: const Size(double.infinity, 48),
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
        ],
      ),
    );
  }

  Widget _buildFullyAnsweredCard(
    int round,
    List<ServiceRequestClarificationModel> clarifications,
  ) {
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                padding: const EdgeInsets.all(AppSpacing.xs + 2),
                decoration: BoxDecoration(
                  color: AppColors.success.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(AppRadius.small),
                ),
                child: const Icon(
                  Icons.check_circle_outline_rounded,
                  size: 18,
                  color: AppColors.success,
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
            'Answers for Round $round have been submitted. Tap Re-analyze to continue with provider matching.',
            style: AppTextStyles.body.copyWith(
              color: AppColors.textSecondary,
            ),
          ),
          const SizedBox(height: AppSpacing.md),
          ...clarifications.map((item) {
            return Padding(
              padding: const EdgeInsets.only(bottom: AppSpacing.sm),
              child: Container(
                width: double.infinity,
                padding: const EdgeInsets.all(AppSpacing.sm + 2),
                decoration: BoxDecoration(
                  color: AppColors.background,
                  borderRadius: BorderRadius.circular(AppRadius.small),
                  border: Border.all(color: AppColors.border),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
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
                            '${item.sequence}',
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
                            item.question,
                            style: AppTextStyles.body.copyWith(
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                        ),
                      ],
                    ),
                    if (item.answer != null && item.answer!.isNotEmpty) ...[
                      const SizedBox(height: AppSpacing.xs),
                      Padding(
                        padding: const EdgeInsets.only(left: 30),
                        child: Text(
                          'Your Answer: ${item.answer}',
                          style: AppTextStyles.body.copyWith(
                            color: AppColors.textSecondary,
                          ),
                        ),
                      ),
                    ],
                  ],
                ),
              ),
            );
          }),
          const SizedBox(height: AppSpacing.md),
          Row(
            children: [
              Expanded(
                child: OutlinedButton(
                  onPressed: widget.isReanalyzing ? null : widget.onEditDetails,
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
                  onPressed: widget.onReanalyze,
                  isLoading: widget.isReanalyzing,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildPendingQuestionsCard(
    int round,
    List<ServiceRequestClarificationModel> pending,
  ) {
    return AppCard(
      child: Form(
        key: _formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
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
                Expanded(
                  child: Text(
                    'Clarification Needed',
                    style: AppTextStyles.cardHeading,
                  ),
                ),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                  decoration: BoxDecoration(
                    color: AppColors.primary.withValues(alpha: 0.08),
                    borderRadius: BorderRadius.circular(AppRadius.pill),
                  ),
                  child: Text(
                    'Round $round of 2',
                    style: const TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w600,
                      color: AppColors.primary,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.sm),
            Text(
              'Please answer the follow-up question(s) below so our AI can accurately classify your request:',
              style: AppTextStyles.body.copyWith(
                color: AppColors.textSecondary,
              ),
            ),
            const SizedBox(height: AppSpacing.md),
            ...pending.map((item) {
              final controller = _controllers[item.id] ??= TextEditingController(text: item.answer ?? '');

              return Padding(
                padding: const EdgeInsets.only(bottom: AppSpacing.md),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
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
                            '${item.sequence}',
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
                            item.question,
                            style: AppTextStyles.body.copyWith(
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: AppSpacing.xs + 2),
                    TextFormField(
                      controller: controller,
                      maxLines: 2,
                      maxLength: 1000,
                      decoration: InputDecoration(
                        hintText: 'Enter your answer...',
                        hintStyle: AppTextStyles.body.copyWith(color: AppColors.textSecondary),
                        contentPadding: const EdgeInsets.all(AppSpacing.sm),
                        border: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(AppRadius.small),
                          borderSide: const BorderSide(color: AppColors.border),
                        ),
                        focusedBorder: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(AppRadius.small),
                          borderSide: const BorderSide(color: AppColors.primary, width: 1.5),
                        ),
                      ),
                      validator: (value) {
                        if (value == null || value.trim().isEmpty) {
                          return 'Please provide an answer to this question.';
                        }
                        if (value.trim().length > 1000) {
                          return 'Answer cannot exceed 1000 characters.';
                        }
                        return null;
                      },
                    ),
                  ],
                ),
              );
            }),
            const SizedBox(height: AppSpacing.sm),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: (widget.isSubmitting || widget.isReanalyzing)
                        ? null
                        : widget.onEditDetails,
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
                    text: 'Submit & Re-analyze',
                    onPressed: () => _handleSubmit(round, pending),
                    isLoading: widget.isSubmitting || widget.isReanalyzing,
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildLegacyOrStringCard() {
    final questions = widget.followUpQuestions;

    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
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
          if (questions.isNotEmpty) ...[
            ...questions.asMap().entries.map((entry) {
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
          Row(
            children: [
              Expanded(
                child: OutlinedButton(
                  onPressed: widget.isReanalyzing ? null : widget.onEditDetails,
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
                  onPressed: widget.onReanalyze,
                  isLoading: widget.isReanalyzing,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
