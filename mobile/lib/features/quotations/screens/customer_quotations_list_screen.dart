import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../shared/theme/app_colors.dart';
import '../../../shared/theme/app_spacing.dart';
import '../../../shared/theme/app_text_styles.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/empty_state_card.dart';
import '../../../shared/widgets/section_header.dart';
import '../../auth/providers/auth_provider.dart';
import '../providers/quotation_provider.dart';
import '../services/quotation_service.dart';
import '../widgets/quotation_card.dart';
import 'quotation_detail_screen.dart';

/// Customer-side screen: shows all quotations for a service request.
class CustomerQuotationsListScreen extends StatefulWidget {
  final String serviceRequestId;

  const CustomerQuotationsListScreen({
    super.key,
    required this.serviceRequestId,
  });

  @override
  State<CustomerQuotationsListScreen> createState() =>
      _CustomerQuotationsListScreenState();
}

class _CustomerQuotationsListScreenState
    extends State<CustomerQuotationsListScreen> {
  late final QuotationProvider _provider;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthProvider>();
    _provider = QuotationProvider(
      quotationService: QuotationService(apiClient: auth.authService.apiClient),
    );
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _provider.loadForServiceRequest(widget.serviceRequestId);
    });
  }

  @override
  void dispose() {
    _provider.dispose();
    super.dispose();
  }

  Future<void> _refresh() async {
    await _provider.loadForServiceRequest(widget.serviceRequestId);
  }

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider<QuotationProvider>.value(
      value: _provider,
      child: Scaffold(
        appBar: AppBar(title: const Text('Quotations')),
        body: Consumer<QuotationProvider>(
          builder: (context, provider, _) {
            if (provider.isLoading && provider.quotations.isEmpty) {
              return const Center(child: CircularProgressIndicator());
            }

            if (provider.error != null && provider.quotations.isEmpty) {
              return _ErrorState(
                message: provider.error!,
                onRetry: _refresh,
              );
            }

            if (provider.quotations.isEmpty) {
              return RefreshIndicator(
                onRefresh: _refresh,
                child: ListView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.all(AppSpacing.lg),
                  children: const [
                    SizedBox(height: 40),
                    EmptyStateCard(
                      icon: Icons.receipt_long_outlined,
                      title: 'No quotations yet',
                      description:
                          'When providers respond to this request, their quotations will appear here for your review.',
                    ),
                  ],
                ),
              );
            }

            return RefreshIndicator(
              onRefresh: _refresh,
              child: ListView.separated(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.all(AppSpacing.lg),
                itemCount: provider.quotations.length + 1,
                separatorBuilder: (_, _) =>
                    const SizedBox(height: AppSpacing.sm),
                itemBuilder: (context, index) {
                  if (index == 0) {
                    return const SectionHeader(
                      title: 'Quotations',
                      subtitle: 'Review the details before you decide',
                    );
                  }
                  final q = provider.quotations[index - 1];
                  return QuotationCard(
                    quotation: q,
                    onTap: () {
                      provider.setCurrentQuotation(q);
                      Navigator.of(context).push(
                        MaterialPageRoute<void>(
                          builder: (_) => QuotationDetailScreen(
                            quotationId: q.id,
                            initialQuotation: q,
                          ),
                        ),
                      );
                    },
                  );
                },
              ),
            );
          },
        ),
      ),
    );
  }
}

class _ErrorState extends StatelessWidget {
  final String message;
  final Future<void> Function() onRetry;

  const _ErrorState({required this.message, required this.onRetry});

  @override
  Widget build(BuildContext context) {
    return Center(
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
              message,
              textAlign: TextAlign.center,
              style: AppTextStyles.body,
            ),
            const SizedBox(height: AppSpacing.md),
            AppButton(text: 'Retry', onPressed: onRetry),
          ],
        ),
      ),
    );
  }
}