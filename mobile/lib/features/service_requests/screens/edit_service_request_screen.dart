import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../../shared/theme/app_colors.dart';
import '../../../../shared/theme/app_spacing.dart';
import '../../../../shared/theme/app_text_styles.dart';
import '../../../../shared/widgets/app_button.dart';
import '../../../../shared/widgets/app_text_field.dart';
import '../models/service_request_model.dart';
import '../models/update_service_request_dto.dart';
import '../providers/service_request_provider.dart';

class EditServiceRequestScreen extends StatefulWidget {
  final ServiceRequestModel request;

  const EditServiceRequestScreen({
    super.key,
    required this.request,
  });

  @override
  State<EditServiceRequestScreen> createState() =>
      _EditServiceRequestScreenState();
}

class _EditServiceRequestScreenState extends State<EditServiceRequestScreen> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _descriptionController;
  late final TextEditingController _locationController;

  @override
  void initState() {
    super.initState();
    _descriptionController =
        TextEditingController(text: widget.request.description);
    _locationController =
        TextEditingController(text: widget.request.locationText);
  }

  @override
  void dispose() {
    _descriptionController.dispose();
    _locationController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    final provider = context.read<ServiceRequestProvider>();
    final dto = UpdateServiceRequestDto(
      description: _descriptionController.text.trim(),
      locationText: _locationController.text.trim(),
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
                const Text(
                  'Update problem description',
                  style: AppTextStyles.sectionHeading,
                ),
                const SizedBox(height: AppSpacing.xs),
                Text(
                  'Provide more clarity or details to resolve follow-up questions from the AI diagnosis.',
                  style: AppTextStyles.body.copyWith(
                    color: AppColors.textSecondary,
                  ),
                ),
                const SizedBox(height: AppSpacing.lg),

                // Description Field
                AppTextField(
                  controller: _descriptionController,
                  label: 'Problem Description',
                  hint: 'Describe the issue in detail...',
                  maxLines: 5,
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
                const SizedBox(height: AppSpacing.xl),

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
