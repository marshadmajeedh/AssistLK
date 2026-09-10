import 'package:flutter/foundation.dart';

class AppConfig {
  const AppConfig._();

  static const _apiBaseUrlOverride = String.fromEnvironment('API_BASE_URL');

  static String get apiBaseUrl {
    if (_apiBaseUrlOverride.isNotEmpty) {
      return _apiBaseUrlOverride;
    }

    return kIsWeb ? 'http://localhost:5012/api' : 'http://10.0.2.2:5012/api';
  }
}
