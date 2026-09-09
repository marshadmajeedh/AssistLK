import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../../shared/theme/app_colors.dart';
import '../../../../shared/theme/app_spacing.dart';
import '../../../../shared/theme/app_text_styles.dart';
import '../../../../shared/widgets/app_button.dart';
import '../../../../shared/widgets/app_text_field.dart';
import '../models/create_service_request_dto.dart';
import '../providers/service_request_provider.dart';
import 'service_request_detail_screen.dart';

class CreateServiceRequestScreen extends StatefulWidget {
  const CreateServiceRequestScreen({super.key});

  @override
  State<CreateServiceRequestScreen> createState() =>
      _CreateServiceRequestScreenState();
}

class _CreateServiceRequestScreenState
    extends State<CreateServiceRequestScreen> {
  final _formKey = GlobalKey<FormState>();
  final _descriptionController = TextEditingController();
  final _locationController = TextEditingController();

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
    final dto = CreateServiceRequestDto(
      description: _descriptionController.text.trim(),
      locationText: _locationController.text.trim(),
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
    final provider = context.watch<ServiceRequestProvider>();

    return Scaffold(
      appBar: AppBar(
        title: const Text('Create Service Request'),
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
                  'Describe your problem',
                  style: AppTextStyles.sectionHeading,
                ),
                const SizedBox(height: AppSpacing.xs),
                Text(
                  'AssistLK AI will analyze your description to understand the category, urgency, and requirements.',
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
                      'e.g., The circuit breaker trips every time the AC is turned on...',
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

                // Submit Button
                AppButton(
                  text: 'Submit Request',
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
