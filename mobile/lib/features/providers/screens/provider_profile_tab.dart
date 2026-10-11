import '../../../../core/services/notification_service.dart';
import 'dart:io';
import 'package:image_picker/image_picker.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../providers/provider_dashboard_provider.dart';
import '../../auth/providers/auth_provider.dart';

class ProviderProfileTab extends StatelessWidget {
  const ProviderProfileTab({super.key});

  @override
  Widget build(BuildContext context) {
    final dashboard = context.watch<ProviderDashboardProvider>();
    final auth = context.read<AuthProvider>();
    
    // Customer Blue Theme Colors
    const primaryColor = Color(0xFF1F4E78);
    const primaryDark = Color(0xFF173B5E);

    return Scaffold(
      backgroundColor: const Color(0xFFF8FAFC), // Slate 50
      body: SingleChildScrollView(
        padding: const EdgeInsets.only(bottom: 40),
        child: Column(
          children: [
            // 1. Top Gradient Header with Avatar
            Container(
              width: double.infinity,
              decoration: const BoxDecoration(
                gradient: LinearGradient(
                  colors: [primaryColor, primaryDark],
                  begin: Alignment.topLeft,
                  end: Alignment.bottomRight,
                ),
                borderRadius: BorderRadius.vertical(bottom: Radius.circular(32)),
              ),
              child: SafeArea(
                bottom: false,
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(24, 24, 24, 32),
                  child: Column(
                    children: [
                      // Header Title
                      Row(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          const Text(
                            'My Profile',
                            style: TextStyle(
                              color: Colors.white,
                              fontSize: 20,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 24),
                      
                      // Avatar & Camera Edit Badge (Clickable like WhatsApp)
                      GestureDetector(
                        onTap: () => _showProfilePictureOptions(context, dashboard),
                        child: Stack(
                          alignment: Alignment.bottomRight,
                          children: [
                            Container(
                              padding: const EdgeInsets.all(4),
                              decoration: BoxDecoration(
                                color: Colors.white.withValues(alpha: 0.25),
                                shape: BoxShape.circle,
                              ),
                              child: CircleAvatar(
                                radius: 64,
                                backgroundColor: Colors.white,
                                backgroundImage: dashboard.profileImagePath != null && File(dashboard.profileImagePath!).existsSync()
                                    ? FileImage(File(dashboard.profileImagePath!))
                                    : NetworkImage(dashboard.defaultAvatarUrl) as ImageProvider,
                              ),
                            ),
                            // Small WhatsApp-style Camera button on corner
                            Positioned(
                              bottom: 4,
                              right: 4,
                              child: Container(
                                padding: const EdgeInsets.all(8),
                                decoration: BoxDecoration(
                                  color: Colors.white,
                                  shape: BoxShape.circle,
                                  border: Border.all(color: primaryColor, width: 2),
                                  boxShadow: const [
                                    BoxShadow(
                                      color: Colors.black26,
                                      blurRadius: 4,
                                      offset: Offset(0, 2),
                                    ),
                                  ],
                                ),
                                child: const Icon(
                                  Icons.camera_alt,
                                  color: primaryColor,
                                  size: 18,
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: 16),
                      
                      // Name & Business
                      Text(
                        dashboard.fullName.isNotEmpty ? dashboard.fullName : 'Technician',
                        style: const TextStyle(
                          fontSize: 24,
                          fontWeight: FontWeight.bold,
                          color: Colors.white,
                        ),
                      ),
                      if (dashboard.businessName.isNotEmpty) ...[
                        const SizedBox(height: 4),
                        Text(
                          dashboard.businessName,
                          style: TextStyle(
                            fontSize: 14,
                            color: Colors.white.withValues(alpha: 0.8),
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
              ),
            ),
            
            const SizedBox(height: 24),

            // Metrics Grid
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20),
              child: Row(
                children: [
                  Expanded(child: _buildMetricCard('Rating', dashboard.rating > 0 ? dashboard.rating.toStringAsFixed(1) : '0.0', Icons.star, Colors.amber)),
                  const SizedBox(width: 12),
                  Expanded(child: _buildMetricCard('Completed', (dashboard.profile?['completedJobs'] ?? 0).toString(), Icons.check_circle, primaryColor)),
                  const SizedBox(width: 12),
                  Expanded(child: _buildMetricCard('Experience', '3+ yrs', Icons.work, Colors.blue)),
                ],
              ),
            ),
            const SizedBox(height: 24),

            // SKILLS SECTION
            Container(
              margin: const EdgeInsets.symmetric(horizontal: 20),
              width: double.infinity,
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
                  const Text(
                    'Registered Services',
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.bold,
                      color: Color(0xFF1E293B),
                    ),
                  ),
                  const SizedBox(height: 16),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: dashboard.categories.isEmpty 
                        ? [const Text('No skills registered')]
                        : dashboard.categories.map((cat) => _buildSkillChip(cat, primaryColor)).toList(),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 24),

            // Alerts & Audio Settings Button
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20),
              child: InkWell(
                onTap: () => _openSettingsDialog(context, dashboard),
                borderRadius: BorderRadius.circular(20),
                child: Container(
                  padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
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
                  child: Row(
                    children: [
                      Container(
                        padding: const EdgeInsets.all(10),
                        decoration: BoxDecoration(
                          color: primaryColor.withValues(alpha: 0.1),
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: const Icon(Icons.notifications_active, color: primaryColor, size: 20),
                      ),
                      const SizedBox(width: 16),
                      const Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              'Alert & Audio Settings',
                              style: TextStyle(fontSize: 15, fontWeight: FontWeight.bold, color: Color(0xFF1E293B)),
                            ),
                            SizedBox(height: 2),
                            Text(
                              'Manage voice alerts & notifications',
                              style: TextStyle(fontSize: 12, color: Colors.grey),
                            ),
                          ],
                        ),
                      ),
                      const Icon(Icons.chevron_right, color: Colors.grey),
                    ],
                  ),
                ),
              ),
            ),
            const SizedBox(height: 24),

            // LOGOUT BUTTON
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20),
              child: SizedBox(
                width: double.infinity,
                child: OutlinedButton.icon(
                  style: OutlinedButton.styleFrom(
                    foregroundColor: Colors.red.shade700,
                    side: BorderSide(color: Colors.red.shade200),
                    padding: const EdgeInsets.symmetric(vertical: 16),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(16),
                    ),
                  ),
                  icon: const Icon(Icons.logout),
                  label: const Text(
                    'Log Out',
                    style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                  ),
                  onPressed: () => _confirmLogout(context, dashboard, auth),
                ),
              ),
            ),
          ],
        ),
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

  void _showProfilePictureOptions(BuildContext context, ProviderDashboardProvider dashboard) {
    showModalBottomSheet(
      context: context,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (BuildContext ctx) {
        return SafeArea(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 16),
                child: Text(
                  'Profile Photo',
                  style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                ),
              ),
                            if (dashboard.profileImagePath != null && File(dashboard.profileImagePath!).existsSync())
                ListTile(
                  leading: const Icon(Icons.fullscreen, color: Color(0xFF1F4E78)),
                  title: const Text('View Full Photo'),
                  onTap: () {
                    Navigator.pop(ctx);
                    _showFullPhotoDialog(context, dashboard.profileImagePath!);
                  },
                ),
              ListTile(
                leading: const Icon(Icons.camera_alt, color: Color(0xFF1F4E78)),
                title: const Text('Take Photo'),
                onTap: () async {
                  Navigator.pop(ctx);
                  try {
                    final picker = ImagePicker();
                    final image = await picker.pickImage(
                      source: ImageSource.camera,
                      maxWidth: 800,
                      maxHeight: 800,
                      imageQuality: 85,
                    );
                    if (image != null) {
                      await dashboard.updateProfileImage(image.path);
                    }
                  } catch (e) {
                    debugPrint('Error picking from camera: $e');
                  }
                },
              ),
              ListTile(
                leading: const Icon(Icons.photo_library, color: Color(0xFF1F4E78)),
                title: const Text('Choose from Gallery'),
                onTap: () async {
                  Navigator.pop(ctx);
                  try {
                    final picker = ImagePicker();
                    final image = await picker.pickImage(
                      source: ImageSource.gallery,
                      maxWidth: 800,
                      maxHeight: 800,
                      imageQuality: 85,
                    );
                    if (image != null) {
                      await dashboard.updateProfileImage(image.path);
                    }
                  } catch (e) {
                    debugPrint('Error picking from gallery: $e');
                  }
                },
              ),
              if (dashboard.profileImagePath != null)
                ListTile(
                  leading: const Icon(Icons.delete, color: Colors.redAccent),
                  title: const Text('Remove Photo', style: TextStyle(color: Colors.redAccent)),
                  onTap: () async {
                    Navigator.pop(ctx);
                    await dashboard.removeProfileImage();
                  },
                ),
            ],
          ),
        );
      },
    );
  }

  

