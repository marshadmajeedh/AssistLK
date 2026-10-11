import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../shared/theme/app_colors.dart';
import '../../../shared/theme/app_spacing.dart';
import '../../../shared/theme/app_text_styles.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/empty_state_card.dart';
import '../../auth/providers/auth_provider.dart';
import '../providers/quotation_provider.dart';
import '../services/quotation_service.dart';
import '../widgets/quotation_card.dart';
import 'quotation_detail_screen.dart';

/// Customer-side screen: shows quotations for the signed-in customer's requests.
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
      if (!mounted) return;
      _load();
    });
  }

  @override
  void dispose() {
    _provider.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    _provider.clearError();
    await _provider.loadForCurrentCustomer();
    if (!mounted) return;
    // Log for debugging in Chrome console
    debugPrint(
      '[QuotationList] openedFromRequestId=${widget.serviceRequestId} '
      'customerQuotationCount=${_provider.quotations.length} '
      'error=${_provider.error}',
    );
  }

  Future<void> _refresh() async {
    _provider.clearError();
    await _provider.loadForCurrentCustomer();
  }

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider<QuotationProvider>.value(
      value: _provider,
      child: Scaffold(
        backgroundColor: AppColors.background,
        body: Consumer<QuotationProvider>(
          builder: (context, provider, _) {
            // Loading state
            if (provider.isLoading && provider.quotations.isEmpty) {
              return const Center(
                child: CircularProgressIndicator(color: AppColors.primary),
              );
            }

            // Error state
            if (provider.error != null && provider.quotations.isEmpty) {
              return _ErrorState(message: provider.error!, onRetry: _load);
            }

            // Empty state
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
                      description: 'When providers send quotations for your service requests, they will appear here for your review.',
                    ),
                  ],
                ),
              );
            }

            // List of quotations
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
                    return Center(
                      child: ConstrainedBox(
                        constraints: const BoxConstraints(maxWidth: 1200),
                        child: Container(
                          padding: const EdgeInsets.all(AppSpacing.md),
                          decoration: BoxDecoration(
                            gradient: const LinearGradient(
                              colors: [
                                AppColors.primary,
                                AppColors.primaryDark,
                              ],
                              begin: Alignment.topLeft,
                              end: Alignment.bottomRight,
                            ),
                            borderRadius: BorderRadius.circular(18),
                            boxShadow: const [
                              BoxShadow(
                                color: Color(0x241F4E78),
                                blurRadius: 14,
                                offset: Offset(0, 6),
                              ),
                            ],
                          ),
                          child: const Row(
                            children: [
                              Icon(
                                Icons.receipt_long_rounded,
                                color: Colors.white,
                                size: 26,
                              ),
                              SizedBox(width: AppSpacing.md),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      'Quotation Details',
                                      style: TextStyle(
                                        color: Colors.white,
                                        fontSize: 18,
                                        fontWeight: FontWeight.w700,
                                      ),
                                    ),
                                    SizedBox(height: AppSpacing.xs),
                                    Text(
                                      'Review the details before you decide',
                                      style: TextStyle(
                                        color: Color(0xFFDCEBFA),
                                        fontSize: 13,
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                    );
                  }
                  final q = provider.quotations[index - 1];
                  return Center(
                    child: ConstrainedBox(
                      constraints: const BoxConstraints(maxWidth: 1200),
                      child: QuotationCard(
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
                      ),
                    ),
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
