import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'app/app.dart';
import 'core/api/api_client.dart';
import 'core/auth/token_storage.dart';
import 'features/auth/providers/auth_provider.dart';
import 'features/auth/services/auth_service.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  final tokenStorage = TokenStorage();

  final apiClient = ApiClient(tokenStorage: tokenStorage);

  final authService = AuthService(apiClient: apiClient);

  final authProvider = AuthProvider(
    authService: authService,
    tokenStorage: tokenStorage,
  );

  final initialization = authProvider.initialize();

  runApp(
    ChangeNotifierProvider<AuthProvider>.value(
      value: authProvider,
      child: const AssistLKApp(),
    ),
  );

  await initialization;
}
