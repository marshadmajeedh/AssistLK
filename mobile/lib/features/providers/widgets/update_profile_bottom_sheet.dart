import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../providers/provider_dashboard_provider.dart';

class UpdateProfileBottomSheet extends StatefulWidget {
  const UpdateProfileBottomSheet({super.key});

  @override
  State<UpdateProfileBottomSheet> createState() =>
      _UpdateProfileBottomSheetState();
}

class _UpdateProfileBottomSheetState extends State<UpdateProfileBottomSheet> {
  final _formKey = GlobalKey<FormState>();
  final TextEditingController _businessNameController = TextEditingController();
  bool _isSaving = false;

  @override
  void initState() {
    super.initState();
    final dashboard = context.read<ProviderDashboardProvider>();
    _businessNameController.text = dashboard.businessName;
  }

  @override
  void dispose() {
    _businessNameController.dispose();
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

  Future<void> _saveProfile() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() => _isSaving = true);
    try {
      final dashboard = context.read<ProviderDashboardProvider>();
      await dashboard.updateProfile(
        businessName: _businessNameController.text.trim(),
        operatingRadiusKm: dashboard.operatingRadiusKm,
        latitude: dashboard.latitude,
        longitude: dashboard.longitude,
      );

      if (!mounted) return;
      Navigator.pop(context);
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          backgroundColor: Colors.green,
          content: Text('Business profile updated successfully!'),
        ),
      );
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          backgroundColor: Colors.red,
          content: Text('Update failed: $e'),
        ),
      );
    } finally {
      if (mounted) {
        setState(() => _isSaving = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final dashboard = context.watch<ProviderDashboardProvider>();
    final categoryColor = _getCategoryColor(dashboard.category);
    final categoryIcon = _getCategoryIcon(dashboard.category);
    final email = dashboard.profile?['email']?.toString() ?? '';
    final skillName = dashboard.profile?['skillName']?.toString() ?? '';
    final completedJobs = dashboard.profile?['totalCompletedJobs'] ?? 0;

    return Padding(
      padding: EdgeInsets.only(
        bottom: MediaQuery.of(context).viewInsets.bottom,
      ),
      child: SingleChildScrollView(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
          child: Form(
            key: _formKey,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                // Modal Header
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text(
                          'Business Profile',
                          style: TextStyle(
                            fontSize: 18,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                        const SizedBox(height: 2),
                        Text(
                          'Public identity presented to customers',
                          style: TextStyle(
                            fontSize: 12,
                            color: Colors.grey.shade600,
                          ),
                        ),
                      ],
                    ),
                    IconButton(
                      icon: const Icon(Icons.close),
                      onPressed: () => Navigator.pop(context),
                    ),
                  ],
                ),
                const Divider(height: 20),

                // Read-Only Verified Account Summary Card
                Container(
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: Colors.grey.shade50,
                    borderRadius: BorderRadius.circular(12),
                    border: Border.all(color: Colors.grey.shade200),
                  ),
                  child: Column(
                    children: [
                      Row(
                        children: [
                          CircleAvatar(
                            radius: 22,
                            backgroundColor:
                                categoryColor.withValues(alpha: 0.15),
                            child: Icon(
                              categoryIcon,
                              color: categoryColor,
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
                                    fontSize: 15,
                                  ),
                                  overflow: TextOverflow.ellipsis,
                                ),
                                if (email.isNotEmpty)
                                  Text(
                                    email,
                                    style: TextStyle(
                                      fontSize: 12,
                                      color: Colors.grey.shade600,
                                    ),
                                    overflow: TextOverflow.ellipsis,
                                  ),
                              ],
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 10),
                      Row(
                        children: [
                          // Category badge
                          Container(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 8,
                              vertical: 3,
                            ),
                            decoration: BoxDecoration(
                              color: categoryColor.withValues(alpha: 0.12),
                              borderRadius: BorderRadius.circular(6),
                              border: Border.all(
                                color: categoryColor.withValues(alpha: 0.35),
                              ),
                            ),
                            child: Text(
                              skillName.isNotEmpty
                                  ? '${dashboard.category} • $skillName'
                                  : dashboard.category,
                              style: TextStyle(
                                fontSize: 11,
                                fontWeight: FontWeight.w600,
                                color: categoryColor,
                              ),
                            ),
                          ),
                          const Spacer(),
                          // Verified badge
                          Container(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 8,
                              vertical: 3,
                            ),
                            decoration: BoxDecoration(
                              color: Colors.green.shade50,
                              borderRadius: BorderRadius.circular(6),
                              border: Border.all(color: Colors.green.shade200),
                            ),
                            child: Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                const Icon(
                                  Icons.star,
                                  size: 12,
                                  color: Colors.amber,
                                ),
                                const SizedBox(width: 3),
                                Text(
                                  '${dashboard.rating.toStringAsFixed(1)} ★  •  $completedJobs jobs',
                                  style: TextStyle(
                                    fontSize: 11,
                                    fontWeight: FontWeight.bold,
                                    color: Colors.green.shade900,
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
                const SizedBox(height: 16),

                // Editable Business Name Field
                TextFormField(
                  controller: _businessNameController,
                  decoration: const InputDecoration(
                    labelText: 'Business / Workshop Name',
                    hintText: 'e.g., Nimal Quick Vehicle Repairs',
                    prefixIcon: Icon(Icons.storefront_outlined),
                    border: OutlineInputBorder(),
                    helperText:
                        'Displayed to matched customers on job dispatches and quotes.',
                  ),
                  validator: (val) {
                    if (val == null || val.trim().isEmpty) {
                      return 'Business name is required';
                    }
                    if (val.trim().length < 3) {
                      return 'Business name must be at least 3 characters';
                    }
                    if (val.trim().length > 200) {
                      return 'Business name must be under 200 characters';
                    }
                    return null;
                  },
                ),
                const SizedBox(height: 14),

                // Helpful note about live location & radius
                Container(
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(
                    color: Colors.blue.shade50,
                    borderRadius: BorderRadius.circular(8),
                    border: Border.all(color: Colors.blue.shade100),
                  ),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Icon(
                        Icons.info_outline,
                        size: 16,
                        color: Colors.blue.shade700,
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          'Your operating radius and GPS location are managed in real time directly on your live workspace cockpit map.',
                          style: TextStyle(
                            fontSize: 11.5,
                            color: Colors.blue.shade900,
                            height: 1.3,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 18),

                // Submit Button
                ElevatedButton(
                  style: ElevatedButton.styleFrom(
                    backgroundColor: Colors.blue.shade700,
                    foregroundColor: Colors.white,
                    padding: const EdgeInsets.symmetric(vertical: 14),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(10),
                    ),
                  ),
                  onPressed: _isSaving ? null : _saveProfile,
                  child: _isSaving
                      ? const SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            color: Colors.white,
                          ),
                        )
                      : const Text(
                          'Save Changes',
                          style: TextStyle(
                            fontSize: 15,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
