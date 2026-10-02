import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart' as latlong;

import '../auth/providers/auth_provider.dart';
import '../../shared/theme/app_spacing.dart';
import 'providers/provider_dashboard_provider.dart';
import 'services/provider_service.dart';
import 'widgets/job_alert_card.dart';
import 'widgets/provider_bottom_navigation.dart';
import 'screens/provider_dashboard_tab.dart';
import 'screens/provider_profile_tab.dart';
import '../quotations/screens/booking_management_tab.dart';

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
    try {
      await _dashboardProvider?.acceptJob();
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
        duration: Duration(seconds: 3),
        content: Row(
          children: [
            Icon(Icons.check_circle, color: Colors.white),
            SizedBox(width: 8),
            Expanded(
              child: Text(
                'Job Accepted! Route & customer location locked on map.',
                style: TextStyle(fontWeight: FontWeight.bold),
              ),
            ),
          ],
        ),
      ),
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

class _ProviderHomeView extends StatefulWidget {
  final bool hasAcceptedActiveMatch;
  final VoidCallback onAcceptMatch;
  final VoidCallback onDeclineMatch;

  const _ProviderHomeView({
    required this.hasAcceptedActiveMatch,
    required this.onAcceptMatch,
    required this.onDeclineMatch,
  });

  @override
  State<_ProviderHomeView> createState() => _ProviderHomeViewState();
}

class _ProviderHomeViewState extends State<_ProviderHomeView> {
  int _currentIndex = 0;
  final MapController _mapController = MapController();
  String? _lastFittedJobId;
  bool _isOngoingCardExpanded = false;

  void _fitMapToBounds(latlong.LatLng providerLoc, latlong.LatLng customerLoc) {
    try {
      final bounds = LatLngBounds.fromPoints([providerLoc, customerLoc]);
      _mapController.fitCamera(
        CameraFit.bounds(
          bounds: bounds,
          padding: const EdgeInsets.only(
            top: 70,
            left: 50,
            right: 50,
            bottom: 240,
          ),
        ),
      );
    } catch (e) {
      debugPrint('Map auto-fit error: $e');
    }
  }

  void _centerOnLocation(latlong.LatLng loc) {
    try {
      _mapController.move(loc, 14.0);
    } catch (e) {
      debugPrint('Center map error: $e');
    }
  }

  @override
  void dispose() {
    _mapController.dispose();
    super.dispose();
  }

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

  void _confirmLogout(BuildContext context, ProviderDashboardProvider dashboard, AuthProvider auth) {
    showDialog(
      context: context,
      builder: (dialogCtx) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        title: const Row(
          children: [
            Icon(Icons.logout, color: Colors.redAccent),
            SizedBox(width: 8),
            Text('Log Out?'),
          ],
        ),
        content: const Text('Do you really want to log out of your account?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogCtx),
            child: const Text('No'),
          ),
          ElevatedButton(
            style: ElevatedButton.styleFrom(
              backgroundColor: Colors.redAccent,
              foregroundColor: Colors.white,
            ),
            onPressed: () async {
              Navigator.pop(dialogCtx);
              await dashboard.onLogout();
              if (context.mounted) {
                await auth.logout();
              }
            },
            child: const Text('Yes, Log Out'),
          ),
        ],
      ),
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

