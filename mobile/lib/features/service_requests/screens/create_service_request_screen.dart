import '../models/location_source.dart';
import '../../customer/models/location_suggestion.dart';
import '../widgets/location_attribution.dart';
import '../providers/location_selection_controller.dart';
import '../services/location_geocoding_service.dart';
import '../widgets/location_selection.dart';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../../shared/theme/app_assets.dart';
import '../../../../shared/theme/app_colors.dart';
import '../../../../shared/theme/app_radius.dart';
import '../../../../shared/theme/app_spacing.dart';
import '../../../../shared/theme/app_text_styles.dart';
import '../../../../shared/widgets/app_button.dart';
import '../../../../shared/widgets/app_card.dart';
import '../../../../shared/widgets/app_image_asset.dart';
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
  final LocationGeocodingService? geocodingService;
  final LocationSuggestion? initialLocationSuggestion;

  const CreateServiceRequestScreen({
    super.key,
    this.initialCategoryPreference,
    this.locationService,
    this.geocodingService,
    this.initialLocationSuggestion,
  });

  @override
  State<CreateServiceRequestScreen> createState() =>
      _CreateServiceRequestScreenState();
}

class _CreateServiceRequestScreenState
    extends State<CreateServiceRequestScreen> {
  int _currentStep = 0;
  String? _selectedPreference;
  late final LocationSelectionController _location;

  final _detailsFormKey = GlobalKey<FormState>();
  final _locationFormKey = GlobalKey<FormState>();
  final _descriptionController = TextEditingController();

  @override
  void initState() {
    super.initState();
    _selectedPreference = widget.initialCategoryPreference;
    _location = LocationSelectionController(
      initialSuggestion: widget.initialLocationSuggestion,
      gps: widget.locationService ?? GeolocatorLocationService(),
      geocoding:
          widget.geocodingService ??
          LocationGeocodingService(
            apiClient: context
                .read<ServiceRequestProvider>()
                .serviceRequestService
                .apiClient,
          ),
    );
  }

  @override
  void dispose() {
    _descriptionController.dispose();
    _location.dispose();
    super.dispose();
  }

  String _getCategoryAssetPath(CanonicalServiceCategory category) {
    switch (category.canonicalName) {
      case 'Plumbing':
        return AppAssets.plumbingService;
      case 'Electrical':
        return AppAssets.electricalService;
      case 'Vehicle Repair':
        return AppAssets.vehicleService;
      case 'Appliance Repair':
        return AppAssets.applianceService;
      default:
        return AppAssets.plumbingService;
    }
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
                for (final cat
                    in CanonicalServiceCategory.canonicalShortcuts) ...[
                  ListTile(
                    leading: Container(
                      width: 40,
                      height: 40,
                      padding: const EdgeInsets.all(AppSpacing.xs),
                      decoration: BoxDecoration(
                        color: AppColors.primarySurface,
                        borderRadius: BorderRadius.circular(AppRadius.medium),
                      ),
                      child: Center(
                        child: AppImageAsset(
                          assetPath: _getCategoryAssetPath(cat),
                          width: 28,
                          height: 28,
                          fit: BoxFit.contain,
                          fallbackIcon: cat.icon,
                          semanticLabel: cat.displayName,
                        ),
                      ),
                    ),
                    title: Text(
                      cat.displayName,
                      style: AppTextStyles.cardHeading,
                    ),
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
                  leading: Container(
                    width: 40,
                    height: 40,
                    padding: const EdgeInsets.all(AppSpacing.xs),
                    decoration: BoxDecoration(
                      color: AppColors.aiSurface,
                      borderRadius: BorderRadius.circular(AppRadius.medium),
                    ),
                    child: const Center(
                      child: AppImageAsset(
                        assetPath: AppAssets.aiDiagnosisSpark,
                        width: 28,
                        height: 28,
                        fit: BoxFit.contain,
                        fallbackIcon: Icons.auto_awesome_rounded,
                        semanticLabel: 'AssistLK AI Problem Understanding',
                      ),
                    ),
                  ),
                  title: const Text(
                    'Let AssistLK AI identify',
                    style: AppTextStyles.cardHeading,
                  ),
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
    _location.cancelPending();
    setState(() {
      _currentStep = 0;
    });
  }

  void _backToLocation() {
    setState(() {
      _currentStep = 1;
    });
  }

  Future<void> _submit() async {
    if (_location.validate() != null) return;
    final provider = context.read<ServiceRequestProvider>();
    final canonicalHint = CanonicalServiceCategory.toCanonicalCategoryHint(
      _selectedPreference,
    );
    final dto = CreateServiceRequestDto(
      description: _descriptionController.text.trim(),
      locationText: _location.text.text.trim(),
      locationSource: _location.source,
      latitude: _location.latitude,
      longitude: _location.longitude,
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
          builder: (_) =>
              ServiceRequestDetailScreen(requestId: created.serviceRequestId),
        ),
      );
    } else {
      final errorMessage =
          provider.error ?? 'Failed to create request. Please try again.';
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(errorMessage), backgroundColor: AppColors.error),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Create Service Request')),
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
            Padding(
              key: const Key('wizard_actions'),
              padding: const EdgeInsets.fromLTRB(
                AppSpacing.lg,
                AppSpacing.sm,
                AppSpacing.lg,
                AppSpacing.md,
              ),
              child: _buildWizardActions(context),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildWizardActions(BuildContext context) {
    final provider = context.watch<ServiceRequestProvider>();
    if (_currentStep == 0) {
      return AppButton(text: 'Next: Location', onPressed: _nextFromDetails);
    }
    final back = OutlinedButton(
      onPressed: _currentStep == 1
          ? _backToDetails
          : (provider.isLoading ? null : _backToLocation),
      child: const Text('Back'),
    );
    final next = AppButton(
      text: _currentStep == 1 ? 'Next: Review' : 'Submit Request',
      isLoading: _currentStep == 2 && provider.isLoading,
      onPressed: _currentStep == 1 ? _nextFromLocation : _submit,
    );
    return LayoutBuilder(
      builder: (context, constraints) {
        final scale = MediaQuery.textScalerOf(context).scale(14) / 14;
        if (constraints.maxWidth < 280 * scale) {
          return Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              next,
              const SizedBox(height: AppSpacing.sm),
              back,
            ],
          );
        }
        return Row(
          children: [
            Expanded(child: back),
            const SizedBox(width: AppSpacing.md),
            Expanded(child: next),
          ],
        );
      },
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
        CanonicalServiceCategory.fromCanonicalOrDisplayName(
          _selectedPreference,
        );

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
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Container(
                      width: 44,
                      height: 44,
                      padding: const EdgeInsets.all(AppSpacing.xs),
                      decoration: BoxDecoration(
                        color: selectedCategory != null
                            ? AppColors.primarySurface
                            : AppColors.aiSurface,
                        borderRadius: BorderRadius.circular(AppRadius.medium),
                      ),
                      child: Center(
                        child: AppImageAsset(
                          assetPath: selectedCategory != null
                              ? _getCategoryAssetPath(selectedCategory)
                              : AppAssets.aiDiagnosisSpark,
                          width: 32,
                          height: 32,
                          fit: BoxFit.contain,
                          fallbackIcon:
                              selectedCategory?.icon ??
                              Icons.auto_awesome_rounded,
                          semanticLabel:
                              selectedCategory?.displayName ??
                              'AssistLK AI Problem Understanding',
                        ),
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
                            selectedCategory?.displayName ??
                                'Let AssistLK AI identify',
                            style: AppTextStyles.cardHeading,
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: AppSpacing.sm),
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
            style: AppTextStyles.body.copyWith(color: AppColors.textSecondary),
          ),
          const SizedBox(height: AppSpacing.lg),

          // Problem Description Field
          AppTextField(
            controller: _descriptionController,
            label: 'Problem Description',
            hint: 'e.g., Water is leaking heavily from the pipe under my kitchen sink...',
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
        ],
      ),
    );
  }

  Widget _buildLocationStep() => Form(
    key: _locationFormKey,
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [LocationSelection(controller: _location)],
    ),
  );

  Widget _buildReviewStep(BuildContext context) {
    final selectedCategory =
        CanonicalServiceCategory.fromCanonicalOrDisplayName(
          _selectedPreference,
        );

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
          style: AppTextStyles.body.copyWith(color: AppColors.textSecondary),
        ),
        const SizedBox(height: AppSpacing.lg),

        // Summary Card
        AppCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Container(
                    width: 36,
                    height: 36,
                    padding: const EdgeInsets.all(AppSpacing.xs),
                    decoration: BoxDecoration(
                      color: selectedCategory != null
                          ? AppColors.primarySurface
                          : AppColors.aiSurface,
                      borderRadius: BorderRadius.circular(AppRadius.medium),
                    ),
                    child: Center(
                      child: AppImageAsset(
                        assetPath: selectedCategory != null
                            ? _getCategoryAssetPath(selectedCategory)
                            : AppAssets.aiDiagnosisSpark,
                        width: 24,
                        height: 24,
                        fit: BoxFit.contain,
                        fallbackIcon:
                            selectedCategory?.icon ??
                            Icons.auto_awesome_rounded,
                        semanticLabel:
                            selectedCategory?.displayName ??
                            'AssistLK AI Problem Understanding',
                      ),
                    ),
                  ),
                  const SizedBox(width: AppSpacing.sm),
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
                          selectedCategory?.displayName ??
                              'Let AssistLK AI identify',
                          style: AppTextStyles.cardHeading,
                        ),
                      ],
                    ),
                  ),
                ],
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
              Text(_location.text.text.trim(), style: AppTextStyles.body),
              if (_location.source == LocationSource.openStreetMap)
                const LocationAttribution(),
              if (_location.latitude != null &&
                  _location.longitude != null) ...[
                const SizedBox(height: AppSpacing.xs),
                Row(
                  children: [
                    const Icon(
                      Icons.my_location_rounded,
                      size: 14,
                      color: AppColors.success,
                    ),
                    const SizedBox(width: AppSpacing.xs),
                    Flexible(
                      child: Text(
                        'GPS location captured',
                        style: AppTextStyles.small.copyWith(
                          color: AppColors.success,
                          fontWeight: FontWeight.w600,
                        ),
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
            border: Border.all(color: AppColors.primary.withValues(alpha: 0.2)),
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
      ],
    );
  }
}
