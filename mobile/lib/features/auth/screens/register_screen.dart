import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../shared/theme/app_colors.dart';
import '../../../shared/theme/app_spacing.dart';
import '../../../shared/theme/app_text_styles.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_text_field.dart';
import '../providers/auth_provider.dart';

class RegisterScreen extends StatefulWidget {
  const RegisterScreen({super.key});

  @override
  State<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends State<RegisterScreen> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _emailController = TextEditingController();
  final _phoneController = TextEditingController();
  final _passwordController = TextEditingController();
  final _confirmPasswordController = TextEditingController();

  String _role = 'Customer';

  @override
  void dispose() {
    _nameController.dispose();
    _emailController.dispose();
    _phoneController.dispose();
    _passwordController.dispose();
    _confirmPasswordController.dispose();
    super.dispose();
  }

  Future<void> _register() async {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    final success = await context.read<AuthProvider>().register(
      fullName: _nameController.text,
      email: _emailController.text,
      password: _passwordController.text,
      phoneNumber: _phoneController.text,
      role: _role,
    );

    if (success && mounted) {
      Navigator.of(context).pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();

    return Scaffold(
      appBar: AppBar(title: const Text('Create Account')),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(AppSpacing.lg),
          child: Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const Text('Join AssistLK', style: AppTextStyles.pageTitle),
                const SizedBox(height: AppSpacing.lg),
                AppTextField(
                  controller: _nameController,
                  label: 'Full Name',
                  validator: (value) {
                    if (value == null || value.trim().isEmpty) {
                      return 'Full name is required.';
                    }

                    return null;
                  },
                ),
                const SizedBox(height: AppSpacing.md),
                AppTextField(
                  controller: _emailController,
                  label: 'Email',
                  keyboardType: TextInputType.emailAddress,
                  validator: (value) {
                    if (value == null || value.trim().isEmpty) {
                      return 'Email is required.';
                    }

                    if (!value.contains('@')) {
                      return 'Enter a valid email.';
                    }

                    return null;
                  },
                ),
                const SizedBox(height: AppSpacing.md),
                AppTextField(
                  controller: _phoneController,
                  label: 'Phone Number',
                  keyboardType: TextInputType.phone,
                ),
                const SizedBox(height: AppSpacing.md),
                DropdownButtonFormField<String>(
                  initialValue: _role,
                  decoration: const InputDecoration(labelText: 'Account Type'),
                  items: const [
                    DropdownMenuItem(
                      value: 'Customer',
                      child: Text('Customer'),
                    ),
                    DropdownMenuItem(
                      value: 'Provider',
                      child: Text('Service Provider'),
                    ),
                  ],
                  onChanged: (value) {
                    if (value != null) {
                      setState(() {
                        _role = value;
                      });
                    }
                  },
                ),
                const SizedBox(height: AppSpacing.md),
                AppTextField(
                  controller: _passwordController,
                  label: 'Password',
                  obscureText: true,
                  validator: (value) {
                    if (value == null || value.length < 8) {
                      return 'Password must contain at least 8 characters.';
                    }

                    return null;
                  },
                ),
                const SizedBox(height: AppSpacing.md),
                AppTextField(
                  controller: _confirmPasswordController,
                  label: 'Confirm Password',
                  obscureText: true,
                  validator: (value) {
                    if (value != _passwordController.text) {
                      return 'Passwords do not match.';
                    }

                    return null;
                  },
                ),
                if (auth.error != null) ...[
                  const SizedBox(height: AppSpacing.md),
                  Text(
                    auth.error!,
                    style: const TextStyle(color: AppColors.error),
                  ),
                ],
                const SizedBox(height: AppSpacing.lg),
                AppButton(
                  text: 'Create Account',
                  isLoading: auth.isLoading,
                  onPressed: auth.isLoading ? null : _register,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