    // Objective 1 & 2 - Verified Technician Active Workspace with 4-Tab Navigation
    return Scaffold(
      body: IndexedStack(
        index: _currentIndex,
        children: [
          ProviderDashboardTab(
            onAcceptMatch: widget.onAcceptMatch,
            onDeclineMatch: widget.onDeclineMatch,
            onSwitchToMap: () {
              setState(() {
                _currentIndex = 1;
              });
            },
          ),
          _buildVerifiedWorkspaceScaffold(context, dashboard, auth),
          const BookingManagementTab(),
          const ProviderProfileTab(),
        ],
      ),
      bottomNavigationBar: _currentIndex == 1
          ? null
          : ProviderBottomNavigation(
              selectedIndex: _currentIndex,
              onDestinationSelected: (index) {
                setState(() {
                  _currentIndex = index;
                });
              },
            ),
    );
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
            onPressed: () => _confirmLogout(context, dashboard, auth),
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
                    if (dashboard.categories.length <= 1)
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
                      )
                    else ...[
                      const Text(
                        'Registered Service Categories:',
                        style: TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.bold,
                          color: Colors.black87,
                        ),
                      ),
                      const SizedBox(height: 8),
                      Wrap(
                        spacing: 8,
                        runSpacing: 6,
                        children: dashboard.categories.map((cat) {
                          final cColor = _getCategoryColor(cat);
                          return Container(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 10,
                              vertical: 5,
                            ),
                            decoration: BoxDecoration(
                              color: cColor.withValues(alpha: 0.1),
                              borderRadius: BorderRadius.circular(6),
                              border: Border.all(
                                color: cColor.withValues(alpha: 0.35),
                              ),
                            ),
                            child: Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                Icon(_getCategoryIcon(cat),
                                    size: 15, color: cColor),
                                const SizedBox(width: 6),
                                Text(
                                  cat,
                                  style: TextStyle(
                                    fontSize: 12,
                                    fontWeight: FontWeight.w600,
                                    color: cColor,
                                  ),
                                ),
                              ],
                            ),
                          );
                        }).toList(),
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
                  onPressed: () async {
                    await dashboard.onLogout();
                    if (context.mounted) {
                      await auth.logout();
                    }
                  },
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
            onPressed: () async {
              await dashboard.onLogout();
              if (context.mounted) {
                await auth.logout();
              }
            },
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
                  onPressed: () => _confirmLogout(context, dashboard, auth),
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

    final num? cLat = dashboard.activeJobMatch?['customerLatitude'] as num?;
    final num? cLng = dashboard.activeJobMatch?['customerLongitude'] as num?;
    final customerLocation = (cLat != null && cLng != null)
        ? latlong.LatLng(cLat.toDouble(), cLng.toDouble())
        : null;

    final jobId = dashboard.activeJobMatch?['jobId']?.toString();
    if (jobId != null && customerLocation != null && _lastFittedJobId != jobId) {
      _lastFittedJobId = jobId;
      WidgetsBinding.instance.addPostFrameCallback((_) {
        _fitMapToBounds(centerLocation, customerLocation);
      });
    } else if (jobId == null && _lastFittedJobId != null) {
      _lastFittedJobId = null;
    }

    return dashboard.isLoading
        ? const Center(child: CircularProgressIndicator())
        : Stack(
              fit: StackFit.expand,
              children: [
                // 1. Edge-to-Edge Full Canvas Map
                FlutterMap(
                  mapController: _mapController,
                  options: MapOptions(
                    initialCenter: centerLocation,
                    initialZoom: 13.0,
                    minZoom: 6.5,
                    maxZoom: 18.0,
                  ),
                  children: [
                    TileLayer(
                      urlTemplate:
                          'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
                      userAgentPackageName: 'com.example.mobile',
                    ),
                    // Semi-transparent dynamic coverage radius
                    CircleLayer(
                      circles: [
                        CircleMarker(
                          point: centerLocation,
                          radius: dashboard.operatingRadiusKm * 1000,
                          useRadiusInMeter: true,
                          color: (dashboard.isOnline
                                  ? Colors.blueAccent
                                  : Colors.grey)
                              .withValues(alpha: 0.12),
                          borderColor: (dashboard.isOnline
                                  ? Colors.blueAccent
                                  : Colors.grey)
                              .withValues(alpha: 0.6),
                          borderStrokeWidth: 2,
                        ),
                      ],
                    ),
                    // Route Polyline to customer if available
                    if (dashboard.routePoints.isNotEmpty)
                      PolylineLayer(
                        polylines: [
                          Polyline(
                            points: dashboard.routePoints,
                            strokeWidth: 4.5,
                            color: Colors.blueAccent,
                          ),
                        ],
                      ),
                    // Location Markers (Technician & Customer)
                    MarkerLayer(
                      markers: [
                        // Provider location marker
                        Marker(
                          point: centerLocation,
                          width: 46,
                          height: 46,
                          child: Stack(
                            alignment: Alignment.center,
                            children: [
                              Container(
                                width: 38,
                                height: 38,
                                decoration: BoxDecoration(
                                  color: dashboard.isOnline
                                      ? _getCategoryColor(dashboard.category)
                                      : Colors.grey.shade700,
                                  shape: BoxShape.circle,
                                  boxShadow: const [
                                    BoxShadow(
                                      color: Colors.black26,
                                      blurRadius: 6,
                                      offset: Offset(0, 3),
                                    ),
                                  ],
                                  border: Border.all(
                                    color: Colors.white,
                                    width: 2.5,
                                  ),
                                ),
                                child: Icon(
                                  _getCategoryIcon(dashboard.category),
                                  color: Colors.white,
                                  size: 19,
                                ),
                              ),
                              if (dashboard.isOnline)
                                Positioned(
                                  top: 2,
                                  right: 2,
                                  child: Container(
                                    width: 11,
                                    height: 11,
                                    decoration: BoxDecoration(
                                      color: Colors.greenAccent.shade400,
                                      shape: BoxShape.circle,
                                      border: Border.all(
                                        color: Colors.white,
                                        width: 1.5,
                                      ),
                                    ),
                                  ),
                                ),
                            ],
                          ),
                        ),
                        // Customer location marker
                        if (customerLocation != null)
                          Marker(
                            point: customerLocation,
                            width: 46,
                            height: 46,
                            child: Container(
                              decoration: BoxDecoration(
                                color: Colors.green.shade600,
                                shape: BoxShape.circle,
                                boxShadow: const [
                                  BoxShadow(
                                    color: Colors.black26,
                                    blurRadius: 6,
                                    offset: Offset(0, 3),
                                  ),
                                ],
                                border: Border.all(
                                  color: Colors.white,
                                  width: 2.5,
                                ),
                              ),
                              child: const Icon(
                                Icons.person_pin,
                                color: Colors.white,
                                size: 24,
                              ),
                            ),
                          ),
                      ],
                    ),
                  ],
                ),

                // Top-Left Floating Back Button (Return to Dashboard)
                Positioned(
                  top: MediaQuery.of(context).padding.top + 12,
                  left: 16,
                  child: Material(
                    color: Colors.transparent,
                    child: InkWell(
                      onTap: () {
                        setState(() {
                          _currentIndex = 0;
                        });
                      },
                      borderRadius: BorderRadius.circular(24),
                      child: Container(
                        padding: const EdgeInsets.all(10),
                        decoration: BoxDecoration(
                          color: Colors.white,
                          shape: BoxShape.circle,
                          boxShadow: const [
                            BoxShadow(
                              color: Colors.black26,
                              blurRadius: 6,
                              offset: Offset(0, 2),
                            ),
                          ],
                          border: Border.all(
                            color: Colors.black12,
                            width: 1,
                          ),
                        ),
                        child: const Icon(
                          Icons.arrow_back_rounded,
                          color: Color(0xFF173B5E),
                          size: 22,
                        ),
                      ),
                    ),
                  ),
                ),

                // 2. Floating Map Camera Action Buttons
                Positioned(
                  top: 92,
                  right: 16,
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      if (customerLocation != null) ...[
                        FloatingActionButton.small(
                          heroTag: 'fit_both_btn',
                          tooltip: 'Fit Both on Map',
                          backgroundColor: Colors.white,
                          foregroundColor: Colors.blueAccent,
                          elevation: 3,
                          onPressed: () => _fitMapToBounds(
                            centerLocation,
                            customerLocation,
                          ),
                          child: const Icon(Icons.zoom_out_map),
                        ),
                        const SizedBox(height: 8),
                        FloatingActionButton.small(
                          heroTag: 'customer_loc_btn',
                          tooltip: 'Customer Location',
                          backgroundColor: Colors.white,
                          foregroundColor: Colors.green,
                          elevation: 3,
                          onPressed: () =>
                              _centerOnLocation(customerLocation),
                          child: const Icon(Icons.person_pin_circle),
                        ),
                        const SizedBox(height: 8),
                      ],
                      FloatingActionButton.small(
                        heroTag: 'my_loc_btn',
                        tooltip: 'My Location',
                        backgroundColor: Colors.white,
                        foregroundColor: Colors.redAccent,
                        elevation: 3,
                        onPressed: () =>
                            _centerOnLocation(centerLocation),
                        child: const Icon(Icons.my_location),
                      ),
                    ],
                  ),
                ),

                // 4. Bottom Floating Operations Cockpit or Incoming Job Alert
                Positioned(
                  bottom: 16,
                  left: 16,
                  right: 16,
                  child: Center(
                    child: ConstrainedBox(
                      constraints: const BoxConstraints(maxWidth: 580),
                      child: (dashboard.isOnline &&
                              dashboard.activeJobMatch != null &&
                              !widget.hasAcceptedActiveMatch &&
                              dashboard.activeJobMatch!['status'] != 'Accepted')
                          // Active Incoming Job Alert Card
                          ? JobAlertCard(
                              category: dashboard
                                      .activeJobMatch!['category']
                                      ?.toString() ??
                                  'Service Request',
                              distance: dashboard.liveDistanceKm != null
                                  ? '${dashboard.liveDistanceKm!.toStringAsFixed(1)} km'
                                  : (dashboard.activeJobMatch!['distanceKm'] !=
                                          null
                                      ? '${dashboard.activeJobMatch!['distanceKm']} km'
                                      : 'Nearby'),
                              urgency: dashboard
                                      .activeJobMatch!['urgency']
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
                              remainingSeconds:
                                  dashboard.remainingAcceptSeconds,
                              totalTimeoutSeconds:
                                  dashboard.totalTimeoutSeconds,
                              onAccept: widget.onAcceptMatch,
                              onDecline: widget.onDeclineMatch,
                            )
                          : (dashboard.isOnline &&
                                  dashboard.activeJobMatch != null &&
                                  (widget.hasAcceptedActiveMatch ||
                                      dashboard.activeJobMatch!['status'] ==
                                          'Accepted'))
                              // Accepted Active In-Progress Job Cockpit (Option B: Expandable Full Details)!
                              ? Card(
                                  elevation: 8,
                                  shadowColor: Colors.black26,
                                  shape: RoundedRectangleBorder(
                                    borderRadius: BorderRadius.circular(18),
                                  ),
                                  color: Colors.white,
                                  child: Container(
                                    constraints: BoxConstraints(
                                      maxHeight: _isOngoingCardExpanded
                                          ? MediaQuery.of(context).size.height * 0.46
                                          : 230,
                                    ),
                                    child: SingleChildScrollView(
                                      padding: const EdgeInsets.symmetric(
                                        horizontal: 16,
                                        vertical: 14,
                                      ),
                                      child: Column(
                                        mainAxisSize: MainAxisSize.min,
                                        crossAxisAlignment:
                                            CrossAxisAlignment.start,
                                        children: [
                                          Row(
                                            mainAxisAlignment:
                                                MainAxisAlignment.spaceBetween,
                                            children: [
                                              Row(
                                                children: [
                                                  Container(
                                                    width: 10,
                                                    height: 10,
                                                    decoration:
                                                        const BoxDecoration(
                                                      color: Colors.green,
                                                      shape: BoxShape.circle,
                                                    ),
                                                  ),
                                                  const SizedBox(width: 8),
                                                  const Text(
                                                    'EN ROUTE TO CUSTOMER',
                                                    style: TextStyle(
                                                      fontWeight: FontWeight.w900,
                                                      fontSize: 13,
                                                      color: Colors.green,
                                                      letterSpacing: 0.5,
                                                    ),
                                                  ),
                                                ],
                                              ),
                                              Container(
                                                padding:
                                                    const EdgeInsets.symmetric(
                                                  horizontal: 8,
                                                  vertical: 3,
                                                ),
                                                decoration: BoxDecoration(
                                                  color: Colors.blue.shade50,
                                                  borderRadius:
                                                      BorderRadius.circular(8),
                                                ),
                                                child: Text(
                                                  dashboard.activeJobMatch![
                                                              'category']
                                                          ?.toString() ??
                                                      'Service',
                                                  style: TextStyle(
                                                    color: Colors.blue.shade800,
                                                    fontWeight: FontWeight.bold,
                                                    fontSize: 12,
                                                  ),
                                                ),
                                              ),
                                            ],
                                          ),
                                          const SizedBox(height: 10),
                                          Text(
                                            dashboard.activeJobMatch![
                                                    'detectedProblem'] ??
                                                dashboard.activeJobMatch![
                                                    'description'] ??
                                                'Customer Assistance Request',
                                            style: const TextStyle(
                                              fontSize: 15,
                                              fontWeight: FontWeight.bold,
                                              color: Colors.black87,
                                              height: 1.3,
                                            ),
                                            maxLines: _isOngoingCardExpanded ? null : 2,
                                            overflow: _isOngoingCardExpanded
                                                ? TextOverflow.visible
                                                : TextOverflow.ellipsis,
                                          ),
                                          if (_isOngoingCardExpanded &&
                                              dashboard.activeJobMatch!['description'] != null &&
                                              dashboard.activeJobMatch!['detectedProblem'] != null &&
                                              dashboard.activeJobMatch!['description'] != dashboard.activeJobMatch!['detectedProblem']) ...[
                                            const SizedBox(height: 6),
                                            Text(
                                              'Customer Notes: "${dashboard.activeJobMatch!['description']}"',
                                              style: TextStyle(
                                                fontSize: 12,
                                                fontStyle: FontStyle.italic,
                                                color: Colors.grey.shade800,
                                                height: 1.25,
                                              ),
                                            ),
                                          ],
                                          if (dashboard.activeJobMatch![
                                                  'locationText'] !=
                                              null) ...[
                                            const SizedBox(height: 6),
                                            Row(
                                              crossAxisAlignment: CrossAxisAlignment.start,
                                              children: [
                                                Icon(Icons.location_on,
                                                    size: 14,
                                                    color: Colors.grey.shade600),
                                                const SizedBox(width: 4),
                                                Expanded(
                                                  child: Text(
                                                    dashboard.activeJobMatch![
                                                            'locationText']
                                                        .toString(),
                                                    style: TextStyle(
                                                      fontSize: 12,
                                                      color: Colors.grey.shade700,
                                                      height: 1.25,
                                                    ),
                                                    maxLines: _isOngoingCardExpanded ? null : 1,
                                                    overflow: _isOngoingCardExpanded
                                                        ? TextOverflow.visible
                                                        : TextOverflow.ellipsis,
                                                  ),
                                                ),
                                              ],
                                            ),
                                          ],
                                          const SizedBox(height: 8),
                                          Row(
                                            children: [
                                              Icon(Icons.directions_car,
                                                  size: 16,
                                                  color: Colors.blue.shade700),
                                              const SizedBox(width: 6),
                                              Text(
                                                dashboard.liveDistanceKm != null
                                                    ? '${dashboard.liveDistanceKm!.toStringAsFixed(1)} km away'
                                                    : '${dashboard.activeJobMatch!['distanceKm'] ?? 'Nearby'} km',
                                                style: TextStyle(
                                                  fontWeight: FontWeight.w600,
                                                  fontSize: 13,
                                                  color: Colors.blue.shade900,
                                                ),
                                              ),
                                              const Spacer(),
                                              Text(
                                                'Urgency: ${dashboard.activeJobMatch!['urgency'] ?? 'High'}',
                                                style: TextStyle(
                                                  fontWeight: FontWeight.bold,
                                                  fontSize: 12,
                                                   color: Colors.orange.shade800,
                                                ),
                                              ),
                                            ],
                                          ),
                                          const SizedBox(height: 6),
                                          InkWell(
                                            onTap: () {
                                              setState(() {
                                                _isOngoingCardExpanded = !_isOngoingCardExpanded;
                                              });
                                            },
                                            borderRadius: BorderRadius.circular(6),
                                            child: Padding(
                                              padding: const EdgeInsets.symmetric(vertical: 4),
                                              child: Row(
                                                mainAxisAlignment: MainAxisAlignment.center,
                                                children: [
                                                  Text(
                                                    _isOngoingCardExpanded
                                                        ? 'Show Less'
                                                        : 'Show Full Message & Details',
                                                    style: TextStyle(
                                                      fontSize: 12,
                                                      fontWeight: FontWeight.bold,
                                                      color: Colors.blue.shade700,
                                                    ),
                                                  ),
                                                  Icon(
                                                    _isOngoingCardExpanded
                                                        ? Icons.keyboard_arrow_up
                                                        : Icons.keyboard_arrow_down,
                                                    size: 16,
                                                    color: Colors.blue.shade700,
                                                  ),
                                                ],
                                              ),
                                            ),
                                          ),
                                        ],
                                      ),
                                    ),
                                  ),
                                )
                          : const SizedBox.shrink(),
                    ),
                  ),
                ),
              ],
            );
  }
}
