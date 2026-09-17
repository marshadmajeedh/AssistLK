import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';

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

  @override
  Widget build(BuildContext context) {
    if (_dashboardProvider == null) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }

    return ChangeNotifierProvider.value(
      value: _dashboardProvider!,
      child: const _ProviderHomeView(),
    );
  }
}

class _ProviderHomeView extends StatelessWidget {
  const _ProviderHomeView();

  @override
  Widget build(BuildContext context) {
    final dashboard = context.watch<ProviderDashboardProvider>();
    final auth = context.read<AuthProvider>();

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
                                        ? '🟢 ONLINE (On Duty - Receiving Jobs)'
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
                      GoogleMap(
                        initialCameraPosition: CameraPosition(
                          target: LatLng(
                            dashboard.latitude,
                            dashboard.longitude,
                          ),
                          zoom: 12,
                        ),
                        myLocationEnabled: dashboard.error == null,
                        myLocationButtonEnabled: dashboard.error == null,
                        circles: {
                          Circle(
                            circleId: const CircleId('radius_overlay'),
                            center: LatLng(
                              dashboard.latitude,
                              dashboard.longitude,
                            ),
                            radius: dashboard.operatingRadiusKm * 1000,
                            fillColor: Colors.blue.withOpacity(0.18),
                            strokeColor: Colors.blueAccent,
                            strokeWidth: 2,
                          ),
                        },
                      ),

                      // Dispatched Job Alert Card overlay (active when Online)
                      if (dashboard.isOnline)
                        const Positioned(
                          bottom: AppSpacing.md,
                          left: AppSpacing.md,
                          right: AppSpacing.md,
                          child: JobAlertCard(
                            category: 'Plumbing Repair',
                            distance: '3.2 km',
                            urgency: 'High',
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
