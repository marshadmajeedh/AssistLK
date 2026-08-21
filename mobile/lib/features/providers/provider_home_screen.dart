import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../auth/providers/auth_provider.dart';
import '../../shared/theme/app_spacing.dart';
import '../../shared/theme/app_text_styles.dart';
import '../../shared/widgets/app_button.dart';

class ProviderHomeScreen extends StatelessWidget {
  const ProviderHomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();

    return Scaffold(
      appBar: AppBar(title: const Text('AssistLK Provider')),
      body: Padding(
        padding: const EdgeInsets.all(AppSpacing.lg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Welcome ${auth.user?.fullName ?? ''}',
              style: AppTextStyles.sectionHeading,
            ),
            const SizedBox(height: AppSpacing.sm),
            Text(auth.user?.email ?? '', style: AppTextStyles.body),
            const SizedBox(height: AppSpacing.xl),
            const Text(
              'Provider Home - Component 2',
              style: AppTextStyles.body,
            ),
            const SizedBox(height: AppSpacing.xl),
            AppButton(
              text: 'Logout',
              onPressed: () async {
                await context.read<AuthProvider>().logout();
              },
            ),
          ],
        ),
      ),
    );
  }
}
