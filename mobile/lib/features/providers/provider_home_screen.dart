import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart' as latlong;

import '../auth/providers/auth_provider.dart';
import '../../shared/theme/app_spacing.dart';
import '../../shared/theme/app_text_styles.dart';
import 'providers/provider_dashboard_provider.dart';
import 'services/provider_service.dart';
import 'widgets/job_alert_card.dart';

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

  Future<void> _onAcceptMatch(BuildContext context) async {
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
                    setState(() {
                      _hasAcceptedActiveMatch = true;
                    });
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
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }

    return ChangeNotifierProvider.value(
      value: _dashboardProvider!,
      child: _ProviderHomeView(
        hasAcceptedActiveMatch: _hasAcceptedActiveMatch,
        onAcceptMatch: () => _onAcceptMatch(context),
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

  @override
  Widget build(BuildContext context) {
    final dashboard = context.watch<ProviderDashboardProvider>();
    final auth = context.read<AuthProvider>();

    final centerLocation = latlong.LatLng(
      dashboard.latitude,
      dashboard.longitude,
    );

    return Scaffold(
      appBar: AppBar(
        title: const Text('Provider Workspace'),
        actions: [
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
                // Controls section: Duty Status and Radius
                Padding(
                  padding: const EdgeInsets.all(AppSpacing.md),
                  child: Card(
                    elevation: 3,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: Padding(
                      padding: const EdgeInsets.all(AppSpacing.md),
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
                                    style: AppTextStyles.sectionHeading,
                                  ),
                                  const SizedBox(height: 4),
                                  Text(
                                    dashboard.isOnline
                                        ? '🟢 ONLINE (Receiving Jobs)'
                                        : '⚫ OFFLINE (Off Duty)',
                                    style: TextStyle(
                                      color: dashboard.isOnline
                                          ? Colors.green.shade700
                                          : Colors.grey.shade600,
                                      fontWeight: FontWeight.bold,
                                      fontSize: 14,
                                    ),
                                  ),
                                ],
                              ),
                              Switch(
                                value: dashboard.isOnline,
                                activeColor: Colors.green,
                                onChanged: (value) =>
                                    dashboard.toggleOnlineStatus(value),
                              ),
                            ],
                          ),
                          const Divider(height: 24),
                          Text(
                            'Operating Radius: ${dashboard.operatingRadiusKm.toInt()} km',
                            style: AppTextStyles.body.copyWith(
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                          Slider(
                            value: dashboard.operatingRadiusKm,
                            min: 5,
                            max: 50,
                            divisions: 9,
                            label: '${dashboard.operatingRadiusKm.toInt()} km',
                            onChanged: dashboard.setOperatingRadius,
                            onChangeEnd: dashboard.onRadiusChangeEnd,
                          ),
                          if (dashboard.error != null)
                            Padding(
                              padding: const EdgeInsets.only(
                                top: AppSpacing.xs,
                              ),
                              child: Text(
                                dashboard.error!,
                                style: const TextStyle(
                                  color: Colors.red,
                                  fontSize: 12,
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
                            urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
                            userAgentPackageName: 'com.example.mobile',
                          ),
                          CircleLayer(
                            circles: [
                              CircleMarker(
                                point: centerLocation,
                                radius: dashboard.operatingRadiusKm * 1000,
                                useRadiusInMeter: true,
                                color: Colors.blue.withOpacity(0.18),
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
                                  dashboard.activeJobMatch!['customerLatitude'] != null &&
                                  dashboard.activeJobMatch!['customerLongitude'] != null)
                                Marker(
                                  point: latlong.LatLng(
                                    (dashboard.activeJobMatch!['customerLatitude'] as num).toDouble(),
                                    (dashboard.activeJobMatch!['customerLongitude'] as num).toDouble(),
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
                            // Populates live data straight from the backend dictionary
                            category:
                                dashboard.activeJobMatch!['category']
                                    ?.toString() ??
                                'Service Request',
                            distance:
                                '${dashboard.activeJobMatch!['distanceKm']} km',
                            urgency:
                                dashboard.activeJobMatch!['urgency']
                                    ?.toString() ??
                                'Standard',
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
