import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../shared/theme/app_spacing.dart';
import '../../../shared/theme/app_text_styles.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_card.dart';
import '../providers/auth_provider.dart';

class CustomerAccountScreen extends StatelessWidget {
  const CustomerAccountScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    final user = auth.user;
    return ListView(
      key: const PageStorageKey('customer-account'),
      primary: false,
      padding: const EdgeInsets.all(AppSpacing.lg),
      children: [
        AppCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(user?.fullName ?? '', style: AppTextStyles.sectionHeading),
              const SizedBox(height: AppSpacing.sm),
              Text(user?.email ?? '', style: AppTextStyles.body),
              const SizedBox(height: AppSpacing.sm),
              Text('Role: ${user?.role ?? ''}', style: AppTextStyles.body),
              if (user?.phoneNumber?.trim().isNotEmpty ?? false) ...[
                const SizedBox(height: AppSpacing.sm),
                Text('Phone: ${user!.phoneNumber}', style: AppTextStyles.body),
              ],
            ],
          ),
        ),
        const SizedBox(height: AppSpacing.lg),
        AppButton(text: 'Logout', onPressed: auth.logout),
      ],
    );
  }
}
