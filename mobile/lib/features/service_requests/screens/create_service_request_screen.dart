import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../../shared/theme/app_colors.dart';
import '../../../../shared/theme/app_radius.dart';
import '../../../../shared/theme/app_spacing.dart';
import '../../../../shared/theme/app_text_styles.dart';
import '../../../../shared/widgets/app_button.dart';
import '../../../../shared/widgets/app_card.dart';
import '../../../../shared/widgets/app_text_field.dart';
import '../models/canonical_service_category.dart';
import '../models/create_service_request_dto.dart';
import '../providers/service_request_provider.dart';
import '../services/location_service.dart';
import '../widgets/step_indicator.dart';
import 'service_request_detail_screen.dart';

class CreateServiceRequestScreen extends StatefulWidget {
  final String? initialCategoryPreference;
  final LocationService? locationService;

  const CreateServiceRequestScreen({
    super.key,
    this.initialCategoryPreference,
    this.locationService,
  });

  @override
  State<CreateServiceRequestScreen> createState() =>
      _CreateServiceRequestScreenState();
}

class _CreateServiceRequestScreenState
    extends State<CreateServiceRequestScreen> {
  int _currentStep = 0;
  String? _selectedPreference;
  late final LocationService _locationService;

  double? _latitude;
  double? _longitude;
  bool _isObtainingLocation = false;
  String? _locationFeedbackMessage;
  bool _isLocationError = false;

  final _detailsFormKey = GlobalKey<FormState>();
  final _locationFormKey = GlobalKey<FormState>();
  final _descriptionController = TextEditingController();
  final _locationController = TextEditingController();

  @override
  void initState() {
    super.initState();
    _selectedPreference = widget.initialCategoryPreference;
    _locationService = widget.locationService ?? GeolocatorLocationService();
  }

  @override
  void dispose() {
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
                  title: const Text('Let AI identify', style: AppTextStyles.cardHeading),
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

  void _nextFromDetails() {
    if (!_detailsFormKey.currentState!.validate()) {
      return;
    }
    setState(() {
      _currentStep = 1;
    });
  }

  void _nextFromLocation() {
    if (!_locationFormKey.currentState!.validate()) {
      return;
    }
    setState(() {
      _currentStep = 2;
    });
  }

  void _backToDetails() {
    setState(() {
      _currentStep = 0;
    });
  }

  void _backToLocation() {
    setState(() {
      _currentStep = 1;
    });
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
          _locationFeedbackMessage = 'GPS location captured';
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

  void _clearGpsLocation() {
    setState(() {
      _latitude = null;
      _longitude = null;
      _locationFeedbackMessage = null;
      _isLocationError = false;
    });
  }

  Future<void> _submit() async {
    final provider = context.read<ServiceRequestProvider>();
    final canonicalHint =
        CanonicalServiceCategory.toCanonicalCategoryHint(_selectedPreference);
    final dto = CreateServiceRequestDto(
      description: _descriptionController.text.trim(),
      locationText: _locationController.text.trim(),
      latitude: _latitude,
      longitude: _longitude,
      categoryHint: canonicalHint,
    );

    final created = await provider.createRequest(dto);

    if (!mounted) return;

    if (created != null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Service request created successfully!'),
          backgroundColor: AppColors.success,
        ),
      );

      // Navigate to detail screen so customer can trigger AI analysis
      Navigator.of(context).pushReplacement(
        MaterialPageRoute(
          builder: (_) => ServiceRequestDetailScreen(
            requestId: created.serviceRequestId,
          ),
        ),
      );
    } else {
      final errorMessage =
          provider.error ?? 'Failed to create request. Please try again.';
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
    return Scaffold(
      appBar: AppBar(
        title: const Text('Create Service Request'),
      ),
      body: SafeArea(
        child: Column(
          children: [
            StepIndicator(currentStep: _currentStep),
            Expanded(
              child: SingleChildScrollView(
                padding: const EdgeInsets.all(AppSpacing.lg),
                child: _buildCurrentStep(context),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildCurrentStep(BuildContext context) {
    switch (_currentStep) {
      case 0:
        return _buildDetailsStep();
      case 1:
        return _buildLocationStep();
      case 2:
        return _buildReviewStep(context);
      default:
        return _buildDetailsStep();
    }
  }

  Widget _buildDetailsStep() {
    final selectedCategory =
        CanonicalServiceCategory.fromCanonicalOrDisplayName(_selectedPreference);

    return Form(
      key: _detailsFormKey,
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
                        selectedCategory?.displayName ?? 'Let AI identify',
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
          const SizedBox(height: AppSpacing.lg),

          const Text(
            'Describe your problem',
            style: AppTextStyles.sectionHeading,
          ),
          const SizedBox(height: AppSpacing.xs),
          Text(
            'AssistLK AI will analyze your description to understand the service requirements and urgency.',
            style: AppTextStyles.body.copyWith(
              color: AppColors.textSecondary,
            ),
          ),
          const SizedBox(height: AppSpacing.lg),

          // Problem Description Field
          AppTextField(
            controller: _descriptionController,
            label: 'Problem Description',
            hint:
                'e.g., Water is leaking heavily from the pipe under my kitchen sink...',
            maxLines: 5,
            validator: (value) {
              if (value == null || value.trim().isEmpty) {
                return 'Please describe the problem you are experiencing.';
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
          const SizedBox(height: AppSpacing.xl),

          AppButton(
            text: 'Next: Location',
            onPressed: _nextFromDetails,
          ),
        ],
      ),
    );
  }

  Widget _buildLocationStep() {
    return Form(
      key: _locationFormKey,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Service Location',
            style: AppTextStyles.sectionHeading,
          ),
          const SizedBox(height: AppSpacing.xs),
          Text(
            'Enter the location or address where service is required.',
            style: AppTextStyles.body.copyWith(
              color: AppColors.textSecondary,
            ),
          ),
          const SizedBox(height: AppSpacing.lg),

          // GPS Location Action Button
          SizedBox(
            width: double.infinity,
            child: OutlinedButton.icon(
              key: const Key('use_current_location_button'),
              onPressed: _isObtainingLocation ? null : _useCurrentLocation,
              icon: _isObtainingLocation
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.my_location_rounded, size: 18),
              label: Text(
                _isObtainingLocation
                    ? 'Acquiring GPS location...'
                    : 'Use Current Location',
              ),
              style: OutlinedButton.styleFrom(
                foregroundColor: AppColors.primary,
                side: const BorderSide(color: AppColors.primary),
                padding: const EdgeInsets.symmetric(
                  horizontal: AppSpacing.md,
                  vertical: AppSpacing.sm + 4,
                ),
              ),
            ),
          ),

          if (_latitude != null && _longitude != null) ...[
            const SizedBox(height: AppSpacing.sm),
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
                    Icons.check_circle_rounded,
                    color: AppColors.success,
                    size: 18,
                  ),
                  const SizedBox(width: AppSpacing.xs),
                  Expanded(
                    child: Text(
                      'GPS location captured (${_latitude!.toStringAsFixed(4)}, ${_longitude!.toStringAsFixed(4)})',
                      style: AppTextStyles.small.copyWith(
                        color: AppColors.success,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                  TextButton(
                    onPressed: _clearGpsLocation,
                    child: const Text(
                      'Remove GPS',
                      style: TextStyle(
                        color: AppColors.error,
                        fontSize: 12,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ] else if (_locationFeedbackMessage != null) ...[
            const SizedBox(height: AppSpacing.sm),
            Container(
              padding: const EdgeInsets.all(AppSpacing.sm),
              decoration: BoxDecoration(
                color: (_isLocationError ? AppColors.warning : AppColors.primary)
                    .withValues(alpha: 0.08),
                borderRadius: BorderRadius.circular(AppRadius.medium),
                border: Border.all(
                  color:
                      (_isLocationError ? AppColors.warning : AppColors.primary)
                          .withValues(alpha: 0.3),
                ),
              ),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(
                    _isLocationError
                        ? Icons.info_outline_rounded
                        : Icons.check_circle_outline_rounded,
                    color:
                        _isLocationError ? AppColors.warning : AppColors.primary,
                    size: 18,
                  ),
                  const SizedBox(width: AppSpacing.xs),
                  Expanded(
                    child: Text(
                      _locationFeedbackMessage!,
                      style: AppTextStyles.small.copyWith(
                        color: AppColors.textPrimary,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],
          const SizedBox(height: AppSpacing.md),

          // Divider
          const Row(
            children: [
              Expanded(child: Divider(color: AppColors.border)),
              Padding(
                padding: EdgeInsets.symmetric(horizontal: AppSpacing.sm),
                child: Text(
                  'or enter address manually',
                  style: AppTextStyles.small,
                ),
              ),
              Expanded(child: Divider(color: AppColors.border)),
            ],
          ),
          const SizedBox(height: AppSpacing.md),

          // Location Text Field
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
          const SizedBox(height: AppSpacing.xl),

          Row(
            children: [
              Expanded(
                child: OutlinedButton(
                  onPressed: _backToDetails,
                  child: const Text('Back'),
                ),
              ),
              const SizedBox(width: AppSpacing.md),
              Expanded(
                child: AppButton(
                  text: 'Next: Review',
                  onPressed: _nextFromLocation,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildReviewStep(BuildContext context) {
    final provider = context.watch<ServiceRequestProvider>();
    final selectedCategory =
        CanonicalServiceCategory.fromCanonicalOrDisplayName(_selectedPreference);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text(
          'Review Service Request',
          style: AppTextStyles.sectionHeading,
        ),
        const SizedBox(height: AppSpacing.xs),
        Text(
          'Verify your request details before submitting.',
          style: AppTextStyles.body.copyWith(
            color: AppColors.textSecondary,
          ),
        ),
        const SizedBox(height: AppSpacing.lg),

        // Summary Card
        AppCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text('Service preference', style: AppTextStyles.small),
              const SizedBox(height: 2),
              Text(
                selectedCategory?.displayName ?? 'Let AI identify',
                style: AppTextStyles.cardHeading,
              ),
              const SizedBox(height: AppSpacing.md),
              const Divider(height: 1, color: AppColors.border),
              const SizedBox(height: AppSpacing.md),
              const Text('Problem Description', style: AppTextStyles.small),
              const SizedBox(height: 2),
              Text(
                _descriptionController.text.trim(),
                style: AppTextStyles.body,
              ),
              const SizedBox(height: AppSpacing.md),
              const Divider(height: 1, color: AppColors.border),
              const SizedBox(height: AppSpacing.md),
              const Text('Location', style: AppTextStyles.small),
              const SizedBox(height: 2),
              Text(
                _locationController.text.trim(),
                style: AppTextStyles.body,
              ),
              if (_latitude != null && _longitude != null) ...[
                const SizedBox(height: AppSpacing.xs),
                Row(
                  children: [
                    const Icon(
                      Icons.my_location_rounded,
                      size: 14,
                      color: AppColors.success,
                    ),
                    const SizedBox(width: AppSpacing.xs),
                    Text(
                      'GPS location captured (${_latitude!.toStringAsFixed(4)}, ${_longitude!.toStringAsFixed(4)})',
                      style: AppTextStyles.small.copyWith(
                        color: AppColors.success,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ],
                ),
              ],
            ],
          ),
        ),
        const SizedBox(height: AppSpacing.md),

        // AI Confirmation Disclosure Banner
        Container(
          width: double.infinity,
          padding: const EdgeInsets.all(AppSpacing.md),
          decoration: BoxDecoration(
            color: AppColors.primary.withValues(alpha: 0.05),
            borderRadius: BorderRadius.circular(AppRadius.medium),
            border: Border.all(
              color: AppColors.primary.withValues(alpha: 0.2),
            ),
          ),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Icon(
                Icons.auto_awesome_rounded,
                color: AppColors.primary,
                size: 20,
              ),
              const SizedBox(width: AppSpacing.sm),
              Expanded(
                child: Text(
                  'AssistLK AI will analyze your description and confirm the appropriate service category and urgency.',
                  style: AppTextStyles.body.copyWith(
                    color: AppColors.primaryDark,
                    fontSize: 13,
                  ),
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: AppSpacing.xl),

        Row(
          children: [
            Expanded(
              child: OutlinedButton(
                onPressed: provider.isLoading ? null : _backToLocation,
                child: const Text('Back'),
              ),
            ),
            const SizedBox(width: AppSpacing.md),
            Expanded(
              child: AppButton(
                text: 'Submit Request',
                isLoading: provider.isLoading,
                onPressed: _submit,
              ),
            ),
          ],
        ),
      ],
    );
  }
}
