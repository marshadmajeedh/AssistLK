import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart' as latlong;

import '../auth/providers/auth_provider.dart';
import '../../shared/theme/app_spacing.dart';
import 'providers/provider_dashboard_provider.dart';
import 'services/provider_service.dart';
import 'widgets/job_alert_card.dart';
import 'widgets/update_profile_bottom_sheet.dart';
import 'providers/web_notifications.dart';

class ProviderHomeScreen extends StatefulWidget {
  const ProviderHomeScreen({super.key});

  @override
  State<ProviderHomeScreen> createState() => _ProviderHomeScreenState();
}

class _ProviderHomeScreenState extends State<ProviderHomeScreen> {
  ProviderDashboardProvider? _dashboardProvider;
  bool _hasAcceptedActiveMatch = false;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthProvider>();
    _dashboardProvider = ProviderDashboardProvider(
      providerService: ProviderService(apiClient: auth.authService.apiClient),
    );
    _dashboardProvider?.init();
  }

  @override
  void dispose() {
    _dashboardProvider?.dispose();
    super.dispose();
  }

  Future<void> _onAcceptMatch() async {
    final distanceKm = _dashboardProvider?.activeJobMatch?['distanceKm'] ?? 0.0;

    try {
      await _dashboardProvider?.providerService.acceptActiveDispatch(
        '/api/providers/active-dispatch/accept',
      );
      _dashboardProvider?.clearActiveJobMatch();
      if (mounted) {
        setState(() {
          _hasAcceptedActiveMatch = true;
        });
      }
    } catch (e) {
      debugPrint('Failed to accept match: $e');
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            backgroundColor: Colors.red,
            content: Text('Failed to accept match: $e'),
          ),
        );
      }
      return;
    }

    if (!mounted) return;

    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        backgroundColor: Colors.green,
        duration: Duration(seconds: 2),
        content: Row(
          children: [
            Icon(Icons.check_circle, color: Colors.white),
            SizedBox(width: 8),
            Expanded(
              child: Text(
                'Match Accepted! Creating booking payload...',
                style: TextStyle(fontWeight: FontWeight.bold),
              ),
            ),
          ],
        ),
      ),
    );

    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (sheetContext) {
        return Padding(
          padding: const EdgeInsets.all(AppSpacing.lg),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  const Text(
                    'Component 3 Handoff',
                    style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
                  ),
                  IconButton(
                    icon: const Icon(Icons.close),
                    onPressed: () => Navigator.of(sheetContext).pop(),
                  ),
                ],
              ),
              const Divider(),
              const SizedBox(height: 8),
              const Text(
                'Matched Candidate Contract:',
                style: TextStyle(fontWeight: FontWeight.bold, fontSize: 14),
              ),
              const SizedBox(height: 6),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: Colors.grey.shade100,
                  borderRadius: BorderRadius.circular(8),
                  border: Border.all(color: Colors.grey.shade300),
                ),
                child: Text(
                  '{\n'
                  '  "ServiceRequestId": "d8b4b485-3c0b-401a-849e-f0f116219903",\n'
                  '  "ProviderId": "prov-001",\n'
                  '  "DistanceKm": $distanceKm,\n'
                  '  "Status": "AcceptedForQuotation"\n'
                  '}',
                  style: const TextStyle(fontFamily: 'monospace', fontSize: 13),
                ),
              ),
              const SizedBox(height: 16),
              const Text(
                'The dispatch request has been transferred to the Quotation & Booking subsystem.',
                style: TextStyle(color: Colors.black87, fontSize: 13),
              ),
              const SizedBox(height: 20),
              SizedBox(
                width: double.infinity,
                child: ElevatedButton(
                  style: ElevatedButton.styleFrom(
                    backgroundColor: Colors.green.shade700,
                    foregroundColor: Colors.white,
                    padding: const EdgeInsets.symmetric(vertical: 14),
                  ),
                  onPressed: () {
                    Navigator.of(sheetContext).pop();
                    if (mounted) {
                      setState(() {
                        _hasAcceptedActiveMatch = true;
                      });
                    }
                  },
                  child: const Text('Proceed to Quotation Workspace'),
                ),
              ),
              const SizedBox(height: 10),
            ],
          ),
        );
      },
    );
  }

  Future<void> _onDeclineMatch() async {
    await _dashboardProvider?.declineJob();
  }

  @override
  Widget build(BuildContext context) {
    if (_dashboardProvider == null) {
      return const Scaffold(
        body: Center(child: CircularProgressIndicator()),
      );
    }

    return ChangeNotifierProvider.value(
      value: _dashboardProvider!,
      child: _ProviderHomeView(
        hasAcceptedActiveMatch: _hasAcceptedActiveMatch,
        onAcceptMatch: _onAcceptMatch,
        onDeclineMatch: _onDeclineMatch,
      ),
    );
  }
}

