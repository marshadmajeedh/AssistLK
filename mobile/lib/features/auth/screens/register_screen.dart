import 'package:flutter/material.dart';

import 'customer_register_screen.dart';

export 'customer_register_screen.dart';

/// Backward-compatible wrapper for customer registration.
///
/// In AssistLK Component 1 UX, registration is separated by account type.
/// The customer registration implementation is maintained in [CustomerRegisterScreen].
class RegisterScreen extends StatelessWidget {
  const RegisterScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return const CustomerRegisterScreen();
  }
}
