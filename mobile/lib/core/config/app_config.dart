class AppConfig {
  const AppConfig._();

  static const _apiBaseUrlOverride = String.fromEnvironment('API_BASE_URL');

  static String get apiBaseUrl {
    return _apiBaseUrlOverride.isNotEmpty
        ? _apiBaseUrlOverride
        : 'https://assistlk-backend-production.up.railway.app/api';
  }
}