class _ProviderHomeView extends StatelessWidget {
  final bool hasAcceptedActiveMatch;
  final VoidCallback onAcceptMatch;
  final VoidCallback onDeclineMatch;

  const _ProviderHomeView({
    required this.hasAcceptedActiveMatch,
    required this.onAcceptMatch,
    required this.onDeclineMatch,
  });

  Color _getCategoryColor(String category) {
    switch (category.trim().toLowerCase()) {
      case 'plumbing':
        return Colors.blue.shade700;
      case 'electrical':
        return Colors.amber.shade900;
      case 'vehicle assistance':
      case 'vehicleassistance':
        return Colors.deepOrange.shade700;
      case 'appliance repair':
      case 'appliancerepair':
        return Colors.purple.shade700;
      default:
        return Colors.teal.shade700;
    }
  }

  IconData _getCategoryIcon(String category) {
    switch (category.trim().toLowerCase()) {
      case 'plumbing':
        return Icons.plumbing;
      case 'electrical':
        return Icons.electrical_services;
      case 'vehicle assistance':
      case 'vehicleassistance':
        return Icons.car_repair;
      case 'appliance repair':
      case 'appliancerepair':
        return Icons.home_repair_service;
      default:
        return Icons.handyman;
    }
  }

  void _openEditProfileModal(
    BuildContext context,
    ProviderDashboardProvider dashboard,
  ) {
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (_) => ChangeNotifierProvider.value(
        value: dashboard,
        child: const UpdateProfileBottomSheet(),
      ),
    );
  }

  void _openSupportDialog(BuildContext context) {
    showDialog(
      context: context,
      builder: (dialogCtx) => AlertDialog(
        title: const Row(
          children: [
            Icon(Icons.support_agent, color: Colors.blue),
            SizedBox(width: 8),
            Text('AssistLK Support'),
          ],
        ),
        content: const Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'If your registration was rejected or you need assistance updating your trade certificates, reach out to our verification team:',
              style: TextStyle(fontSize: 14),
            ),
            SizedBox(height: 16),
            Row(
              children: [
                Icon(Icons.email_outlined, size: 18, color: Colors.blueGrey),
                SizedBox(width: 8),
                SelectableText(
                  'support@assistlk.com',
                  style: TextStyle(fontWeight: FontWeight.bold),
                ),
              ],
            ),
            SizedBox(height: 8),
            Row(
              children: [
                Icon(Icons.phone_outlined, size: 18, color: Colors.blueGrey),
                SizedBox(width: 8),
                SelectableText(
                  '+94 11 234 5678',
                  style: TextStyle(fontWeight: FontWeight.bold),
                ),
              ],
            ),
            SizedBox(height: 12),
            Text(
              'Verification desk: Mon–Fri, 8:00 AM – 6:00 PM',
              style: TextStyle(fontSize: 12, color: Colors.grey),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogCtx).pop(),
            child: const Text('Close'),
          ),
        ],
      ),
    );
  }

  void _openSettingsDialog(
    BuildContext context,
    ProviderDashboardProvider dashboard,
  ) {
    showDialog(
      context: context,
      builder: (dialogCtx) {
        return StatefulBuilder(
          builder: (context, setDialogState) {
            final permStatus = getNotificationPermissionStatus();
            return AlertDialog(
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(16),
              ),
              title: const Row(
                children: [
                  Icon(Icons.settings, color: Colors.blue),
                  SizedBox(width: 8),
                  Text(
                    'Workspace Settings',
                    style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                  ),
                ],
              ),
              content: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Audio & Alert Preferences',
                    style: TextStyle(
                      fontWeight: FontWeight.bold,
                      fontSize: 13,
                      color: Colors.blueGrey,
                    ),
                  ),
                  const SizedBox(height: 8),
                  SwitchListTile(
                    contentPadding: EdgeInsets.zero,
                    secondary: Icon(
                      dashboard.isVoiceAlertEnabled
                          ? Icons.volume_up
                          : Icons.volume_off,
                      color: dashboard.isVoiceAlertEnabled
                          ? Colors.blue
                          : Colors.grey,
                    ),
                    title: const Text(
                      'AI Voice Alert',
                      style:
                          TextStyle(fontSize: 14, fontWeight: FontWeight.bold),
                    ),
                    subtitle: const Text(
                      'Speaks basic audio notification when a new job arrives',
                      style: TextStyle(fontSize: 12),
                    ),
                    value: dashboard.isVoiceAlertEnabled,
                    onChanged: (val) {
                      dashboard.toggleVoiceAlert(val);
                      setDialogState(() {});
                    },
                  ),
                  const Divider(height: 20),
                  const Text(
                    'Desktop Notifications (Chrome)',
                    style: TextStyle(
                      fontWeight: FontWeight.bold,
                      fontSize: 13,
                      color: Colors.blueGrey,
                    ),
                  ),
                  const SizedBox(height: 8),
                  Row(
                    children: [
                      Icon(
                        permStatus == 'granted'
                            ? Icons.check_circle
                            : Icons.info_outline,
                        color: permStatus == 'granted'
                            ? Colors.green
                            : Colors.orange,
                        size: 18,
                      ),
                      const SizedBox(width: 8),
                      Text(
                        'Status: ${permStatus.toUpperCase()}',
                        style: TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.bold,
                          color: permStatus == 'granted'
                              ? Colors.green.shade800
                              : Colors.orange.shade800,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 10),
                  Wrap(
                    spacing: 8,
                    runSpacing: 6,
                    children: [
                      ElevatedButton.icon(
                        style: ElevatedButton.styleFrom(
                          backgroundColor: Colors.blue.shade700,
                          foregroundColor: Colors.white,
                          padding: const EdgeInsets.symmetric(
                              horizontal: 10, vertical: 8),
                          visualDensity: VisualDensity.compact,
                        ),
                        icon: const Icon(Icons.notifications_active, size: 16),
                        label: const Text('Enable Notifications',
                            style: TextStyle(fontSize: 12)),
                        onPressed: () {
                          requestNotificationPermissions();
                          setDialogState(() {});
                        },
                      ),
                      OutlinedButton.icon(
                        style: OutlinedButton.styleFrom(
                          padding: const EdgeInsets.symmetric(
                              horizontal: 10, vertical: 8),
                          visualDensity: VisualDensity.compact,
                        ),
                        icon: const Icon(Icons.send, size: 16),
                        label: const Text('Test Notification',
                            style: TextStyle(fontSize: 12)),
                        onPressed: () {
                          sendTestNotification();
                        },
                      ),
                    ],
                  ),
                  if (permStatus == 'denied') ...[
                    const SizedBox(height: 10),
                    Container(
                      padding: const EdgeInsets.all(8),
                      decoration: BoxDecoration(
                        color: Colors.red.shade50,
                        borderRadius: BorderRadius.circular(6),
                        border: Border.all(color: Colors.red.shade200),
                      ),
                      child: Text(
                        'Notifications are blocked by Chrome.\nTo enable: Click the tune/padlock icon on the left of the URL bar -> Site settings -> Notifications -> Set to Allow.',
                        style: TextStyle(fontSize: 11, color: Colors.red.shade900),
                      ),
                    ),
                  ],
                ],
              ),
              actions: [
                TextButton(
                  onPressed: () => Navigator.of(dialogCtx).pop(),
                  child: const Text('Done'),
                ),
              ],
            );
          },
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final dashboard = context.watch<ProviderDashboardProvider>();
    final auth = context.read<AuthProvider>();

    // Loading State
    if (dashboard.isProfileLoading) {
      return const Scaffold(
        body: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              CircularProgressIndicator(),
              SizedBox(height: 16),
              Text(
                'Loading provider workspace...',
                style: TextStyle(fontSize: 14, color: Colors.grey),
              ),
            ],
          ),
        ),
      );
    }

    // Objective 1 - Gating State: Pending
    if (dashboard.isPending) {
      return _buildPendingView(context, dashboard, auth);
    }

    // Objective 1 - Gating State: Rejected
    if (dashboard.isRejected) {
      return _buildRejectedView(context, dashboard, auth);
    }

    // Objective 1 & 2 - Verified Technician Active Workspace
    return _buildVerifiedWorkspaceScaffold(context, dashboard, auth);
  }

  Widget _buildPendingView(
    BuildContext context,
    ProviderDashboardProvider dashboard,
    AuthProvider auth,
  ) {
    final categoryColor = _getCategoryColor(dashboard.category);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Provider Portal'),
        actions: [
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: 'Log Out',
            onPressed: () => auth.logout(),
          ),
        ],
      ),
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(AppSpacing.lg),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Container(
                padding: const EdgeInsets.all(24),
                decoration: BoxDecoration(
                  color: Colors.amber.shade50,
                  shape: BoxShape.circle,
                  border: Border.all(color: Colors.amber.shade200, width: 2),
                ),
                child: Icon(
                  Icons.hourglass_top_rounded,
                  size: 64,
                  color: Colors.amber.shade800,
                ),
              ),
              const SizedBox(height: 24),
              const Text(
                'Application Under Review',
                style: TextStyle(
                  fontSize: 22,
                  fontWeight: FontWeight.bold,
                  color: Colors.black87,
                ),
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 12),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 16),
                child: Text(
                  'Your application is currently pending admin verification. You will be notified once your trade license is approved.',
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    fontSize: 14,
                    height: 1.4,
                    color: Colors.grey.shade700,
                  ),
                ),
              ),
              const SizedBox(height: 24),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: Colors.grey.shade50,
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: Colors.grey.shade300),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Expanded(
                          child: Text(
                            dashboard.fullName.isNotEmpty
                                ? dashboard.fullName
                                : 'Technician Profile',
                            style: const TextStyle(
                              fontSize: 16,
                              fontWeight: FontWeight.bold,
                            ),
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                        Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 8,
                            vertical: 4,
                          ),
                          decoration: BoxDecoration(
                            color: Colors.amber.shade100,
                            borderRadius: BorderRadius.circular(6),
                            border: Border.all(color: Colors.amber.shade300),
                          ),
                          child: Text(
                            'PENDING',
                            style: TextStyle(
                              fontSize: 11,
                              fontWeight: FontWeight.bold,
                              color: Colors.amber.shade900,
                            ),
                          ),
                        ),
                      ],
                    ),
                    if (dashboard.businessName.isNotEmpty) ...[
                      const SizedBox(height: 4),
                      Text(
                        dashboard.businessName,
                        style: TextStyle(
                          fontSize: 13,
                          color: Colors.grey.shade700,
                        ),
                      ),
                    ],
                    const Divider(height: 18),
                    Row(
                      children: [
                        Icon(
                          _getCategoryIcon(dashboard.category),
                          size: 18,
                          color: categoryColor,
                        ),
                        const SizedBox(width: 8),
                        Text(
                          'Category: ${dashboard.category}',
                          style: TextStyle(
                            fontSize: 13,
                            fontWeight: FontWeight.w600,
                            color: categoryColor,
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 32),
              SizedBox(
                width: double.infinity,
                child: ElevatedButton.icon(
                  style: ElevatedButton.styleFrom(
                    backgroundColor: Colors.blue.shade700,
                    foregroundColor: Colors.white,
                    padding: const EdgeInsets.symmetric(vertical: 14),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(8),
                    ),
                  ),
                  icon: const Icon(Icons.refresh),
                  label: const Text(
                    'Refresh Status',
                    style: TextStyle(fontSize: 15, fontWeight: FontWeight.bold),
                  ),
                  onPressed: () async {
                    await dashboard.fetchProfile();
                  },
                ),
              ),
              const SizedBox(height: 12),
              SizedBox(
                width: double.infinity,
                child: OutlinedButton.icon(
                  style: OutlinedButton.styleFrom(
                    foregroundColor: Colors.red.shade700,
                    side: BorderSide(color: Colors.red.shade300),
                    padding: const EdgeInsets.symmetric(vertical: 14),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(8),
                    ),
                  ),
                  icon: const Icon(Icons.logout),
                  label: const Text(
                    'Log Out',
                    style: TextStyle(fontSize: 15),
                  ),
                  onPressed: () => auth.logout(),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildRejectedView(
    BuildContext context,
    ProviderDashboardProvider dashboard,
    AuthProvider auth,
  ) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Provider Portal'),
        actions: [
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: 'Log Out',
            onPressed: () => auth.logout(),
          ),
        ],
      ),
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(AppSpacing.lg),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Container(
                padding: const EdgeInsets.all(24),
                decoration: BoxDecoration(
                  color: Colors.red.shade50,
                  shape: BoxShape.circle,
                  border: Border.all(color: Colors.red.shade200, width: 2),
                ),
                child: Icon(
                  Icons.cancel_outlined,
                  size: 64,
                  color: Colors.red.shade700,
                ),
              ),
              const SizedBox(height: 24),
              const Text(
                'Registration Rejected',
                style: TextStyle(
                  fontSize: 22,
                  fontWeight: FontWeight.bold,
                  color: Colors.red,
                ),
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 12),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 16),
                child: Text(
                  'Your provider registration was not approved. This may be due to incomplete trade license documentation or criteria requirements. Please contact our support team for further assistance.',
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    fontSize: 14,
                    height: 1.4,
                    color: Colors.grey.shade700,
                  ),
                ),
              ),
              const SizedBox(height: 24),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: Colors.grey.shade50,
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: Colors.grey.shade300),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Expanded(
                          child: Text(
                            dashboard.fullName.isNotEmpty
                                ? dashboard.fullName
                                : 'Technician Profile',
                            style: const TextStyle(
                              fontSize: 16,
                              fontWeight: FontWeight.bold,
                            ),
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                        Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 8,
                            vertical: 4,
                          ),
                          decoration: BoxDecoration(
                            color: Colors.red.shade100,
                            borderRadius: BorderRadius.circular(6),
                            border: Border.all(color: Colors.red.shade300),
                          ),
                          child: Text(
                            'REJECTED',
                            style: TextStyle(
                              fontSize: 11,
                              fontWeight: FontWeight.bold,
                              color: Colors.red.shade900,
                            ),
                          ),
                        ),
                      ],
                    ),
                    if (dashboard.businessName.isNotEmpty) ...[
                      const SizedBox(height: 4),
                      Text(
                        dashboard.businessName,
                        style: TextStyle(
                          fontSize: 13,
                          color: Colors.grey.shade700,
                        ),
                      ),
                    ],
                  ],
                ),
              ),
              const SizedBox(height: 32),
              SizedBox(
                width: double.infinity,
                child: ElevatedButton.icon(
                  style: ElevatedButton.styleFrom(
                    backgroundColor: Colors.blue.shade700,
                    foregroundColor: Colors.white,
                    padding: const EdgeInsets.symmetric(vertical: 14),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(8),
                    ),
                  ),
                  icon: const Icon(Icons.support_agent),
                  label: const Text(
                    'Contact Support',
                    style: TextStyle(fontSize: 15, fontWeight: FontWeight.bold),
                  ),
                  onPressed: () => _openSupportDialog(context),
                ),
              ),
              const SizedBox(height: 12),
              SizedBox(
                width: double.infinity,
                child: OutlinedButton.icon(
                  style: OutlinedButton.styleFrom(
                    foregroundColor: Colors.red.shade700,
                    side: BorderSide(color: Colors.red.shade300),
                    padding: const EdgeInsets.symmetric(vertical: 14),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(8),
                    ),
                  ),
                  icon: const Icon(Icons.logout),
                  label: const Text(
                    'Log Out',
                    style: TextStyle(fontSize: 15),
                  ),
                  onPressed: () => auth.logout(),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildVerifiedWorkspaceScaffold(
    BuildContext context,
    ProviderDashboardProvider dashboard,
    AuthProvider auth,
  ) {
    final centerLocation = latlong.LatLng(
      dashboard.latitude,
      dashboard.longitude,
    );

    return Scaffold(
      appBar: AppBar(
        title: const Text('Provider Workspace'),
        actions: [
          IconButton(
            icon: const Icon(Icons.settings_outlined),
            tooltip: 'Workspace Settings',
            onPressed: () => _openSettingsDialog(context, dashboard),
          ),
          IconButton(
            icon: const Icon(Icons.refresh),
            tooltip: 'Refresh Status',
            onPressed: () => dashboard.fetchProfile(),
          ),
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: 'Logout',
            onPressed: () async {
              await auth.logout();
            },
          ),
        ],
      ),
      body: dashboard.isLoading
          ? const Center(child: CircularProgressIndicator())
          : Column(
              children: [
                // Identity Header Card
                Padding(
                  padding: const EdgeInsets.fromLTRB(
                    AppSpacing.md,
                    AppSpacing.sm,
                    AppSpacing.md,
                    0,
                  ),
                  child: Card(
                    elevation: 2,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: Padding(
                      padding: const EdgeInsets.all(AppSpacing.md),
                      child: Column(
                        children: [
                          Row(
                            children: [
                              CircleAvatar(
                                radius: 22,
                                backgroundColor: _getCategoryColor(
                                  dashboard.category,
                                ).withValues(alpha: 0.15),
                                child: Icon(
                                  _getCategoryIcon(dashboard.category),
                                  color: _getCategoryColor(dashboard.category),
                                  size: 24,
                                ),
                              ),
                              const SizedBox(width: 12),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      dashboard.fullName.isNotEmpty
                                          ? dashboard.fullName
                                          : 'Technician',
                                      style: const TextStyle(
                                        fontWeight: FontWeight.bold,
                                        fontSize: 16,
                                      ),
                                      maxLines: 1,
                                      overflow: TextOverflow.ellipsis,
                                    ),
                                    if (dashboard.businessName.isNotEmpty)
                                      Text(
                                        dashboard.businessName,
                                        style: TextStyle(
                                          color: Colors.grey.shade700,
                                          fontSize: 13,
                                        ),
                                        maxLines: 1,
                                        overflow: TextOverflow.ellipsis,
                                      ),
                                  ],
                                ),
                              ),
                              OutlinedButton.icon(
                                style: OutlinedButton.styleFrom(
                                  padding: const EdgeInsets.symmetric(
                                    horizontal: 10,
                                    vertical: 6,
                                  ),
                                  visualDensity: VisualDensity.compact,
                                ),
                                icon: const Icon(Icons.edit, size: 14),
                                label: const Text(
                                  'Edit Profile',
                                  style: TextStyle(fontSize: 12),
                                ),
                                onPressed: () =>
                                    _openEditProfileModal(context, dashboard),
                              ),
                            ],
                          ),
                          const SizedBox(height: 10),
                          Row(
                            children: [
                              // Trade Category badge
                              Container(
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 8,
                                  vertical: 4,
                                ),
                                decoration: BoxDecoration(
                                  color: _getCategoryColor(
                                    dashboard.category,
                                  ).withValues(alpha: 0.12),
                                  borderRadius: BorderRadius.circular(6),
                                  border: Border.all(
                                    color: _getCategoryColor(
                                      dashboard.category,
                                    ).withValues(alpha: 0.4),
                                  ),
                                ),
                                child: Row(
                                  mainAxisSize: MainAxisSize.min,
                                  children: [
                                    Icon(
                                      _getCategoryIcon(dashboard.category),
                                      size: 14,
                                      color: _getCategoryColor(
                                        dashboard.category,
                                      ),
                                    ),
                                    const SizedBox(width: 4),
                                    Text(
                                      dashboard.category,
                                      style: TextStyle(
                                        fontSize: 12,
                                        fontWeight: FontWeight.w600,
                                        color: _getCategoryColor(
                                          dashboard.category,
                                        ),
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                              const SizedBox(width: 8),
                              // Verified Chip with Star Rating
                              Container(
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 8,
                                  vertical: 4,
                                ),
                                decoration: BoxDecoration(
                                  color: Colors.green.shade50,
                                  borderRadius: BorderRadius.circular(6),
                                  border: Border.all(
                                    color: Colors.green.shade300,
                                  ),
                                ),
                                child: Row(
                                  mainAxisSize: MainAxisSize.min,
                                  children: [
                                    const Icon(
                                      Icons.verified,
                                      size: 14,
                                      color: Colors.green,
                                    ),
                                    const SizedBox(width: 4),
                                    Text(
                                      'Verified Technician ★ ${dashboard.rating.toStringAsFixed(1)}',
                                      style: TextStyle(
                                        fontSize: 12,
                                        fontWeight: FontWeight.bold,
                                        color: Colors.green.shade800,
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            ],
                          ),
                        ],
                      ),
                    ),
                  ),
                ),

                // Duty Status & Operating Radius card
                Padding(
                  padding: const EdgeInsets.fromLTRB(
                    AppSpacing.md,
                    AppSpacing.xs,
                    AppSpacing.md,
                    AppSpacing.xs,
                  ),
                  child: Card(
                    elevation: 2,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: Padding(
                      padding: const EdgeInsets.symmetric(
                        horizontal: AppSpacing.md,
                        vertical: AppSpacing.sm,
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            mainAxisAlignment: MainAxisAlignment.spaceBetween,
                            children: [
                              Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  const Text(
                                    'Duty Status',
                                    style: TextStyle(
                                      fontWeight: FontWeight.bold,
                                      fontSize: 14,
                                    ),
                                  ),
                                  const SizedBox(height: 2),
                                  Text(
                                    dashboard.isOnline
                                        ? '🟢 ONLINE (Receiving Jobs)'
                                        : '⚫ OFFLINE (Off Duty)',
                                    style: TextStyle(
                                      color: dashboard.isOnline
                                          ? Colors.green.shade700
                                          : Colors.grey.shade600,
                                      fontWeight: FontWeight.bold,
                                      fontSize: 13,
                                    ),
                                  ),
                                ],
                              ),
                              Switch(
                                value: dashboard.isOnline,
                                activeThumbColor: Colors.green,
                                onChanged: (value) =>
                                    dashboard.toggleOnlineStatus(value),
                              ),
                            ],
                          ),
                          const Divider(height: 12),
                          Row(
                            mainAxisAlignment: MainAxisAlignment.spaceBetween,
                            children: [
                              Text(
                                'Operating Radius: ${dashboard.operatingRadiusKm.toInt()} km',
                                style: const TextStyle(
                                  fontWeight: FontWeight.w600,
                                  fontSize: 13,
                                ),
                              ),
                            ],
                          ),
                          Slider(
                            value: dashboard.operatingRadiusKm.clamp(1.0, 50.0),
                            min: 1,
                            max: 50,
                            divisions: 49,
                            label: '${dashboard.operatingRadiusKm.toInt()} km',
                            onChanged: dashboard.setOperatingRadius,
                            onChangeEnd: dashboard.onRadiusChangeEnd,
                          ),
                          if (dashboard.error != null)
                            Padding(
                              padding: const EdgeInsets.only(bottom: 4),
                              child: Text(
                                dashboard.error!,
                                style: const TextStyle(
                                  color: Colors.red,
                                  fontSize: 11,
                                ),
                              ),
                            ),
                        ],
                      ),
                    ),
                  ),
                ),

                // Map & Incoming Alerts Section
                Expanded(
                  child: Stack(
                    children: [
                      FlutterMap(
                        options: MapOptions(
                          initialCenter: centerLocation,
                          initialZoom: 13.0,
                        ),
                        children: [
                          TileLayer(
                            urlTemplate:
                                'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
                            userAgentPackageName: 'com.example.mobile',
                          ),
                          CircleLayer(
                            circles: [
                              CircleMarker(
                                point: centerLocation,
                                radius: dashboard.operatingRadiusKm * 1000,
                                useRadiusInMeter: true,
                                color: Colors.blue.withValues(alpha: 0.18),
                                borderColor: Colors.blueAccent,
                                borderStrokeWidth: 2,
                              ),
                            ],
                          ),
                          MarkerLayer(
                            markers: [
                              Marker(
                                point: centerLocation,
                                width: 40,
                                height: 40,
                                child: const Icon(
                                  Icons.location_on,
                                  color: Colors.red,
                                  size: 40,
                                ),
                              ),
                              if (dashboard.activeJobMatch != null &&
                                  dashboard
                                          .activeJobMatch!['customerLatitude'] !=
                                      null &&
                                  dashboard
                                          .activeJobMatch!['customerLongitude'] !=
                                      null)
                                Marker(
                                  point: latlong.LatLng(
                                    (dashboard.activeJobMatch!['customerLatitude']
                                            as num)
                                        .toDouble(),
                                    (dashboard.activeJobMatch!['customerLongitude']
                                            as num)
                                        .toDouble(),
                                  ),
                                  width: 40,
                                  height: 40,
                                  child: const Icon(
                                    Icons.location_on,
                                    color: Colors.green,
                                    size: 40,
                                  ),
                                ),
                            ],
                          ),
                          if (dashboard.routePoints.isNotEmpty)
                            PolylineLayer(
                              polylines: [
                                Polyline(
                                  points: dashboard.routePoints,
                                  strokeWidth: 4.0,
                                  color: Colors.blueAccent,
                                ),
                              ],
                            ),
                        ],
                      ),

                      // Dynamic Job Alert Card overlay
                      if (dashboard.isOnline &&
                          dashboard.activeJobMatch != null &&
                          !hasAcceptedActiveMatch)
                        Positioned(
                          bottom: AppSpacing.md,
                          left: AppSpacing.md,
                          right: AppSpacing.md,
                          child: JobAlertCard(
                            category:
                                dashboard.activeJobMatch!['category']
                                    ?.toString() ??
                                'Service Request',
                            distance:
                                dashboard.liveDistanceKm != null
                                    ? dashboard.liveDistanceKm!.toStringAsFixed(1)
                                    : '${dashboard.activeJobMatch!['distanceKm']}',
                            urgency:
                                dashboard.activeJobMatch!['urgency']
                                    ?.toString() ??
                                'Standard',
                            description: dashboard
                                .activeJobMatch!['description']
                                ?.toString(),
                            aiRationale: (dashboard
                                        .activeJobMatch!['detectedProblem'] ??
                                    dashboard
                                        .activeJobMatch!['aiRationale'] ??
                                    dashboard
                                        .activeJobMatch!['rationale'])
                                ?.toString(),
                            isOutOfRange: dashboard.liveDistanceKm != null &&
                                dashboard.liveDistanceKm! >
                                    dashboard.operatingRadiusKm,
                            onAccept: onAcceptMatch,
                            onDecline: onDeclineMatch,
                          ),
                        ),
                    ],
                  ),
                ),
              ],
            ),
    );
  }
}
