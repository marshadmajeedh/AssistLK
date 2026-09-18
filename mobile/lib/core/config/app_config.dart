import 'package:flutter/foundation.dart';

class AppConfig {
  const AppConfig._();

  static const _apiBaseUrlOverride = String.fromEnvironment('API_BASE_URL');
  static const _agentBaseUrlOverride = String.fromEnvironment('AGENT_BASE_URL');

  static String get apiBaseUrl {
    if (_apiBaseUrlOverride.isNotEmpty) {
      return _apiBaseUrlOverride.replaceFirst(RegExp(r'\/$'), '');
    }

    return kIsWeb ? 'http://localhost:5012/api' : 'http://10.0.2.2:5012/api';
  }

  static String get agentBaseUrl {
    if (_agentBaseUrlOverride.isNotEmpty) {
      return _agentBaseUrlOverride.replaceFirst(RegExp(r'\/$'), '');
    }

    return kIsWeb ? 'http://localhost:8000' : 'http://10.0.2.2:8000';
  }
}
