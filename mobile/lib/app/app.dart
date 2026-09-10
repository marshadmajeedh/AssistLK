import 'package:flutter/material.dart';

import '../features/auth/screens/auth_gate.dart';
import '../shared/theme/app_theme.dart';

class AssistLKApp extends StatelessWidget {
  const AssistLKApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'AssistLK',

      debugShowCheckedModeBanner: false,

      theme: AppTheme.lightTheme,

      home: const AuthGate(),
    );
  }
}
