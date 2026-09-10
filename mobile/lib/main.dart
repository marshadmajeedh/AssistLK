import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'app/app.dart';
import 'core/api/api_client.dart';
import 'core/auth/token_storage.dart';
import 'features/auth/providers/auth_provider.dart';
import 'features/auth/services/auth_service.dart';
import 'features/service_requests/providers/service_request_provider.dart';
import 'features/service_requests/services/service_request_service.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  final tokenStorage = TokenStorage();

  final apiClient = ApiClient(tokenStorage: tokenStorage);

  final authService = AuthService(apiClient: apiClient);

  final authProvider = AuthProvider(
    authService: authService,
    tokenStorage: tokenStorage,
  );

  final serviceRequestService = ServiceRequestService(apiClient: apiClient);

  runApp(
    MultiProvider(
      providers: [
        ChangeNotifierProvider<AuthProvider>.value(
          value: authProvider,
        ),
        ChangeNotifierProvider<ServiceRequestProvider>(
          create: (_) => ServiceRequestProvider(
            serviceRequestService: serviceRequestService,
          ),
        ),
      ],
      child: const AssistLKApp(),
    ),
  );

  await authProvider.initialize();
}

