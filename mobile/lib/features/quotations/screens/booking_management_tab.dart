import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../auth/providers/auth_provider.dart';
import '../models/booking_model.dart';
import '../providers/quotation_provider.dart';
import '../services/quotation_service.dart';
import 'provider_quotation_form_screen.dart';

class BookingManagementTab extends StatefulWidget {
  const BookingManagementTab({super.key});

  @override
  State<BookingManagementTab> createState() => _BookingManagementTabState();
}

class _BookingManagementTabState extends State<BookingManagementTab> {
  static const primaryColor = Color(0xFF1F4E78);
  static const primaryDark = Color(0xFF173B5E);
  static const pageColor = Color(0xFFF4F7FB);
  static const successColor = Color(0xFF18804A);
  static const successSurface = Color(0xFFEAF7EF);
  static const mutedText = Color(0xFF667085);

  late final QuotationProvider _provider;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthProvider>();
    _provider = QuotationProvider(
      quotationService: QuotationService(apiClient: auth.authService.apiClient),
    );
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) _provider.loadProviderBookings();
    });
  }

  @override
  void dispose() {
    _provider.dispose();
    super.dispose();
  }

  Future<void> _refresh() => _provider.loadProviderBookings();

  Future<void> _createQuotation(ProviderBookingSummaryModel booking) async {
    final serviceRequestId = booking.serviceRequestId;
    if (serviceRequestId == null || serviceRequestId.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('This request is missing its service request ID.'),
        ),
      );
      return;
    }
    if (booking.providerId.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Your provider profile could not be identified.'),
        ),
      );
      return;
    }

    final sent = await Navigator.of(context).push<bool>(
      MaterialPageRoute(
        builder: (_) => ProviderQuotationFormScreen(
          serviceRequestId: serviceRequestId,
          providerId: booking.providerId,
          customerName: booking.customerName,
          serviceDescription: booking.serviceTitle,
          provider: _provider,
        ),
      ),
    );
    if (sent == true && mounted) await _refresh();
  }

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider<QuotationProvider>.value(
      value: _provider,
      child: Consumer<QuotationProvider>(
        builder: (context, provider, _) {
          return Scaffold(
            backgroundColor: pageColor,
            body: DefaultTabController(
              length: 2,
              child: Column(
                children: [
                  _buildHeader(provider),
                  Expanded(
                    child: TabBarView(
                      children: [
                        _buildList(
                          provider.providerBookings,
                          provider.isProviderBookingsLoading,
                          provider.providerBookingsError,
                        ),
                        _buildList(
                          provider.providerCompletedBookings,
                          provider.isProviderBookingsLoading,
                          provider.providerBookingsError,
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          );
        },
      ),
    );
  }

  // ─────────────────────────────────────────────────────────────
  // Header
  // ─────────────────────────────────────────────────────────────

  Widget _buildHeader(QuotationProvider provider) {
    final upcoming = provider.providerBookings;
    final completed = provider.providerCompletedBookings;
    final needsQuotation = upcoming
        .where((booking) => booking.isPendingQuote)
        .length;

    return Container(
      width: double.infinity,
      color: Colors.white,
      child: SafeArea(
        bottom: false,
        child: LayoutBuilder(
          builder: (context, constraints) {
            final horizontalPadding = constraints.maxWidth >= 900 ? 40.0 : 20.0;
            return Center(
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 1240),
                child: Padding(
                  padding: EdgeInsets.fromLTRB(
                    horizontalPadding,
                    22,
                    horizontalPadding,
                    0,
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          Container(
                            width: 46,
                            height: 46,
                            decoration: BoxDecoration(
                              color: const Color(0xFFEAF1F8),
                              borderRadius: BorderRadius.circular(14),
                            ),
                            child: const Icon(
                              Icons.calendar_month_rounded,
                              color: primaryColor,
                              size: 24,
                            ),
                          ),
                          const SizedBox(width: 14),
                          const Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  'My Bookings',
                                  style: TextStyle(
                                    color: Color(0xFF182230),
                                    fontSize: 24,
                                    fontWeight: FontWeight.w800,
                                    letterSpacing: -0.5,
                                  ),
                                ),
                                SizedBox(height: 3),
                                Text(
                                  'Keep track of customer requests and service appointments.',
                                  style: TextStyle(
                                    color: mutedText,
                                    fontSize: 13,
                                  ),
                                ),
                              ],
                            ),
                          ),
                          const SizedBox(width: 8),
                          if (constraints.maxWidth < 540)
                            IconButton.outlined(
                              onPressed: _refresh,
                              tooltip: 'Refresh bookings',
                              icon: const Icon(Icons.refresh_rounded),
                              color: primaryColor,
                            )
                          else
                            OutlinedButton.icon(
                              onPressed: _refresh,
                              icon: const Icon(Icons.refresh_rounded, size: 18),
                              label: const Text('Refresh'),
                              style: OutlinedButton.styleFrom(
                                foregroundColor: primaryColor,
                                side: const BorderSide(
                                  color: Color(0xFFD8E1EB),
                                ),
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 16,
                                  vertical: 13,
                                ),
                                shape: RoundedRectangleBorder(
                                  borderRadius: BorderRadius.circular(12),
                                ),
                              ),
                            ),
                        ],
                      ),
                      const SizedBox(height: 20),
                      LayoutBuilder(
                        builder: (context, metricConstraints) {
                          final metrics = [
                            (
                              'Upcoming',
                              upcoming.length,
                              Icons.event_available_rounded,
                              primaryColor,
                            ),
                            (
                              'Needs quotation',
                              needsQuotation,
                              Icons.receipt_long_rounded,
                              const Color(0xFFB86B12),
                            ),
                            (
                              'Completed',
                              completed.length,
                              Icons.task_alt_rounded,
                              successColor,
                            ),
                          ];
                          if (metricConstraints.maxWidth < 560) {
                            final width = (metricConstraints.maxWidth - 8) / 2;
                            return Wrap(
                              spacing: 8,
                              runSpacing: 8,
                              children: [
                                for (final metric in metrics)
                                  SizedBox(
                                    width: width,
                                    child: _summaryMetric(
                                      metric.$1,
                                      metric.$2,
                                      metric.$3,
                                      metric.$4,
                                    ),
                                  ),
                              ],
                            );
                          }
                          const gap = 12.0;
                          final width =
                              (metricConstraints.maxWidth - gap * 2) / 3;
                          return Row(
                            children: [
                              for (var i = 0; i < metrics.length; i++) ...[
                                if (i > 0) const SizedBox(width: gap),
                                SizedBox(
                                  width: width,
                                  child: _summaryMetric(
                                    metrics[i].$1,
                                    metrics[i].$2,
                                    metrics[i].$3,
                                    metrics[i].$4,
                                  ),
                                ),
                              ],
                            ],
                          );
                        },
                      ),
                      const SizedBox(height: 20),
                      Container(
                        padding: const EdgeInsets.all(5),
                        decoration: BoxDecoration(
                          color: primaryDark,
                          borderRadius: BorderRadius.circular(14),
                        ),
                        child: TabBar(
                          indicatorSize: TabBarIndicatorSize.tab,
                          indicator: BoxDecoration(
                            color: const Color(0xFF3478B8),
                            borderRadius: BorderRadius.circular(10),
                            boxShadow: const [
                              BoxShadow(
                                color: Color(0x260B2540),
                                blurRadius: 5,
                                offset: Offset(0, 2),
                              ),
                            ],
                          ),
                          dividerColor: Colors.transparent,
                          labelColor: Colors.white,
                          unselectedLabelColor: const Color(0xFFD9EAF8),
                          labelStyle: const TextStyle(
                            fontWeight: FontWeight.w700,
                            fontSize: 14,
                          ),
                          unselectedLabelStyle: const TextStyle(
                            fontWeight: FontWeight.w600,
                            fontSize: 14,
                          ),
                          tabs: [
                            Tab(text: 'Upcoming  ·  ${upcoming.length}'),
                            Tab(text: 'Completed  ·  ${completed.length}'),
                          ],
                        ),
                      ),
                      const SizedBox(height: 20),
                    ],
                  ),
                ),
              ),
            );
          },
        ),
      ),
    );
  }

  Widget _summaryMetric(String label, int value, IconData icon, Color color) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
      decoration: BoxDecoration(
        color: const Color(0xFFFAFBFC),
        border: Border.all(color: const Color(0xFFE7ECF2)),
        borderRadius: BorderRadius.circular(14),
      ),
      child: Row(
        children: [
          Container(
            width: 36,
            height: 36,
            decoration: BoxDecoration(
              color: color.withValues(alpha: 0.10),
              borderRadius: BorderRadius.circular(11),
            ),
            child: Icon(icon, color: color, size: 19),
          ),
          const SizedBox(width: 11),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  value.toString(),
                  style: const TextStyle(
                    color: Color(0xFF182230),
                    fontSize: 19,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                Text(
                  label,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(color: mutedText, fontSize: 11.5),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  // ─────────────────────────────────────────────────────────────
  // List + states
  // ─────────────────────────────────────────────────────────────

  Widget _buildList(
    List<ProviderBookingSummaryModel> items,
    bool isLoading,
    String? error,
  ) {
    if (isLoading && items.isEmpty) {
      return const Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            SizedBox(
              width: 28,
              height: 28,
              child: CircularProgressIndicator(
                strokeWidth: 3,
                color: primaryColor,
              ),
            ),
            SizedBox(height: 14),
            Text(
              'Loading your bookings...',
              style: TextStyle(color: mutedText, fontSize: 13),
            ),
          ],
        ),
      );
    }
    if (error != null && items.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 420),
            child: Container(
              padding: const EdgeInsets.all(24),
              decoration: BoxDecoration(
                color: Colors.white,
                border: Border.all(color: const Color(0xFFE5EAF0)),
                borderRadius: BorderRadius.circular(18),
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Container(
                    width: 54,
                    height: 54,
                    decoration: BoxDecoration(
                      color: const Color(0xFFFEF3F2),
                      borderRadius: BorderRadius.circular(16),
                    ),
                    child: const Icon(
                      Icons.cloud_off_outlined,
                      color: Color(0xFFB42318),
                    ),
                  ),
                  const SizedBox(height: 14),
                  const Text(
                    'Could not load bookings',
                    style: TextStyle(
                      color: Color(0xFF182230),
                      fontSize: 17,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: 6),
                  Text(
                    error,
                    textAlign: TextAlign.center,
                    style: const TextStyle(color: mutedText, fontSize: 13),
                  ),
                  const SizedBox(height: 16),
                  FilledButton.icon(
                    onPressed: _refresh,
                    icon: const Icon(Icons.refresh_rounded, size: 18),
                    label: const Text('Try again'),
                    style: FilledButton.styleFrom(
                      backgroundColor: primaryColor,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      );
    }
    if (items.isEmpty) {
      return _buildEmptyState();
    }
    return RefreshIndicator(
      onRefresh: _refresh,
      child: ListView.separated(
        padding: const EdgeInsets.all(16),
        itemCount: items.length,
        separatorBuilder: (_, _) => const SizedBox(height: 14),
        itemBuilder: (context, i) => Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 1240),
            child: _buildBookingCard(items[i]),
          ),
        ),
      ),
    );
  }

  // ─────────────────────────────────────────────────────────────
  // Provider can create a quotation for a matched request.
  // ─────────────────────────────────────────────────────────────

  Widget _buildBookingCard(ProviderBookingSummaryModel b) {
    final isPending = b.isPendingQuote;
    final (statusColor, statusBg) = _statusColors(b);

    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: const Color(0xFFE5EAF0)),
        boxShadow: [
          BoxShadow(
            color: const Color(0xFF182230).withValues(alpha: 0.045),
            blurRadius: 18,
            offset: const Offset(0, 5),
          ),
        ],
      ),
      padding: const EdgeInsets.all(18),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // ─── Top row: status + amount ─────────────────────
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: 10,
                  vertical: 4,
                ),
                decoration: BoxDecoration(
                  color: statusBg,
                  borderRadius: BorderRadius.circular(20),
                ),
                child: Text(
                  b.statusLabel.toUpperCase(),
                  style: TextStyle(
                    fontSize: 11,
                    fontWeight: FontWeight.w800,
                    letterSpacing: 0.5,
                    color: statusColor,
                  ),
                ),
              ),
              Text(
                'Rs. ${b.amount.toStringAsFixed(0)}',
                style: const TextStyle(
                  fontSize: 18,
                  fontWeight: FontWeight.w900,
                  color: Color(0xFF182230),
                ),
              ),
            ],
          ),
          const SizedBox(height: 14),

          if (isPending) ...[
            SizedBox(
              width: double.infinity,
              child: FilledButton.icon(
                onPressed: () => _createQuotation(b),
                icon: const Icon(Icons.receipt_long_outlined),
                label: Text(
                  b.statusLabel == 'DRAFT QUOTE'
                      ? 'Edit / Send Quotation'
                      : 'Create Quotation',
                ),
                style: FilledButton.styleFrom(
                  backgroundColor: primaryColor,
                  foregroundColor: Colors.white,
                  padding: const EdgeInsets.symmetric(vertical: 13),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                  ),
                ),
              ),
            ),
            const SizedBox(height: 14),
          ],

          // ─── Customer block ───────────────────────────────
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Avatar
              Container(
                width: 46,
                height: 46,
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    colors: [
                      primaryColor.withValues(alpha: 0.15),
                      primaryColor.withValues(alpha: 0.06),
                    ],
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                  ),
                  shape: BoxShape.circle,
                ),
                alignment: Alignment.center,
                child: Text(
                  b.customerName.isNotEmpty
                      ? b.customerName.substring(0, 1).toUpperCase()
                      : '?',
                  style: const TextStyle(
                    color: primaryColor,
                    fontWeight: FontWeight.w900,
                    fontSize: 18,
                  ),
                ),
              ),
              const SizedBox(width: 12),

              // Name + service + location
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      b.customerName,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.w800,
                        color: Color(0xFF182230),
                        letterSpacing: 0.1,
                      ),
                    ),
                    const SizedBox(height: 3),
                    Row(
                      children: [
                        Icon(
                          Icons.handyman_outlined,
                          size: 13,
                          color: primaryColor.withValues(alpha: 0.7),
                        ),
                        const SizedBox(width: 4),
                        Flexible(
                          child: Text(
                            b.serviceTitle,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(
                              fontSize: 12.5,
                              fontWeight: FontWeight.w700,
                              color: Color(0xFF475467),
                            ),
                          ),
                        ),
                      ],
                    ),
                    if (b.locationText.isNotEmpty) ...[
                      const SizedBox(height: 5),
                      Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Icon(
                            Icons.location_on,
                            size: 14,
                            color: Color(0xFF667085),
                          ),
                          const SizedBox(width: 5),
                          Expanded(
                            child: Text(
                              b.locationText,
                              maxLines: 2,
                              overflow: TextOverflow.ellipsis,
                              style: const TextStyle(
                                fontSize: 12,
                                height: 1.35,
                                fontWeight: FontWeight.w600,
                                color: Color(0xFF475467),
                              ),
                            ),
                          ),
                        ],
                      ),
                    ],
                  ],
                ),
              ),
            ],
          ),

          // ─── Bottom: date / time ──────────────────────────
          if (b.scheduledAt != null) ...[
            const SizedBox(height: 14),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
              decoration: BoxDecoration(
                color: const Color(0xFFF7F9FC),
                borderRadius: BorderRadius.circular(10),
                border: Border.all(color: const Color(0xFFE7ECF2), width: 1),
              ),
              child: Row(
                children: [
                  _buildIconLabel(
                    Icons.calendar_today_rounded,
                    _formatDate(b.scheduledAt!),
                  ),
                  const SizedBox(width: 14),
                  _buildIconLabel(
                    Icons.access_time_rounded,
                    _formatTime(b.scheduledAt!),
                  ),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }

  (Color, Color) _statusColors(ProviderBookingSummaryModel booking) {
    final label = booking.statusLabel.trim().toUpperCase();
    if (booking.isPendingQuote) {
      return (const Color(0xFFB86B12), const Color(0xFFFFF4E5));
    }
    if (label == 'CONFIRMED' ||
        label.contains('AWAITING CUSTOMER') ||
        label.contains('WAITING FOR CUSTOMER')) {
      return (successColor, successSurface);
    }
    if (label.contains('COMPLETED')) {
      return (const Color(0xFF175CD3), const Color(0xFFEFF8FF));
    }
    if (label.contains('CANCEL') || label.contains('REJECT')) {
      return (const Color(0xFFB42318), const Color(0xFFFEF3F2));
    }
    return (primaryColor, const Color(0xFFEAF1F8));
  }

  Widget _buildIconLabel(IconData icon, String label) {
    return Row(
      children: [
        Icon(icon, size: 14, color: primaryColor),
        const SizedBox(width: 5),
        Text(
          label,
          style: const TextStyle(
            fontSize: 12.5,
            fontWeight: FontWeight.w700,
            color: primaryColor,
          ),
        ),
      ],
    );
  }

  Widget _buildEmptyState() {
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Container(
            width: 76,
            height: 76,
            decoration: BoxDecoration(
              color: const Color(0xFFEAF1F8),
              borderRadius: BorderRadius.circular(24),
            ),
            child: const Icon(
              Icons.event_note_rounded,
              size: 36,
              color: primaryColor,
            ),
          ),
          const SizedBox(height: 18),
          const Text(
            'No bookings here yet',
            style: TextStyle(
              fontSize: 17,
              color: Color(0xFF344054),
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 6),
          const Text(
            'New customer requests and appointments will appear here.',
            textAlign: TextAlign.center,
            style: TextStyle(fontSize: 13, color: mutedText),
          ),
        ],
      ),
    );
  }

  String _formatDate(DateTime d) {
    const months = [
      '',
      'Jan',
      'Feb',
      'Mar',
      'Apr',
      'May',
      'Jun',
      'Jul',
      'Aug',
      'Sep',
      'Oct',
      'Nov',
      'Dec',
    ];
    return '${months[d.month]} ${d.day}, ${d.year}';
  }

  String _formatTime(DateTime d) {
    final h = d.hour % 12 == 0 ? 12 : d.hour % 12;
    final m = d.minute.toString().padLeft(2, '0');
    final ap = d.hour < 12 ? 'AM' : 'PM';
    return '$h:$m $ap';
  }
}
