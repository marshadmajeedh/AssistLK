import 'dart:io';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../providers/provider_dashboard_provider.dart';
import '../widgets/notification_center_modal.dart';
import '../widgets/job_alert_card.dart';

class ProviderDashboardTab extends StatelessWidget {
  final VoidCallback? onAcceptMatch;
  final VoidCallback? onDeclineMatch;
  final VoidCallback? onSwitchToMap;

  const ProviderDashboardTab({
    super.key,
    this.onAcceptMatch,
    this.onDeclineMatch,
    this.onSwitchToMap,
  });

  @override
  Widget build(BuildContext context) {
    final dashboard = context.watch<ProviderDashboardProvider>();

    // Customer Blue Theme Colors
    const primaryColor = Color(0xFF1F4E78);
    const primaryDark = Color(0xFF173B5E);

    final int completedJobs = (dashboard.profile?['completedJobs'] as num?)?.toInt() ??
        (dashboard.profile?['totalCompletedJobs'] as num?)?.toInt() ??
        0;
    final int ongoingJobs = (dashboard.activeJobMatch != null &&
            dashboard.activeJobMatch!['status'] == 'Accepted')
        ? 1
        : 0;
    final int totalJobs = completedJobs + ongoingJobs;

    return Scaffold(
      backgroundColor: const Color(0xFFF8FAFC),
      body: SingleChildScrollView(
        child: Column(
          children: [
            // 1. TOP BRAND HEADER (Gradient)
            Container(
              width: double.infinity,
              decoration: const BoxDecoration(
                gradient: LinearGradient(
                  colors: [primaryColor, primaryDark],
                  begin: Alignment.topLeft,
                  end: Alignment.bottomRight,
                ),
                borderRadius: BorderRadius.vertical(bottom: Radius.circular(28)),
                boxShadow: [
                  BoxShadow(
                    color: Colors.black12,
                    blurRadius: 10,
                    offset: Offset(0, 4),
                  ),
                ],
              ),
              child: SafeArea(
                bottom: false,
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
                  child: Column(
                    children: [
                      // Logo & Notification
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Row(
                            children: [
                              Container(
                                width: 28,
                                height: 28,
                                decoration: BoxDecoration(
                                  color: Colors.white.withValues(alpha: 0.2),
                                  borderRadius: BorderRadius.circular(8),
                                ),
                                child: const Center(
                                  child: Text(
                                    'LK',
                                    style: TextStyle(
                                      color: Colors.white,
                                      fontWeight: FontWeight.w900,
                                      fontSize: 12,
                                    ),
                                  ),
                                ),
                              ),
                              const SizedBox(width: 8),
                              const Text(
                                'AssistLK Provider',
                                style: TextStyle(
                                  color: Colors.white,
                                  fontWeight: FontWeight.bold,
                                  fontSize: 14,
                                  letterSpacing: 0.5,
                                ),
                              ),
                            ],
                          ),
                          GestureDetector(
                            onTap: () => NotificationCenterModal.show(context, dashboard),
                            child: Stack(
                              clipBehavior: Clip.none,
                              children: [
                                Container(
                                  width: 36,
                                  height: 36,
                                  decoration: BoxDecoration(
                                    color: Colors.white.withValues(alpha: 0.15),
                                    shape: BoxShape.circle,
                                  ),
                                  child: const Icon(
                                    Icons.notifications_none,
                                    color: Colors.white,
                                    size: 20,
                                  ),
                                ),
                                if (dashboard.unreadNotificationCount > 0)
                                  Positioned(
                                    top: -2,
                                    right: -2,
                                    child: Container(
                                      padding: const EdgeInsets.all(4),
                                      decoration: const BoxDecoration(
                                        color: Colors.redAccent,
                                        shape: BoxShape.circle,
                                      ),
                                      constraints: const BoxConstraints(
                                        minWidth: 18,
                                        minHeight: 18,
                                      ),
                                      child: Text(
                                        dashboard.unreadNotificationCount > 9
                                            ? '9+'
                                            : dashboard.unreadNotificationCount.toString(),
                                        style: const TextStyle(
                                          color: Colors.white,
                                          fontSize: 10,
                                          fontWeight: FontWeight.bold,
                                        ),
                                        textAlign: TextAlign.center,
                                      ),
                                    ),
                                  ),
                              ],
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 20),

                      // Profile Info & Online Pill
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Expanded(
                            child: Row(
                              children: [
                                GestureDetector(
                                  onTap: () {
                                    if (dashboard.profileImagePath != null &&
                                        File(dashboard.profileImagePath!).existsSync()) {
                                      _showFullPhotoDialog(context, dashboard.profileImagePath!, null);
                                    } else {
                                      _showFullPhotoDialog(context, null, dashboard.defaultAvatarUrl);
                                    }
                                  },
                                  child: Container(
                                    padding: const EdgeInsets.all(3),
                                    decoration: BoxDecoration(
                                      color: Colors.white.withValues(alpha: 0.35),
                                      shape: BoxShape.circle,
                                    ),
                                    child: CircleAvatar(
                                      radius: 34,
                                      backgroundColor: Colors.white,
                                      backgroundImage: dashboard.profileImagePath != null &&
                                              File(dashboard.profileImagePath!).existsSync()
                                          ? FileImage(File(dashboard.profileImagePath!))
                                          : NetworkImage(dashboard.defaultAvatarUrl) as ImageProvider,
                                    ),
                                  ),
                                ),
                                const SizedBox(width: 14),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    mainAxisSize: MainAxisSize.min,
                                    children: [
                                      Text(
                                        dashboard.fullName.isNotEmpty ? dashboard.fullName : 'Technician',
                                        style: const TextStyle(
                                          color: Colors.white,
                                          fontWeight: FontWeight.bold,
                                          fontSize: 18,
                                        ),
                                        overflow: TextOverflow.ellipsis,
                                      ),
                                      const SizedBox(height: 3),
                                      Row(
                                        children: [
                                          Flexible(
                                            child: Text(
                                              dashboard.category,
                                              style: TextStyle(
                                                color: Colors.blue.shade100,
                                                fontSize: 12,
                                                fontWeight: FontWeight.w500,
                                              ),
                                              overflow: TextOverflow.ellipsis,
                                            ),
                                          ),
                                          const SizedBox(width: 6),
                                          const Text('•', style: TextStyle(color: Colors.white54, fontSize: 10)),
                                          const SizedBox(width: 6),
                                          const Icon(Icons.star, color: Colors.amber, size: 12),
                                          const SizedBox(width: 3),
                                          Text(
                                            dashboard.rating > 0
                                                ? dashboard.rating.toStringAsFixed(1)
                                                : '0.0',
                                            style: const TextStyle(
                                              color: Colors.amber,
                                              fontSize: 12,
                                              fontWeight: FontWeight.bold,
                                            ),
                                          ),
                                        ],
                                      ),
                                    ],
                                  ),
                                ),
                              ],
                            ),
                          ),
                          const SizedBox(width: 10),

                          // Custom Online Pill with Confirmation
                          GestureDetector(
                            onTap: () => _confirmToggleOnline(context, dashboard),
                            child: AnimatedContainer(
                              duration: const Duration(milliseconds: 300),
                              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                              decoration: BoxDecoration(
                                color: dashboard.isOnline ? Colors.white : Colors.white.withValues(alpha: 0.2),
                                borderRadius: BorderRadius.circular(20),
                                boxShadow: dashboard.isOnline
                                    ? [
                                        BoxShadow(
                                          color: Colors.black.withValues(alpha: 0.1),
                                          blurRadius: 8,
                                          offset: const Offset(0, 2),
                                        ),
                                      ]
                                    : [],
                              ),
                              child: Row(
                                mainAxisSize: MainAxisSize.min,
                                children: [
                                  Container(
                                    width: 8,
                                    height: 8,
                                    decoration: BoxDecoration(
                                      color: dashboard.isOnline ? Colors.green.shade500 : Colors.red.shade400,
                                      shape: BoxShape.circle,
                                    ),
                                  ),
                                  const SizedBox(width: 6),
                                  Text(
                                    dashboard.isOnline ? 'Online' : 'Offline',
                                    style: TextStyle(
                                      color: dashboard.isOnline ? primaryColor : Colors.white,
                                      fontWeight: FontWeight.w900,
                                      fontSize: 12,
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
              ),
            ),

            // 2. 3-METRIC SUMMARY GRID
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 0),
              child: Row(
                children: [
                  Expanded(
                    child: _buildCompactStat(
                      'Total Jobs',
                      totalJobs.toString(),
                      Colors.blueGrey.shade800,
                    ),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: _buildCompactStat(
                      'Completed',
                      completedJobs.toString(),
                      primaryColor,
                    ),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: _buildCompactStat('Ongoing', ongoingJobs.toString(), Colors.amber.shade600),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 16),

            // 3. OPERATING RADIUS CONTROLLER
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: Container(
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(24),
                  border: Border.all(color: const Color(0xFFF1F5F9)),
                  boxShadow: [
                    BoxShadow(
                      color: Colors.black.withValues(alpha: 0.02),
                      blurRadius: 15,
                      offset: const Offset(0, 4),
                    ),
                  ],
                ),
                child: Column(
                  children: [
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Row(
                          children: [
                            Container(
                              padding: const EdgeInsets.all(8),
                              decoration: BoxDecoration(
                                color: primaryColor.withValues(alpha: 0.08),
                                borderRadius: BorderRadius.circular(12),
                              ),
                              child: const Icon(Icons.radar, color: primaryColor, size: 20),
                            ),
                            const SizedBox(width: 12),
                            Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                const Text(
                                  'Operating Radius',
                                  style: TextStyle(
                                    fontSize: 13,
                                    fontWeight: FontWeight.bold,
                                    color: Color(0xFF1E293B),
                                  ),
                                ),
                                const SizedBox(height: 2),
                                Text(
                                  'Currently accepting within ${dashboard.operatingRadiusKm.toInt()} km',
                                  style: const TextStyle(
                                    fontSize: 10,
                                    color: Color(0xFF64748B),
                                  ),
                                ),
                              ],
                            ),
                          ],
                        ),
                      ],
                    ),
                    // Quick Radius Presets (5 km, 10 km, 20 km, 30 km)
                    const SizedBox(height: 14),
                    Row(
                      children: [5, 10, 20, 30].map((preset) {
                        final isSelected = dashboard.operatingRadiusKm.toInt() == preset;
                        return Expanded(
                          child: Padding(
                            padding: const EdgeInsets.symmetric(horizontal: 3),
                            child: InkWell(
                              borderRadius: BorderRadius.circular(10),
                              onTap: () {
                                dashboard.setOperatingRadius(preset.toDouble());
                                dashboard.onRadiusChangeEnd(preset.toDouble());
                              },
                              child: AnimatedContainer(
                                duration: const Duration(milliseconds: 200),
                                padding: const EdgeInsets.symmetric(vertical: 8),
                                decoration: BoxDecoration(
                                  color: isSelected ? primaryColor : primaryColor.withValues(alpha: 0.06),
                                  borderRadius: BorderRadius.circular(10),
                                  border: Border.all(
                                    color: isSelected ? primaryColor : primaryColor.withValues(alpha: 0.15),
                                    width: isSelected ? 1.5 : 1.0,
                                  ),
                                ),
                                alignment: Alignment.center,
                                child: Text(
                                  '$preset km',
                                  style: TextStyle(
                                    fontSize: 12,
                                    fontWeight: isSelected ? FontWeight.bold : FontWeight.w600,
                                    color: isSelected ? Colors.white : primaryColor,
                                  ),
                                ),
                              ),
                            ),
                          ),
                        );
                      }).toList(),
                    ),
                    const SizedBox(height: 10),
                    SliderTheme(
                      data: SliderTheme.of(context).copyWith(
                        activeTrackColor: primaryColor,
                        inactiveTrackColor: primaryColor.withValues(alpha: 0.15),
                        thumbColor: primaryColor,
                        overlayColor: primaryColor.withValues(alpha: 0.1),
                        trackHeight: 6,
                        thumbShape: const RoundSliderThumbShape(enabledThumbRadius: 8),
                      ),
                      child: Slider(
                        value: dashboard.operatingRadiusKm.clamp(1.0, 50.0),
                        min: 1,
                        max: 50,
                        divisions: 49,
                        onChanged: dashboard.setOperatingRadius,
                        onChangeEnd: dashboard.onRadiusChangeEnd,
                      ),
                    ),
                    Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 8),
                      child: Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Text('1 km', style: TextStyle(color: Colors.grey.shade400, fontSize: 10, fontWeight: FontWeight.bold)),
                          Text('50 km', style: TextStyle(color: Colors.grey.shade400, fontSize: 10, fontWeight: FontWeight.bold)),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),

            // 4. ACTIVE DISPATCH ALERT / ONGOING JOB / RADAR STATUS
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
              child: _buildDashboardDispatchSection(context, dashboard, primaryColor),
            ),

            const SizedBox(height: 20),
          ],
        ),
      ),
    );
  }

  Widget _buildDashboardDispatchSection(
    BuildContext context,
    ProviderDashboardProvider dashboard,
    Color primaryColor,
  ) {
    // State 1: Incoming Alert awaiting Accept or Decline
    if (dashboard.isOnline &&
        dashboard.activeJobMatch != null &&
        dashboard.activeJobMatch!['status'] != 'Accepted') {
      return JobAlertCard(
        category: dashboard.activeJobMatch!['category']?.toString() ?? 'Service Request',
        distance: dashboard.liveDistanceKm != null
            ? '${dashboard.liveDistanceKm!.toStringAsFixed(1)} km'
            : (dashboard.activeJobMatch!['distanceKm'] != null
                ? '${dashboard.activeJobMatch!['distanceKm']} km'
                : 'Nearby'),
        urgency: dashboard.activeJobMatch!['urgency']?.toString() ?? 'Standard',
        description: dashboard.activeJobMatch!['description']?.toString(),
        aiRationale: (dashboard.activeJobMatch!['detectedProblem'] ??
                dashboard.activeJobMatch!['aiRationale'] ??
                dashboard.activeJobMatch!['rationale'])
            ?.toString(),
        isOutOfRange: dashboard.liveDistanceKm != null &&
            dashboard.liveDistanceKm! > dashboard.operatingRadiusKm,
        remainingSeconds: dashboard.remainingAcceptSeconds,
        totalTimeoutSeconds: dashboard.totalTimeoutSeconds,
        onAccept: onAcceptMatch ?? () => dashboard.acceptJob(),
        onDecline: onDeclineMatch ?? () => dashboard.declineJob(),
      );
    }

    // State 2: Ongoing Job (Accepted State)
    if (dashboard.isOnline &&
        dashboard.activeJobMatch != null &&
        dashboard.activeJobMatch!['status'] == 'Accepted') {
      final problemText = (dashboard.activeJobMatch!['detectedProblem'] ??
              dashboard.activeJobMatch!['description'] ??
              'Customer Assistance Request')
          .toString();
      final customerDesc = dashboard.activeJobMatch!['description']?.toString();
      final locationText = dashboard.activeJobMatch!['locationText']?.toString();
      final category = dashboard.activeJobMatch!['category']?.toString() ?? 'Service';
      final urgency = dashboard.activeJobMatch!['urgency']?.toString() ?? 'High';
      final distanceText = dashboard.liveDistanceKm != null
          ? '${dashboard.liveDistanceKm!.toStringAsFixed(1)} km away'
          : '${dashboard.activeJobMatch!['distanceKm'] ?? 'Nearby'} km';

      return Container(
        padding: const EdgeInsets.all(18),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(24),
          border: Border.all(color: Colors.green.shade200, width: 1.5),
          boxShadow: [
            BoxShadow(
              color: Colors.green.withValues(alpha: 0.08),
              blurRadius: 16,
              offset: const Offset(0, 4),
            ),
          ],
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Row(
                  children: [
                    Container(
                      width: 10,
                      height: 10,
                      decoration: const BoxDecoration(
                        color: Colors.green,
                        shape: BoxShape.circle,
                      ),
                    ),
                    const SizedBox(width: 8),
                    const Text(
                      'EN ROUTE TO CUSTOMER',
                      style: TextStyle(
                        fontWeight: FontWeight.w900,
                        fontSize: 12,
                        color: Colors.green,
                        letterSpacing: 0.5,
                      ),
                    ),
                  ],
                ),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                  decoration: BoxDecoration(
                    color: primaryColor.withValues(alpha: 0.08),
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Text(
                    category,
                    style: TextStyle(
                      color: primaryColor,
                      fontWeight: FontWeight.bold,
                      fontSize: 12,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            Text(
              problemText,
              style: const TextStyle(
                fontSize: 15,
                fontWeight: FontWeight.bold,
                color: Color(0xFF0F172A),
                height: 1.35,
              ),
            ),
            if (customerDesc != null &&
                customerDesc.isNotEmpty &&
                customerDesc != problemText) ...[
              const SizedBox(height: 6),
              Text(
                'Customer Note: "$customerDesc"',
                style: TextStyle(
                  fontSize: 12,
                  fontStyle: FontStyle.italic,
                  color: Colors.blueGrey.shade700,
                ),
              ),
            ],
            if (locationText != null && locationText.isNotEmpty) ...[
              const SizedBox(height: 10),
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(Icons.location_on, size: 16, color: Colors.blueGrey.shade600),
                  const SizedBox(width: 6),
                  Expanded(
                    child: Text(
                      locationText,
                      style: TextStyle(
                        fontSize: 12,
                        color: Colors.blueGrey.shade800,
                        height: 1.3,
                      ),
                    ),
                  ),
                ],
              ),
            ],
            const SizedBox(height: 12),
            const Divider(height: 1),
            const SizedBox(height: 12),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Row(
                  children: [
                    Icon(Icons.directions_car, size: 16, color: primaryColor),
                    const SizedBox(width: 6),
                    Text(
                      distanceText,
                      style: TextStyle(
                        fontWeight: FontWeight.bold,
                        fontSize: 13,
                        color: primaryColor,
                      ),
                    ),
                  ],
                ),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                  decoration: BoxDecoration(
                    color: Colors.red.shade50,
                    borderRadius: BorderRadius.circular(6),
                    border: Border.all(color: Colors.red.shade200),
                  ),
                  child: Text(
                    'Urgency: $urgency',
                    style: TextStyle(
                      fontWeight: FontWeight.bold,
                      fontSize: 11,
                      color: Colors.red.shade800,
                    ),
                  ),
                ),
              ],
            ),
            if (onSwitchToMap != null) ...[
              const SizedBox(height: 14),
              SizedBox(
                width: double.infinity,
                child: ElevatedButton.icon(
                  style: ElevatedButton.styleFrom(
                    backgroundColor: primaryColor,
                    foregroundColor: Colors.white,
                    padding: const EdgeInsets.symmetric(vertical: 12),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(12),
                    ),
                    elevation: 0,
                  ),
                  icon: const Icon(Icons.map_outlined, size: 18),
                  label: const Text(
                    'View on Map & Live Route',
                    style: TextStyle(fontSize: 13, fontWeight: FontWeight.bold),
                  ),
                  onPressed: onSwitchToMap,
                ),
              ),
            ],
          ],
        ),
      );
    }

    // State 3: Standby / Idle (Radar scanning)
    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(24),
        border: Border.all(color: const Color(0xFFF1F5F9)),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.02),
            blurRadius: 15,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 10,
                height: 10,
                decoration: BoxDecoration(
                  color: dashboard.isOnline ? Colors.green.shade500 : Colors.grey.shade400,
                  shape: BoxShape.circle,
                ),
              ),
              const SizedBox(width: 8),
              Text(
                dashboard.isOnline ? 'Dispatch Radar Active' : 'Dispatch Radar Standby',
                style: TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.bold,
                  color: dashboard.isOnline ? Colors.green.shade800 : Colors.grey.shade700,
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          Text(
            dashboard.isOnline
                ? 'Actively scanning for emergency requests within your ${dashboard.operatingRadiusKm.toInt()} km radius. Incoming alerts will appear here.'
                : 'You are currently offline. Switch status to Online above to receive customer job dispatches in real time.',
            style: TextStyle(
              fontSize: 12,
              color: Colors.grey.shade600,
              height: 1.4,
            ),
          ),
        ],
      ),
    );
  }

  void _confirmToggleOnline(BuildContext context, ProviderDashboardProvider dashboard) {
    if (dashboard.isOnline) {
      final bool hasActiveJob = dashboard.activeJobMatch != null &&
          dashboard.activeJobMatch!['status'] == 'Accepted';

      if (hasActiveJob) {
        showDialog(
          context: context,
          builder: (dialogCtx) => AlertDialog(
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
            title: const Row(
              children: [
                Icon(Icons.warning_amber_rounded, color: Colors.orange),
                SizedBox(width: 8),
                Expanded(
                  child: Text(
                    'Active Job in Progress',
                    style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                  ),
                ),
              ],
            ),
            content: const Text(
              'You currently have an active service job en route. Please complete or resolve your ongoing dispatch before going offline.',
            ),
            actions: [
              ElevatedButton(
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFF1F4E78),
                  foregroundColor: Colors.white,
                ),
                onPressed: () => Navigator.pop(dialogCtx),
                child: const Text('OK'),
              ),
            ],
          ),
        );
        return;
      }

      showDialog(
        context: context,
        builder: (dialogCtx) => AlertDialog(
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
          title: const Row(
            children: [
              Icon(Icons.power_settings_new, color: Colors.redAccent),
              SizedBox(width: 8),
              Expanded(
                child: Text(
                  'Go Offline?',
                  style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                ),
              ),
            ],
          ),
          content: const Text(
            'You will no longer receive incoming customer dispatch requests while offline. Are you sure you want to go offline?',
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(dialogCtx),
              child: const Text('Cancel'),
            ),
            ElevatedButton(
              style: ElevatedButton.styleFrom(
                backgroundColor: Colors.redAccent,
                foregroundColor: Colors.white,
              ),
              onPressed: () {
                Navigator.pop(dialogCtx);
                dashboard.toggleOnlineStatus(false);
              },
              child: const Text('Yes, Go Offline'),
            ),
          ],
        ),
      );
    } else {
      showDialog(
        context: context,
        builder: (dialogCtx) => AlertDialog(
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
          title: const Row(
            children: [
              Icon(Icons.sensors_rounded, color: Color(0xFF1F4E78)),
              SizedBox(width: 8),
              Expanded(
                child: Text(
                  'Go Online?',
                  style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                ),
              ),
            ],
          ),
          content: Text(
            'You will become available for automated dispatch matching within your ${dashboard.operatingRadiusKm.toInt()} km radius. Ready to receive requests?',
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(dialogCtx),
              child: const Text('Cancel'),
            ),
            ElevatedButton(
              style: ElevatedButton.styleFrom(
                backgroundColor: const Color(0xFF1F4E78),
                foregroundColor: Colors.white,
              ),
              onPressed: () {
                Navigator.pop(dialogCtx);
                dashboard.toggleOnlineStatus(true);
              },
              child: const Text('Yes, Go Online'),
            ),
          ],
        ),
      );
    }
  }

  void _showFullPhotoDialog(BuildContext context, String? imagePath, String? imageUrl) {
    showDialog(
      context: context,
      builder: (dialogCtx) => Dialog(
        backgroundColor: Colors.transparent,
        insetPadding: const EdgeInsets.all(16),
        child: Stack(
          alignment: Alignment.topRight,
          children: [
            InteractiveViewer(
              child: ClipRRect(
                borderRadius: BorderRadius.circular(16),
                child: imagePath != null && File(imagePath).existsSync()
                    ? Image.file(
                        File(imagePath),
                        fit: BoxFit.contain,
                      )
                    : imageUrl != null
                        ? Image.network(
                            imageUrl,
                            fit: BoxFit.contain,
                          )
                        : const SizedBox.shrink(),
              ),
            ),
            Positioned(
              top: 8,
              right: 8,
              child: CircleAvatar(
                backgroundColor: Colors.black54,
                radius: 18,
                child: IconButton(
                  padding: EdgeInsets.zero,
                  icon: const Icon(Icons.close, color: Colors.white, size: 20),
                  onPressed: () => Navigator.pop(dialogCtx),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildCompactStat(String title, String value, Color valueColor) {
    return Container(
      padding: const EdgeInsets.symmetric(vertical: 16, horizontal: 8),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: const Color(0xFFF1F5F9)),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.02),
            blurRadius: 15,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: Column(
        children: [
          Text(
            value,
            style: TextStyle(
              fontSize: 22,
              fontWeight: FontWeight.w900,
              color: valueColor,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            title.toUpperCase(),
            style: const TextStyle(
              fontSize: 9,
              fontWeight: FontWeight.w800,
              color: Color(0xFF94A3B8),
              letterSpacing: 0.5,
            ),
          ),
        ],
      ),
    );
  }
}
