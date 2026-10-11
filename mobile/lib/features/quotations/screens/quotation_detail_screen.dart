import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../shared/theme/app_colors.dart';
import '../../../shared/theme/app_radius.dart';
import '../../../shared/theme/app_spacing.dart';
import '../../../shared/theme/app_text_styles.dart';
import '../../../shared/widgets/app_button.dart';
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
      // Still refresh from backend in the background to get latest
      // (including the risk assessment if not present on the initial object).
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted) return;
        _refreshFromBackend();
      });
    } else {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted) return;
        _load();
      });
    }
  }

  Future<void> _load() async {
    _provider.clearError();
    await _provider.loadById(widget.quotationId);
    if (!mounted) return;
    debugPrint(
      '[QuotationDetail] id=${widget.quotationId} '
      'error=${_provider.error}',
    );
  }

  Future<void> _refreshFromBackend() async {
    // Best-effort refresh; keeps the initial data if the fetch fails.
    try {
      await _provider.loadById(widget.quotationId);
    } catch (_) {
      // silent — the initial object is still showing
    }
  }

  @override
  void dispose() {
    _provider.dispose();
    super.dispose();
  }

  Future<void> _onApprove(QuotationModel q) async {
    if (q.workflowThreadId == null) {
      _snack(
        'This quotation is not ready for approval yet.',
        AppColors.warning,
      );
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
      _snack(
        'Quotation rejected. The provider has been notified.',
        AppColors.textSecondary,
      );
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

          // Loading without data
          if (q == null && provider.isLoading) {
            return Scaffold(
              backgroundColor: AppColors.background,
              appBar: _buildAppBar('Quotation Details'),
              body: const Center(
                child: CircularProgressIndicator(color: AppColors.primary),
              ),
            );
          }

          // No data + no loading
          if (q == null) {
            return Scaffold(
              backgroundColor: AppColors.background,
              appBar: _buildAppBar('Quotation Details'),
              body: Center(
                child: Padding(
                  padding: const EdgeInsets.all(AppSpacing.lg),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      const Icon(
                        Icons.error_outline_rounded,
                        size: 48,
                        color: AppColors.error,
                      ),
                      const SizedBox(height: AppSpacing.md),
                      Text(
                        provider.error ?? 'Quotation not found.',
                        style: AppTextStyles.body,
                        textAlign: TextAlign.center,
                      ),
                      const SizedBox(height: AppSpacing.md),
                      AppButton(text: 'Retry', onPressed: _load),
                    ],
                  ),
                ),
              ),
            );
          }

          // Detail view
          return Scaffold(
            backgroundColor: AppColors.background,
            appBar: _buildAppBar('Quotation #${q.id}'),
            body: LayoutBuilder(
              builder: (context, constraints) {
                final horizontalPadding = constraints.maxWidth >= 900
                    ? AppSpacing.xl
                    : AppSpacing.md;
                return SingleChildScrollView(
                  padding: EdgeInsets.symmetric(
                    horizontal: horizontalPadding,
                    vertical: AppSpacing.lg,
                  ),
                  child: Center(
                    child: ConstrainedBox(
                      constraints: const BoxConstraints(maxWidth: 1000),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          // Status
                          _detailCard(
                            child: Row(
                              children: [
                                Container(
                                  width: 42,
                                  height: 42,
                                  decoration: BoxDecoration(
                                    color: AppColors.primarySurface,
                                    borderRadius: BorderRadius.circular(13),
                                  ),
                                  child: const Icon(
                                    Icons.fact_check_outlined,
                                    color: AppColors.primary,
                                  ),
                                ),
                                const SizedBox(width: AppSpacing.md),
                                const Expanded(
                                  child: Column(
                                    crossAxisAlignment:
                                        CrossAxisAlignment.start,
                                    children: [
                                      Text(
                                        'Quotation status',
                                        style: TextStyle(
                                          color: AppColors.textPrimary,
                                          fontSize: 15,
                                          fontWeight: FontWeight.w700,
                                        ),
                                      ),
                                      SizedBox(height: 3),
                                      Text(
                                        'Current response from the provider',
                                        style: TextStyle(
                                          color: AppColors.textSecondary,
                                          fontSize: 12,
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                                QuotationStatusBadge(status: q.status),
                              ],
                            ),
                          ),
                          const SizedBox(height: AppSpacing.md),

                          // AI Risk Assessment
                          if (q.riskAssessment != null &&
                              q.riskAssessment!.hasData) ...[
                            RiskAssessmentCard(assessment: q.riskAssessment!),
                            const SizedBox(height: AppSpacing.md),
                          ],

                          // Line items
                          _detailCard(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                const Row(
                                  children: [
                                    Icon(
                                      Icons.receipt_long_rounded,
                                      color: AppColors.primary,
                                      size: 20,
                                    ),
                                    SizedBox(width: AppSpacing.sm),
                                    Text(
                                      'Price breakdown',
                                      style: TextStyle(
                                        color: AppColors.textPrimary,
                                        fontSize: 16,
                                        fontWeight: FontWeight.w700,
                                      ),
                                    ),
                                  ],
                                ),
                                const SizedBox(height: AppSpacing.sm),
                                ...q.items.indexed.map((entry) {
                                  final (index, item) = entry;
                                  return Padding(
                                    padding: const EdgeInsets.only(top: 8),
                                    child: Container(
                                      padding: const EdgeInsets.all(
                                        AppSpacing.sm,
                                      ),
                                      decoration: BoxDecoration(
                                        color: const Color(0xFFF8FAFC),
                                        borderRadius: BorderRadius.circular(12),
                                        border: Border.all(
                                          color: const Color(0xFFE8EEF5),
                                        ),
                                      ),
                                      child: Row(
                                        crossAxisAlignment:
                                            CrossAxisAlignment.center,
                                        children: [
                                          Container(
                                            width: 30,
                                            height: 30,
                                            alignment: Alignment.center,
                                            decoration: BoxDecoration(
                                              color: AppColors.primarySurface,
                                              borderRadius:
                                                  BorderRadius.circular(9),
                                            ),
                                            child: Text(
                                              '${index + 1}',
                                              style: const TextStyle(
                                                color: AppColors.primary,
                                                fontSize: 12,
                                                fontWeight: FontWeight.w700,
                                              ),
                                            ),
                                          ),
                                          const SizedBox(width: AppSpacing.sm),
                                          Expanded(
                                            child: Column(
                                              crossAxisAlignment:
                                                  CrossAxisAlignment.start,
                                              children: [
                                                Text(
                                                  item.description,
                                                  style: AppTextStyles.body
                                                      .copyWith(
                                                        fontWeight:
                                                            FontWeight.w600,
                                                      ),
                                                ),
                                                const SizedBox(height: 3),
                                                Text(
                                                  'Rs. ${item.amount.toStringAsFixed(2)} × ${item.quantity}',
                                                  style: AppTextStyles.small
                                                      .copyWith(
                                                        color: AppColors
                                                            .textSecondary,
                                                      ),
                                                ),
                                              ],
                                            ),
                                          ),
                                          const SizedBox(width: AppSpacing.sm),
                                          Text(
                                            'Rs. ${item.lineTotal.toStringAsFixed(2)}',
                                            style: AppTextStyles.body.copyWith(
                                              color: AppColors.textPrimary,
                                              fontWeight: FontWeight.w700,
                                            ),
                                          ),
                                        ],
                                      ),
                                    ),
                                  );
                                }),
                                const SizedBox(height: AppSpacing.md),
                                Container(
                                  padding: const EdgeInsets.symmetric(
                                    horizontal: AppSpacing.md,
                                    vertical: AppSpacing.md,
                                  ),
                                  decoration: BoxDecoration(
                                    color: AppColors.primarySurface,
                                    borderRadius: BorderRadius.circular(13),
                                    border: Border.all(
                                      color: AppColors.primary.withValues(
                                        alpha: 0.12,
                                      ),
                                    ),
                                  ),
                                  child: Row(
                                    mainAxisAlignment:
                                        MainAxisAlignment.spaceBetween,
                                    children: [
                                      const Text(
                                        'Total',
                                        style: TextStyle(
                                          color: AppColors.textPrimary,
                                          fontSize: 15,
                                          fontWeight: FontWeight.w700,
                                        ),
                                      ),
                                      Text(
                                        'Rs. ${q.totalAmount.toStringAsFixed(2)}',
                                        style: const TextStyle(
                                          color: AppColors.primary,
                                          fontSize: 19,
                                          fontWeight: FontWeight.w800,
                                        ),
                                      ),
                                    ],
                                  ),
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
                                  borderRadius: BorderRadius.circular(
                                    AppRadius.medium,
                                  ),
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
                              message: 'The provider is still preparing this quotation.',
                            ),
                          ] else if (q.status.isPendingApproval &&
                              q.workflowThreadId == null) ...[
                            _StatusBanner(
                              icon: Icons.hourglass_top_rounded,
                              color: AppColors.warning,
                              message: 'Waiting for the provider to submit this quotation for your approval.',
                            ),
                          ],
                        ],
                      ),
                    ),
                  ),
                );
              },
            ),
          );
        },
      ),
    );
  }

  PreferredSizeWidget _buildAppBar(String title) {
    return AppBar(
      title: Text(title),
      backgroundColor: AppColors.primary,
      foregroundColor: Colors.white,
      elevation: 0,
      scrolledUnderElevation: 0,
      centerTitle: false,
    );
  }

  Widget _detailCard({required Widget child}) {
    return Container(
      padding: const EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: const Color(0xFFE2E8F0)),
        boxShadow: const [
          BoxShadow(
            color: Color(0x0D182230),
            blurRadius: 14,
            offset: Offset(0, 4),
          ),
        ],
      ),
      child: child,
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
