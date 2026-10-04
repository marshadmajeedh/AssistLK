import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../shared/theme/app_colors.dart';
import '../../../shared/theme/app_radius.dart';
import '../../../shared/theme/app_spacing.dart';
import '../../../shared/theme/app_text_styles.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_card.dart';
import '../../auth/providers/auth_provider.dart';
import '../models/quotation_model.dart';
import '../providers/quotation_provider.dart';
import '../services/quotation_service.dart';
import '../widgets/quotation_status_badge.dart';
import '../widgets/risk_assessment_card.dart';
import 'booking_confirmation_screen.dart';

class QuotationDetailScreen extends StatefulWidget {
  final int quotationId;
  final QuotationModel? initialQuotation;

  const QuotationDetailScreen({
    super.key,
    required this.quotationId,
    this.initialQuotation,
  });

  @override
  State<QuotationDetailScreen> createState() => _QuotationDetailScreenState();
}

class _QuotationDetailScreenState extends State<QuotationDetailScreen> {
  late final QuotationProvider _provider;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthProvider>();
    _provider = QuotationProvider(
      quotationService: QuotationService(apiClient: auth.authService.apiClient),
    );
    if (widget.initialQuotation != null) {
      _provider.setCurrentQuotation(widget.initialQuotation);
    } else {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        _provider.loadById(widget.quotationId);
      });
    }
  }

  @override
  void dispose() {
    _provider.dispose();
    super.dispose();
  }

  Future<void> _onApprove(QuotationModel q) async {
    if (q.workflowThreadId == null) {
      _snack('This quotation is not ready for approval yet.', AppColors.warning);
      return;
    }

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogCtx) => AlertDialog(
        title: const Text('Approve Quotation'),
        content: Text(
          'Approve quotation #${q.id} for Rs. ${q.totalAmount.toStringAsFixed(2)}?\n\n'
          'A booking will be created and the provider will be notified.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogCtx).pop(false),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () => Navigator.of(dialogCtx).pop(true),
            style: TextButton.styleFrom(foregroundColor: AppColors.success),
            child: const Text('Approve'),
          ),
        ],
      ),
    );

    if (confirmed != true || !mounted) return;

    final booking = await _provider.approve(
      quotationId: q.id,
      threadId: q.workflowThreadId!,
      customerRemarks: 'Approved via mobile app',
    );

    if (!mounted) return;

    if (booking != null) {
      Navigator.of(context).pushReplacement(
        MaterialPageRoute<void>(
          builder: (_) => BookingConfirmationScreen(booking: booking),
        ),
      );
    } else {
      _snack(
        _provider.error ?? 'Approval failed. Please try again.',
        AppColors.error,
      );
    }
  }

  Future<void> _onReject(QuotationModel q) async {
    if (q.workflowThreadId == null) {
      _snack('This quotation is not ready for review yet.', AppColors.warning);
      return;
    }

    final controller = TextEditingController();
    final reason = await showDialog<String>(
      context: context,
      builder: (dialogCtx) => AlertDialog(
        title: const Text('Reject Quotation'),
        content: TextField(
          controller: controller,
          maxLines: 3,
          decoration: const InputDecoration(
            hintText: 'Tell the provider why you are rejecting this quotation.',
            border: OutlineInputBorder(),
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogCtx).pop(),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () {
              final text = controller.text.trim();
              if (text.isEmpty) return;
              Navigator.of(dialogCtx).pop(text);
            },
            style: TextButton.styleFrom(foregroundColor: AppColors.error),
            child: const Text('Reject'),
          ),
        ],
      ),
    );

    if (reason == null || reason.isEmpty || !mounted) return;

    final updated = await _provider.reject(
      quotationId: q.id,
      threadId: q.workflowThreadId!,
      reason: reason,
    );

    if (!mounted) return;

    if (updated != null) {
      _snack('Quotation rejected. The provider has been notified.',
          AppColors.textSecondary);
    } else {
      _snack(_provider.error ?? 'Rejection failed.', AppColors.error);
    }
  }

  void _snack(String message, Color background) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message), backgroundColor: background),
    );
  }

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider<QuotationProvider>.value(
      value: _provider,
      child: Consumer<QuotationProvider>(
        builder: (context, provider, _) {
          final q = provider.currentQuotation;

          if (q == null && provider.isLoading) {
            return Scaffold(
              appBar: AppBar(title: const Text('Quotation Details')),
              body: const Center(child: CircularProgressIndicator()),
            );
          }

          if (q == null) {
            return Scaffold(
              appBar: AppBar(title: const Text('Quotation Details')),
              body: Center(
                child: Padding(
                  padding: const EdgeInsets.all(AppSpacing.lg),
                  child: Text(
                    provider.error ?? 'Quotation not found.',
                    style: AppTextStyles.body,
                    textAlign: TextAlign.center,
                  ),
                ),
              ),
            );
          }

          return Scaffold(
            appBar: AppBar(title: Text('Quotation #${q.id}')),
            body: SingleChildScrollView(
              padding: const EdgeInsets.all(AppSpacing.lg),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  // Status
                  AppCard(
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        const Text('Status', style: AppTextStyles.cardHeading),
                        QuotationStatusBadge(status: q.status),
                      ],
                    ),
                  ),
                  const SizedBox(height: AppSpacing.md),

                  // AI Risk Assessment
                  if (q.riskAssessment != null && q.riskAssessment!.hasData) ...[
                    RiskAssessmentCard(assessment: q.riskAssessment!),
                    const SizedBox(height: AppSpacing.md),
                  ],

                  // Line items
                  AppCard(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text('Line Items',
                            style: AppTextStyles.cardHeading),
                        const SizedBox(height: AppSpacing.sm),
                        ...q.items.map(
                          (item) => Padding(
                            padding:
                                const EdgeInsets.symmetric(vertical: 6),
                            child: Row(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment:
                                        CrossAxisAlignment.start,
                                    children: [
                                      Text(
                                        item.description,
                                        style: AppTextStyles.body.copyWith(
                                          fontWeight: FontWeight.w600,
                                        ),
                                      ),
                                      const SizedBox(height: 2),
                                      Text(
                                        'Rs. ${item.amount.toStringAsFixed(2)} × ${item.quantity}',
                                        style: AppTextStyles.small.copyWith(
                                          color: AppColors.textSecondary,
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                                Text(
                                  'Rs. ${item.lineTotal.toStringAsFixed(2)}',
                                  style: AppTextStyles.body.copyWith(
                                    fontWeight: FontWeight.w600,
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ),
                        const Divider(
                          height: AppSpacing.lg,
                          color: AppColors.border,
                        ),
                        Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            const Text(
                              'Total',
                              style: AppTextStyles.cardHeading,
                            ),
                            Text(
                              'Rs. ${q.totalAmount.toStringAsFixed(2)}',
                              style: AppTextStyles.cardHeading.copyWith(
                                color: AppColors.primary,
                              ),
                            ),
                          ],
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: AppSpacing.md),

                  // Actions
                  if (q.canApprove) ...[
                    AppButton(
                      text: 'Approve Quotation',
                      isLoading: provider.isSubmitting,
                      onPressed: () => _onApprove(q),
                    ),
                    const SizedBox(height: AppSpacing.sm),
                    OutlinedButton(
                      onPressed: provider.isSubmitting
                          ? null
                          : () => _onReject(q),
                      style: OutlinedButton.styleFrom(
                        foregroundColor: AppColors.error,
                        side: const BorderSide(color: AppColors.error),
                        minimumSize: const Size(double.infinity, 48),
                        shape: RoundedRectangleBorder(
                          borderRadius:
                              BorderRadius.circular(AppRadius.medium),
                        ),
                      ),
                      child: const Text('Reject Quotation'),
                    ),
                  ] else if (q.status.isApproved) ...[
                    _StatusBanner(
                      icon: Icons.check_circle_rounded,
                      color: AppColors.success,
                      message: 'This quotation has been approved.',
                    ),
                  ] else if (q.status.isRejected) ...[
                    _StatusBanner(
                      icon: Icons.cancel_rounded,
                      color: AppColors.error,
                      message: 'This quotation has been rejected.',
                    ),
                  ] else if (q.status.isDraft) ...[
                    _StatusBanner(
                      icon: Icons.edit_note_rounded,
                      color: AppColors.textSecondary,
                      message:
                          'The provider is still preparing this quotation.',
                    ),
                  ],
                ],
              ),
            ),
          );
        },
      ),
    );
  }
}

class _StatusBanner extends StatelessWidget {
  final IconData icon;
  final Color color;
  final String message;

  const _StatusBanner({
    required this.icon,
    required this.color,
    required this.message,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(AppRadius.medium),
        border: Border.all(color: color.withValues(alpha: 0.25)),
      ),
      child: Row(
        children: [
          Icon(icon, color: color),
          const SizedBox(width: AppSpacing.sm),
          Expanded(
            child: Text(
              message,
              style: AppTextStyles.body.copyWith(
                color: color,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ],
      ),
    );
  }
}