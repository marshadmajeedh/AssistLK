import 'package:flutter/material.dart';

import '../../../shared/theme/app_assets.dart';
import '../../../shared/theme/app_colors.dart';
import '../../../shared/theme/app_radius.dart';
import '../../../shared/theme/app_spacing.dart';
import '../../../shared/theme/app_text_styles.dart';
import '../../../shared/widgets/app_image_asset.dart';
import 'customer_register_screen.dart';

class AccountTypeSelectionScreen extends StatelessWidget {
  const AccountTypeSelectionScreen({super.key});

  void _onProviderSelected(BuildContext context) {
    // Check if a dedicated provider registration screen exists in the codebase.
    // As of Component 1, provider registration is owned by Component 2 and not yet present.
    // Present a polished, neutral notice without exposing internal terminology.
    showModalBottomSheet<void>(
      context: context,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(
          top: Radius.circular(AppRadius.large),
        ),
      ),
      builder: (BuildContext sheetContext) {
        return SafeArea(
          child: Padding(
            padding: const EdgeInsets.all(AppSpacing.lg),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Row(
                  children: [
                    Container(
                      padding: const EdgeInsets.all(AppSpacing.sm),
                      decoration: BoxDecoration(
                        color: AppColors.secondarySurface,
                        borderRadius: BorderRadius.circular(AppRadius.medium),
                      ),
                      child: const Icon(
                        Icons.handyman_outlined,
                        color: AppColors.secondary,
                        size: 24,
                      ),
                    ),
                    const SizedBox(width: AppSpacing.md),
                    const Expanded(
                      child: Text(
                        'Provider Registration',
                        style: AppTextStyles.cardHeading,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: AppSpacing.md),
                const Text(
                  'Provider registration is being prepared.\n\nProvider onboarding will be available soon.',
                  style: AppTextStyles.body,
                ),
                const SizedBox(height: AppSpacing.lg),
                ElevatedButton(
                  onPressed: () => Navigator.of(sheetContext).pop(),
                  child: const Text('Understood'),
                ),
              ],
            ),
          ),
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Join AssistLK'),
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          onPressed: () => Navigator.of(context).pop(),
        ),
      ),
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.symmetric(
              horizontal: AppSpacing.lg,
              vertical: AppSpacing.md,
            ),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 460),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  // Illustration
                  Center(
                    child: AppImageAsset(
                      assetPath: AppAssets.authRegister,
                      height: 140,
                      fit: BoxFit.contain,
                      semanticLabel: 'Account Registration Selection',
                      fallbackIcon: Icons.how_to_reg_outlined,
                    ),
                  ),
                  const SizedBox(height: AppSpacing.md),

                  // Header
                  const Text(
                    'Join AssistLK',
                    style: AppTextStyles.pageTitle,
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: AppSpacing.xs),
                  const Text(
                    'Choose how you would like to get started',
                    style: TextStyle(
                      fontSize: 14,
                      color: AppColors.textSecondary,
                    ),
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: AppSpacing.xl),

                  // Choice 1: Register as Customer
                  _AccountTypeCard(
                    title: 'Register as Customer',
                    supportingText:
                        'Request home, vehicle, electrical and appliance services.',
                    icon: Icons.person_outline_rounded,
                    accentColor: AppColors.primary,
                    surfaceColor: AppColors.primarySurface,
                    onTap: () {
                      Navigator.of(context).push(
                        MaterialPageRoute(
                          builder: (_) => const CustomerRegisterScreen(),
                        ),
                      );
                    },
                  ),
                  const SizedBox(height: AppSpacing.md),

                  // Choice 2: Register as Provider
                  _AccountTypeCard(
                    title: 'Register as Provider',
                    supportingText:
                        'Offer professional services through AssistLK.',
                    icon: Icons.handyman_outlined,
                    accentColor: AppColors.secondary,
                    surfaceColor: AppColors.secondarySurface,
                    onTap: () => _onProviderSelected(context),
                  ),
                  const SizedBox(height: AppSpacing.xl),

                  // Return to login
                  Wrap(
                    alignment: WrapAlignment.center,
                    crossAxisAlignment: WrapCrossAlignment.center,
                    children: [
                      const Text(
                        'Already have an account?',
                        style: TextStyle(
                          fontSize: 14,
                          color: AppColors.textSecondary,
                        ),
                      ),
                      const SizedBox(width: AppSpacing.xs),
                      TextButton(
                        onPressed: () => Navigator.of(context).pop(),
                        child: const Text('Sign In'),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _AccountTypeCard extends StatelessWidget {
  final String title;
  final String supportingText;
  final IconData icon;
  final Color accentColor;
  final Color surfaceColor;
  final VoidCallback onTap;

  const _AccountTypeCard({
    required this.title,
    required this.supportingText,
    required this.icon,
    required this.accentColor,
    required this.surfaceColor,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Card(
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.large),
        side: const BorderSide(color: AppColors.border),
      ),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(AppRadius.large),
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Row(
            children: [
              Container(
                width: 48,
                height: 48,
                decoration: BoxDecoration(
                  color: surfaceColor,
                  borderRadius: BorderRadius.circular(AppRadius.medium),
                ),
                child: Icon(icon, color: accentColor, size: 26),
              ),
              const SizedBox(width: AppSpacing.md),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(title, style: AppTextStyles.cardHeading),
                    const SizedBox(height: AppSpacing.xs),
                    Text(
                      supportingText,
                      style: AppTextStyles.small,
                    ),
                  ],
                ),
              ),
              const SizedBox(width: AppSpacing.sm),
              const Icon(
                Icons.chevron_right_rounded,
                color: AppColors.disabled,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
