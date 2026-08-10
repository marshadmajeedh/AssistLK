import 'package:flutter/material.dart';

import 'shared/theme/app_theme.dart';

void main() {
  runApp(const AssistLKApp());
}

class AssistLKApp extends StatelessWidget {
  const AssistLKApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'AssistLK',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.lightTheme,
      home: const Scaffold(
        body: Center(
          child: Text('Home screen placeholder'),
        ),
      ),
    );
  }
}
