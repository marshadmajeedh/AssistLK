import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'app/app.dart';
import 'core/api/api_client.dart';
import 'core/auth/token_storage.dart';
import 'features/auth/providers/auth_provider.dart';
import 'features/auth/services/auth_service.dart';
import 'features/service_requests/services/problem_image_picker.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  final tokenStorage = TokenStorage();

  final apiClient = ApiClient(tokenStorage: tokenStorage);

  final authService = AuthService(apiClient: apiClient);

  final authProvider = AuthProvider(
    authService: authService,
    tokenStorage: tokenStorage,
  );

  final photoPicker = NativeProblemImagePicker();
  var session = authProvider.sessionGeneration;
  authProvider.addListener(() {
    if (session == authProvider.sessionGeneration) return;
    final previousSession = session;
    session = authProvider.sessionGeneration;
    if (previousSession != 0 || authProvider.user == null) {
      photoPicker.clearRecovery().catchError((Object _) {});
    }
  });
  final initialization = authProvider.initialize();

  runApp(
    MultiProvider(
      providers: [
        ChangeNotifierProvider<AuthProvider>.value(value: authProvider),
        Provider<ProblemImagePicker>.value(value: photoPicker),
      ],
      child: const AssistLKApp(),
    ),
  );

  await initialization;
  await photoPicker.prepareRecovery(
    authProvider.user?.role == 'Customer' ? authProvider.user?.userId : null,
  );
}
