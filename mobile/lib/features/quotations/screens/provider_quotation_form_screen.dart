import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../shared/theme/app_colors.dart';
import '../../../shared/theme/app_spacing.dart';
import '../../../shared/theme/app_text_styles.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_card.dart';
import '../models/quotation_item_model.dart';
import '../providers/quotation_provider.dart';

class ProviderQuotationFormScreen extends StatefulWidget {
  final String serviceRequestId;
  final String providerId;
  final String customerName;
  final String serviceDescription;
  final QuotationProvider provider;

  const ProviderQuotationFormScreen({
    super.key,
    required this.serviceRequestId,
    required this.providerId,
    required this.customerName,
    required this.serviceDescription,
    required this.provider,
  });

  @override
  State<ProviderQuotationFormScreen> createState() =>
      _ProviderQuotationFormScreenState();
}

class _ProviderQuotationFormScreenState
    extends State<ProviderQuotationFormScreen> {
  final _notesController = TextEditingController();

  final _serviceChargeController = TextEditingController();
  final _visitingChargeController = TextEditingController(text: '0');

  @override
  void dispose() {
    _notesController.dispose();
    _serviceChargeController.dispose();
    _visitingChargeController.dispose();
    super.dispose();
  }

  double get _total {
    final serviceCharge =
        double.tryParse(_serviceChargeController.text.trim()) ?? 0;
    final visitingCharge =
        double.tryParse(_visitingChargeController.text.trim()) ?? 0;
    return serviceCharge + visitingCharge;
  }

  String? _validate() {
    final serviceCharge = double.tryParse(_serviceChargeController.text.trim());
    if (serviceCharge == null ||
        !serviceCharge.isFinite ||
        serviceCharge <= 0) {
      return 'Enter a valid service charge greater than zero';
    }

    final visitingCharge = double.tryParse(
      _visitingChargeController.text.trim(),
    );
    if (visitingCharge == null ||
        !visitingCharge.isFinite ||
        visitingCharge < 0) {
      return 'Enter a valid visiting charge (use 0 if not applicable)';
    }

    if (_notesController.text.trim().length > 1000) {
      return 'Notes must be 1000 characters or fewer';
    }
    return null;
  }

  Future<void> _submit() async {
    if (widget.provider.isSubmitting) return;

    if (widget.provider.hasUnsentProviderDraft) {
      await _sendQuotation(retrySavedDraft: true);
      return;
    }

    final error = _validate();
    if (error != null) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(error), backgroundColor: AppColors.error),
      );
      return;
    }

    final serviceCharge = double.parse(_serviceChargeController.text.trim());
    final visitingCharge = double.parse(_visitingChargeController.text.trim());
    final items = <QuotationItemModel>[
      QuotationItemModel(
        id: 0,
        description: 'Service charge',
        amount: serviceCharge,
        quantity: 1,
      ),
      if (visitingCharge > 0)
        QuotationItemModel(
          id: 0,
          description: 'Visiting charge',
          amount: visitingCharge,
          quantity: 1,
        ),
    ];

    final shouldSend = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Review quotation'),
        content: SizedBox(
          width: 420,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('Send this quotation to ${widget.customerName}?'),
              const SizedBox(height: 12),
              ...items.map(
                (item) => Padding(
                  padding: const EdgeInsets.only(bottom: 6),
                  child: Row(
                    children: [
                      Expanded(
                        child: Text(
                          item.description,
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                      const SizedBox(width: 12),
                      Text('Rs. ${item.lineTotal.toStringAsFixed(2)}'),
                    ],
                  ),
                ),
              ),
              const Divider(),
              Align(
                alignment: Alignment.centerRight,
                child: Text(
                  'Total: Rs. ${_total.toStringAsFixed(2)}',
                  style: const TextStyle(fontWeight: FontWeight.bold),
                ),
              ),
              const SizedBox(height: 8),
              const Text(
                'The quotation will be checked by the quotation assistant '
                'and sent to the customer for approval.',
              ),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext, false),
            child: const Text('Review again'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(dialogContext, true),
            child: const Text('Send to customer'),
          ),
        ],
      ),
    );
    if (shouldSend != true || !mounted) return;

    await _sendQuotation(items: items, notes: _notesController.text.trim());
  }

  Future<void> _sendQuotation({
    bool retrySavedDraft = false,
    List<QuotationItemModel>? items,
    String? notes,
  }) async {
    final result = retrySavedDraft
        ? await widget.provider.retrySendProviderQuotation()
        : await widget.provider.createAndSendQuotation(
            serviceRequestId: widget.serviceRequestId,
            providerId: widget.providerId,
            items: items!,
            notes: notes,
          );

    if (!mounted) return;

    if (result != null) {
      await showDialog<void>(
        context: context,
        builder: (dialogContext) => AlertDialog(
          icon: const Icon(Icons.check_circle, color: AppColors.success),
          title: const Text('Quotation sent'),
          content: Text(
            '${widget.customerName} can now review and approve your '
            'quotation. Total: Rs. ${result.totalAmount.toStringAsFixed(2)}.',
          ),
          actions: [
            FilledButton(
              onPressed: () => Navigator.pop(dialogContext),
              child: const Text('Done'),
            ),
          ],
        ),
      );
      if (!mounted) return;
      Navigator.of(context).pop(true);
    } else {
      final hasSavedDraft = widget.provider.hasUnsentProviderDraft;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            hasSavedDraft
                ? 'Quotation saved, but could not be sent. Tap “Retry sending” to avoid creating a duplicate. ${widget.provider.error ?? ''}'
                : widget.provider.error ?? 'Failed to send quotation.',
          ),
          backgroundColor: AppColors.error,
          duration: const Duration(seconds: 6),
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider<QuotationProvider>.value(
      value: widget.provider,
      child: Consumer<QuotationProvider>(
        builder: (context, provider, _) {
          return Scaffold(
            appBar: AppBar(
              title: const Text('Create quotation'),
              backgroundColor: AppColors.surface,
              foregroundColor: AppColors.textPrimary,
            ),
            body: ListView(
              padding: const EdgeInsets.all(AppSpacing.lg),
              children: [
                AppCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Row(
                        children: [
                          Icon(Icons.person_outline, color: AppColors.primary),
                          SizedBox(width: AppSpacing.xs),
                          Text(
                            'Quotation for',
                            style: AppTextStyles.cardHeading,
                          ),
                        ],
                      ),
                      const SizedBox(height: AppSpacing.xs),
                      Text(widget.customerName, style: AppTextStyles.body),
                      const SizedBox(height: AppSpacing.sm),
                      Text(
                        widget.serviceDescription,
                        style: AppTextStyles.small.copyWith(
                          color: AppColors.textSecondary,
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: AppSpacing.md),

                const Text('Price breakdown', style: AppTextStyles.cardHeading),
                const SizedBox(height: AppSpacing.sm),
                _buildChargeField(
                  controller: _serviceChargeController,
                  label: 'Service charge (Rs.)',
                ),
                const SizedBox(height: AppSpacing.sm),
                _buildChargeField(
                  controller: _visitingChargeController,
                  label: 'Visiting charge (Rs.)',
                  helperText: 'Enter 0 if no visiting charge applies.',
                ),
                const Padding(
                  padding: EdgeInsets.only(top: AppSpacing.xs),
                  child: Text(
                    'Total = service charge + visiting charge.',
                    style: AppTextStyles.small,
                  ),
                ),
                const SizedBox(height: AppSpacing.md),

                TextField(
                  controller: _notesController,
                  maxLines: 3,
                  maxLength: 1000,
                  decoration: const InputDecoration(
                    labelText: 'Notes for customer (optional)',
                    border: OutlineInputBorder(),
                    alignLabelWithHint: true,
                  ),
                ),
                const SizedBox(height: AppSpacing.md),

                AppCard(
                  child: Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      const Text('Total', style: AppTextStyles.cardHeading),
                      Text(
                        'Rs. ${_total.toStringAsFixed(2)}',
                        style: AppTextStyles.cardHeading.copyWith(
                          color: AppColors.primary,
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: AppSpacing.lg),

                AppButton(
                  text: provider.hasUnsentProviderDraft
                      ? 'Retry sending saved quotation'
                      : 'Review and send quotation',
                  isLoading: provider.isSubmitting,
                  onPressed: _submit,
                ),
                const SizedBox(height: AppSpacing.sm),
                const Text(
                  'The customer must approve the quotation before a booking '
                  'is confirmed.',
                  textAlign: TextAlign.center,
                  style: AppTextStyles.small,
                ),
              ],
            ),
          );
        },
      ),
    );
  }

  Widget _buildChargeField({
    required TextEditingController controller,
    required String label,
    String? helperText,
  }) {
    return AppCard(
      child: TextField(
        controller: controller,
        keyboardType: const TextInputType.numberWithOptions(decimal: true),
        decoration: InputDecoration(
          labelText: label,
          helperText: helperText,
          border: const OutlineInputBorder(),
        ),
        onChanged: (_) => setState(() {}),
      ),
    );
  }
}
