import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../../shared/theme/app_colors.dart';
import '../../../../shared/theme/app_radius.dart';
import '../../../../shared/theme/app_spacing.dart';
import '../../../../shared/theme/app_text_styles.dart';
import '../../../../shared/widgets/app_button.dart';
import '../../../../shared/widgets/app_text_field.dart';
import '../models/canonical_service_category.dart';
import '../models/service_request_model.dart';
import '../models/update_service_request_dto.dart';
import '../providers/service_request_provider.dart';
import '../services/location_service.dart';

class EditServiceRequestScreen extends StatefulWidget {
  final ServiceRequestModel request;
  final LocationService? locationService;

  const EditServiceRequestScreen({
    super.key,
    required this.request,
    this.locationService,
  });

  @override
  State<EditServiceRequestScreen> createState() =>
      _EditServiceRequestScreenState();
}

class _EditServiceRequestScreenState extends State<EditServiceRequestScreen> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _descriptionController;
  late final TextEditingController _locationController;
  late final LocationService _locationService;
  String? _selectedPreference;

  late final String _initialLocationText;
  late final double? _initialLatitude;
  late final double? _initialLongitude;
  double? _latitude;
  double? _longitude;
  bool _isObtainingLocation = false;
  String? _locationFeedbackMessage;
  bool _isLocationError = false;
  bool _explicitChoiceMade = false;
  bool _showConfirmationPrompt = false;

  @override
  void initState() {
    super.initState();
    _locationService = widget.locationService ?? GeolocatorLocationService();
    _descriptionController =
        TextEditingController(text: widget.request.description);
    _locationController =
        TextEditingController(text: widget.request.locationText);
    _locationController.addListener(_onLocationTextChanged);
    _selectedPreference = widget.request.categoryHint;

    _initialLocationText = widget.request.locationText;
    _initialLatitude = widget.request.latitude;
    _initialLongitude = widget.request.longitude;
    _latitude = widget.request.latitude;
    _longitude = widget.request.longitude;
  }

  void _onLocationTextChanged() {
    if (!mounted) return;
    setState(() {
      if (!_locationTextChanged) {
        _showConfirmationPrompt = false;
        _explicitChoiceMade = false;
        _latitude = _initialLatitude;
        _longitude = _initialLongitude;
        _locationFeedbackMessage = null;
      }
    });
  }

  bool get _hasOriginalCoordinates =>
      _initialLatitude != null && _initialLongitude != null;

  bool get _locationTextChanged =>
      _locationController.text.trim() != _initialLocationText.trim();

  bool get _requiresConfirmation =>
      _hasOriginalCoordinates &&
      _locationTextChanged &&
      !_explicitChoiceMade &&
      _latitude != null &&
      _longitude != null;

  @override
  void dispose() {
    _locationController.removeListener(_onLocationTextChanged);
    _descriptionController.dispose();
    _locationController.dispose();
    super.dispose();
  }

  void _showChangePreferenceSheet() {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(
          top: Radius.circular(AppRadius.large),
        ),
      ),
      builder: (sheetCtx) {
        return SafeArea(
          child: Padding(
            padding: const EdgeInsets.symmetric(
              horizontal: AppSpacing.lg,
              vertical: AppSpacing.md,
            ),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    const Text(
                      'Select Service Preference',
                      style: AppTextStyles.sectionHeading,
                    ),
                    IconButton(
                      icon: const Icon(Icons.close),
                      onPressed: () => Navigator.of(sheetCtx).pop(),
                    ),
                  ],
                ),
                const SizedBox(height: AppSpacing.sm),
                for (final cat in CanonicalServiceCategory.canonicalShortcuts) ...[
                  ListTile(
                    leading: Icon(cat.icon, color: AppColors.primary),
                    title: Text(cat.displayName, style: AppTextStyles.cardHeading),
                    subtitle: Text(cat.description, style: AppTextStyles.small),
                    trailing: _selectedPreference == cat.canonicalName
                        ? const Icon(Icons.check, color: AppColors.primary)
                        : null,
                    onTap: () {
                      setState(() {
                        _selectedPreference = cat.canonicalName;
                      });
                      Navigator.of(sheetCtx).pop();
                    },
                  ),
                  const Divider(height: 1, color: AppColors.border),
                ],
                ListTile(
                  leading: const Icon(
                    Icons.auto_awesome_rounded,
                    color: AppColors.primary,
                  ),
                  title: const Text('Let AssistLK AI identify', style: AppTextStyles.cardHeading),
                  subtitle: const Text(
                    'AssistLK AI will determine the service category',
                    style: AppTextStyles.small,
                  ),
                  trailing: _selectedPreference == null
                      ? const Icon(Icons.check, color: AppColors.primary)
                      : null,
                  onTap: () {
                    setState(() {
                      _selectedPreference = null;
                    });
                    Navigator.of(sheetCtx).pop();
                  },
                ),
              ],
            ),
          ),
        );
      },
    );
  }

  Future<void> _useCurrentLocation() async {
    setState(() {
      _isObtainingLocation = true;
      _locationFeedbackMessage = null;
      _isLocationError = false;
    });

    try {
      final result = await _locationService.getCurrentLocation();
      if (!mounted) return;

      if (result.isSuccess) {
        setState(() {
          _latitude = result.coordinates!.latitude;
          _longitude = result.coordinates!.longitude;
          _explicitChoiceMade = true;
          _showConfirmationPrompt = false;
          _locationFeedbackMessage = 'GPS location updated';
          _isLocationError = false;
        });
      } else {
        setState(() {
          _locationFeedbackMessage = result.message ??
              'Could not retrieve location. Please enter address manually.';
          _isLocationError = true;
        });
      }
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _locationFeedbackMessage =
            'Could not retrieve your current location. Please enter the location manually.';
        _isLocationError = true;
      });
    } finally {
      if (mounted) {
        setState(() {
          _isObtainingLocation = false;
        });
      }
    }
  }

  void _removeGpsCoordinates() {
    setState(() {
      _latitude = null;
      _longitude = null;
      _explicitChoiceMade = true;
      _showConfirmationPrompt = false;
      _locationFeedbackMessage = 'GPS coordinates removed';
      _isLocationError = false;
    });
  }

  void _keepExistingGpsCoordinates() {
    setState(() {
      _latitude = _initialLatitude;
      _longitude = _initialLongitude;
      _explicitChoiceMade = true;
      _showConfirmationPrompt = false;
      _locationFeedbackMessage = 'Original GPS coordinates retained';
      _isLocationError = false;
    });
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    if (_requiresConfirmation) {
      setState(() {
        _showConfirmationPrompt = true;
      });
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text(
            'Please confirm how to handle the attached GPS coordinates.',
          ),
          backgroundColor: AppColors.warning,
        ),
      );
      return;
    }

    final provider = context.read<ServiceRequestProvider>();
    final canonicalHint =
        CanonicalServiceCategory.toCanonicalCategoryHint(_selectedPreference);
    final dto = UpdateServiceRequestDto(
      description: _descriptionController.text.trim(),
      locationText: _locationController.text.trim(),
      latitude: _latitude,
      longitude: _longitude,
      categoryHint: canonicalHint,
    );

    final updated = await provider.updateRequest(
      widget.request.serviceRequestId,
      dto,
    );

    if (!mounted) return;

    if (updated != null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Request updated successfully!'),
          backgroundColor: AppColors.success,
        ),
      );
      Navigator.of(context).pop(updated);
    } else {
      final errorMessage =
          provider.error ?? 'Failed to update request. Please try again.';
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(errorMessage),
          backgroundColor: AppColors.error,
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<ServiceRequestProvider>();
    final selectedCategory =
        CanonicalServiceCategory.fromCanonicalOrDisplayName(_selectedPreference);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Edit Request Details'),
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(AppSpacing.lg),
          child: Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                // Service Preference Banner Card
                Container(
                  width: double.infinity,
                  padding: const EdgeInsets.all(AppSpacing.md),
                  decoration: BoxDecoration(
                    color: AppColors.surface,
                    borderRadius: BorderRadius.circular(AppRadius.large),
                    border: Border.all(color: AppColors.border),
                  ),
                  child: Row(
                    children: [
                      Container(
                        padding: const EdgeInsets.all(AppSpacing.sm),
                        decoration: BoxDecoration(
                          color: AppColors.primary.withValues(alpha: 0.08),
                          borderRadius: BorderRadius.circular(AppRadius.medium),
                        ),
                        child: Icon(
                          selectedCategory?.icon ?? Icons.auto_awesome_rounded,
                          color: AppColors.primary,
                          size: 22,
                        ),
                      ),
                      const SizedBox(width: AppSpacing.md),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            const Text(
                              'Service preference',
                              style: AppTextStyles.small,
                            ),
                            const SizedBox(height: 2),
                            Text(
                              selectedCategory?.displayName ?? 'Let AssistLK AI identify',
                              style: AppTextStyles.cardHeading,
                            ),
                          ],
                        ),
                      ),
                      TextButton(
                        onPressed: _showChangePreferenceSheet,
                        child: Text(
                          selectedCategory != null ? 'Change' : 'Choose preference',
                          style: const TextStyle(fontWeight: FontWeight.w600),
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: AppSpacing.md),

                const Text(
                  'Update problem description',
                  style: AppTextStyles.sectionHeading,
                ),
                const SizedBox(height: AppSpacing.xs),
                Text(
                  'Provide more clarity or details to resolve follow-up questions from AssistLK AI.',
                  style: AppTextStyles.body.copyWith(
                    color: AppColors.textSecondary,
                  ),
                ),
                const SizedBox(height: AppSpacing.md),

                // Description Field
                AppTextField(
                  controller: _descriptionController,
                  label: 'Problem Description',
                  hint: 'Describe the issue in detail...',
                  maxLines: 4,
                  validator: (value) {
                    if (value == null || value.trim().isEmpty) {
                      return 'Please describe the problem.';
                    }
                    if (value.trim().length < 10) {
                      return 'Please provide at least 10 characters.';
                    }
                    if (value.trim().length > 4000) {
                      return 'Description cannot exceed 4000 characters.';
                    }
                    return null;
                  },
                ),
                const SizedBox(height: AppSpacing.md),

                // Location Field
                AppTextField(
                  controller: _locationController,
                  label: 'Location / Address',
                  hint: 'e.g., Colombo 03, Havelock Road',
                  validator: (value) {
                    if (value == null || value.trim().isEmpty) {
                      return 'Please provide the service location.';
                    }
                    if (value.trim().length > 255) {
                      return 'Location cannot exceed 255 characters.';
                    }
                    return null;
                  },
                ),
                const SizedBox(height: AppSpacing.sm),

                // GPS Status or Removal Notice
                if (_latitude != null && _longitude != null) ...[
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: AppSpacing.md,
                      vertical: AppSpacing.sm,
                    ),
                    decoration: BoxDecoration(
                      color: AppColors.success.withValues(alpha: 0.08),
                      borderRadius: BorderRadius.circular(AppRadius.medium),
                      border: Border.all(
                        color: AppColors.success.withValues(alpha: 0.3),
                      ),
                    ),
                    child: Row(
                      children: [
                        const Icon(
                          Icons.my_location_rounded,
                          size: 16,
                          color: AppColors.success,
                        ),
                        const SizedBox(width: AppSpacing.xs),
                        Expanded(
                          child: Text(
                            'GPS location attached (${_latitude!.toStringAsFixed(4)}, ${_longitude!.toStringAsFixed(4)})',
                            style: AppTextStyles.small.copyWith(
                              color: AppColors.success,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: AppSpacing.xs),
                ] else if (_hasOriginalCoordinates) ...[
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: AppSpacing.md,
                      vertical: AppSpacing.sm,
                    ),
                    decoration: BoxDecoration(
                      color: AppColors.surface,
                      borderRadius: BorderRadius.circular(AppRadius.medium),
                      border: Border.all(color: AppColors.border),
                    ),
                    child: Row(
                      children: [
                        const Icon(
                          Icons.location_off_outlined,
                          size: 16,
                          color: AppColors.textSecondary,
                        ),
                        const SizedBox(width: AppSpacing.xs),
                        Expanded(
                          child: Text(
                            'GPS coordinates will be removed on save',
                            style: AppTextStyles.small.copyWith(
                              color: AppColors.textSecondary,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: AppSpacing.xs),
                ],

                // Stale GPS Confirmation Prompt
                if (_requiresConfirmation || _showConfirmationPrompt) ...[
                  Container(
                    key: const Key('edit_gps_confirmation_prompt'),
                    padding: const EdgeInsets.all(AppSpacing.md),
                    decoration: BoxDecoration(
                      color: AppColors.warning.withValues(alpha: 0.08),
                      borderRadius: BorderRadius.circular(AppRadius.medium),
                      border: Border.all(
                        color: AppColors.warning.withValues(alpha: 0.4),
                      ),
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Row(
                          children: [
                            Icon(
                              Icons.warning_amber_rounded,
                              color: AppColors.warning,
                              size: 20,
                            ),
                            SizedBox(width: AppSpacing.xs),
                            Expanded(
                              child: Text(
                                'Address changed with attached GPS',
                                style: TextStyle(
                                  fontWeight: FontWeight.w600,
                                  color: AppColors.warning,
                                  fontSize: 13,
                                ),
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: AppSpacing.xs),
                        Text(
                          'You edited the service address. Please choose how to handle the attached GPS coordinates:',
                          style: AppTextStyles.small.copyWith(
                            color: AppColors.textPrimary,
                          ),
                        ),
                        const SizedBox(height: AppSpacing.sm),
                        Wrap(
                          spacing: AppSpacing.sm,
                          runSpacing: AppSpacing.xs,
                          children: [
                            OutlinedButton.icon(
                              key: const Key('edit_update_gps_button'),
                              onPressed: _isObtainingLocation
                                  ? null
                                  : _useCurrentLocation,
                              icon: _isObtainingLocation
                                  ? const SizedBox(
                                      width: 14,
                                      height: 14,
                                      child: CircularProgressIndicator(
                                        strokeWidth: 2,
                                      ),
                                    )
                                  : const Icon(
                                      Icons.my_location_rounded,
                                      size: 15,
                                    ),
                              label: const Text(
                                'Update with Current Location',
                                style: TextStyle(fontSize: 12),
                              ),
                            ),
                            OutlinedButton.icon(
                              key: const Key('edit_remove_gps_button'),
                              onPressed: _removeGpsCoordinates,
                              icon: const Icon(
                                Icons.delete_outline_rounded,
                                size: 15,
                                color: AppColors.error,
                              ),
                              label: const Text(
                                'Remove GPS Coordinates',
                                style: TextStyle(
                                  color: AppColors.error,
                                  fontSize: 12,
                                ),
                              ),
                            ),
                            OutlinedButton.icon(
                              key: const Key('edit_keep_gps_button'),
                              onPressed: _keepExistingGpsCoordinates,
                              icon: const Icon(Icons.check_rounded, size: 15),
                              label: const Text(
                                'Keep Existing GPS Coordinates',
                                style: TextStyle(fontSize: 12),
                              ),
                            ),
                          ],
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: AppSpacing.sm),
                ] else ...[
                  // Normal Quick Actions
                  Wrap(
                    spacing: AppSpacing.sm,
                    children: [
                      TextButton.icon(
                        key: const Key('edit_quick_update_gps_button'),
                        onPressed:
                            _isObtainingLocation ? null : _useCurrentLocation,
                        icon: _isObtainingLocation
                            ? const SizedBox(
                                width: 14,
                                height: 14,
                                child: CircularProgressIndicator(
                                  strokeWidth: 2,
                                ),
                              )
                            : const Icon(Icons.my_location_rounded, size: 15),
                        label: Text(
                          _latitude != null
                              ? 'Update with Current Location'
                              : 'Use Current Location',
                          style: const TextStyle(fontSize: 12),
                        ),
                      ),
                      if (_latitude != null)
                        TextButton.icon(
                          key: const Key('edit_quick_remove_gps_button'),
                          onPressed: _removeGpsCoordinates,
                          icon: const Icon(
                            Icons.delete_outline_rounded,
                            size: 15,
                            color: AppColors.error,
                          ),
                          label: const Text(
                            'Remove GPS Coordinates',
                            style: TextStyle(
                              color: AppColors.error,
                              fontSize: 12,
                            ),
                          ),
                        ),
                    ],
                  ),
                ],

                if (_locationFeedbackMessage != null) ...[
                  const SizedBox(height: AppSpacing.xs),
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: AppSpacing.sm,
                      vertical: AppSpacing.xs,
                    ),
                    decoration: BoxDecoration(
                      color: (_isLocationError
                              ? AppColors.warning
                              : AppColors.primary)
                          .withValues(alpha: 0.08),
                      borderRadius: BorderRadius.circular(AppRadius.small),
                    ),
                    child: Text(
                      _locationFeedbackMessage!,
                      style: AppTextStyles.small.copyWith(
                        color: _isLocationError
                            ? AppColors.warning
                            : AppColors.primary,
                      ),
                    ),
                  ),
                ],
                const SizedBox(height: AppSpacing.md),

                // Submit Button
                AppButton(
                  text: 'Save Changes',
                  isLoading: provider.isLoading,
                  onPressed: _submit,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