  void _showFullPhotoDialog(BuildContext context, String imagePath) {
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
                child: Image.file(
                  File(imagePath),
                  fit: BoxFit.contain,
                ),
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

  void _openSettingsDialog(
    BuildContext context,
    ProviderDashboardProvider dashboard,
  ) {
    showDialog(
      context: context,
      builder: (dialogCtx) {
        return StatefulBuilder(
          builder: (context, setDialogState) {
            // Mobile native notifications
            return AlertDialog(
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(16),
              ),
              title: const Row(
                children: [
                  Icon(Icons.settings, color: Color(0xFF1F4E78)),
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
                          ? const Color(0xFF1F4E78)
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
                    'Mobile Push Notifications',
                    style: TextStyle(
                      fontWeight: FontWeight.bold,
                      fontSize: 13,
                      color: Colors.blueGrey,
                    ),
                  ),
                  const SizedBox(height: 4),
                  const Text(
                    'Heads-up alert banners for incoming dispatches on your phone',
                    style: TextStyle(fontSize: 12, color: Colors.grey),
                  ),
                  const SizedBox(height: 12),
                  Wrap(
                    spacing: 10,
                    runSpacing: 8,
                    children: [
                      OutlinedButton.icon(
                        icon: const Icon(Icons.volume_up, size: 16),
                        label: const Text('Test Voice Alert'),
                        onPressed: () {
                          dashboard.flutterTts.speak('Incoming high priority service request from AssistLK');
                        },
                      ),
                      ElevatedButton.icon(
                        style: ElevatedButton.styleFrom(
                          backgroundColor: const Color(0xFF1F4E78),
                          foregroundColor: Colors.white,
                        ),
                        icon: const Icon(Icons.send_rounded, size: 16),
                        label: const Text('Test Notification'),
                        onPressed: () async {
                          await NotificationService().showJobAlertNotification(
                            id: 101,
                            title: 'AssistLK Job Dispatch',
                            body: 'New ${dashboard.category} request nearby in your service area',
                          );
                        },
                      ),
                    ],
                  ),
                ],
              ),
              actions: [
                TextButton(
                  onPressed: () => Navigator.pop(dialogCtx),
                  child: const Text('Done', style: TextStyle(color: Color(0xFF1F4E78))),
                ),
              ],
            );
          },
        );
      },
    );
  }

  Widget _buildMetricCard(String title, String value, IconData icon, Color iconColor) {
    return Container(
      padding: const EdgeInsets.all(16),
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
          Icon(icon, size: 24, color: iconColor),
          const SizedBox(height: 8),
          Text(
            value,
            style: const TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.bold,
              color: Color(0xFF1E293B),
            ),
          ),
          const SizedBox(height: 2),
          Text(
            title,
            style: const TextStyle(
              fontSize: 11,
              fontWeight: FontWeight.w600,
              color: Color(0xFF64748B),
              letterSpacing: 0.5,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildSkillChip(String skill, Color themeColor) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
      decoration: BoxDecoration(
        color: themeColor.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: themeColor.withValues(alpha: 0.15)),
      ),
      child: Text(
        skill,
        style: TextStyle(
          fontSize: 13,
          fontWeight: FontWeight.bold,
          color: themeColor,
        ),
      ),
    );
  }
}
