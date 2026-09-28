class AppConfig {
  const AppConfig._();

  static const _apiBaseUrlOverride = String.fromEnvironment('API_BASE_URL');

  static String get apiBaseUrl {
    return _apiBaseUrlOverride.isNotEmpty
        ? _apiBaseUrlOverride
        : 'http://localhost:5012/api';
  }
}
