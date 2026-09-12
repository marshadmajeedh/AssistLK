import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../providers/provider_home_screen.dart';
import '../../service_requests/screens/customer_home_screen.dart';
import '../providers/auth_provider.dart';
import 'login_screen.dart';

class AuthGate extends StatelessWidget {
  const AuthGate({super.key});

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    final user = auth.user;
    if (auth.isInitializing) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }

    if (user == null) {
      return const LoginScreen();
    }

    switch (user.role) {
      case 'Customer':
        return const CustomerHomeScreen();
      case 'Provider':
        return const ProviderHomeScreen();
      default:
        return const LoginScreen();
    }
  }
}
