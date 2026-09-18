import 'package:dio/dio.dart';

import '../auth/token_storage.dart';
import '../config/app_config.dart';

class ApiClient {
  ApiClient({Dio? dio, TokenStorage? tokenStorage})
    : _dio = dio ?? Dio(),
      _tokenStorage = tokenStorage ?? TokenStorage() {
    _dio.options.baseUrl = AppConfig.apiBaseUrl;
    _dio.options.connectTimeout = const Duration(seconds: 15);
    _dio.options.receiveTimeout = const Duration(seconds: 15);
    _dio.options.headers['Content-Type'] = 'application/json';
    _dio.interceptors.add(
      InterceptorsWrapper(
        onRequest: (options, handler) async {
          final generation = _sessionGeneration;
          final tokenGeneration = _tokenStorage.generation;
          final token = await _tokenStorage.getToken();
          if (generation != _sessionGeneration ||
              tokenGeneration != _tokenStorage.generation ||
              options.cancelToken?.isCancelled == true) {
            handler.reject(
              DioException(
                requestOptions: options,
                type: DioExceptionType.cancel,
                message: 'Session changed before the request was sent.',
              ),
            );
            return;
          }
          options.extra['sessionGeneration'] = generation;
          options.extra['tokenGeneration'] = tokenGeneration;
          if (token != null && token.isNotEmpty) {
            options.headers['Authorization'] = 'Bearer $token';
          }
          handler.next(options);
        },
        onError: (error, handler) async {
          if (error.response?.statusCode == 401 &&
              error.requestOptions.headers['Authorization'] != null &&
              error.requestOptions.extra['tokenGeneration'] ==
                  _tokenStorage.generation &&
              error.requestOptions.extra['sessionGeneration'] ==
                  _sessionGeneration) {
            invalidateSession();
            try {
              if (onSessionExpired != null) {
                await onSessionExpired!();
              } else {
                await _tokenStorage.deleteIfCurrent(
                  (error.requestOptions.headers['Authorization'] as String)
                      .replaceFirst('Bearer ', ''),
                  error.requestOptions.extra['tokenGeneration'] as int,
                );
              }
            } catch (_) {
              // Preserve the original HTTP error even if secure storage fails.
            }
          }
          handler.next(error);
        },
      ),
    );
  }

  final Dio _dio;
  final TokenStorage _tokenStorage;
  Future<void> Function()? onSessionExpired;
  int _sessionGeneration = 0;

  void invalidateSession() => _sessionGeneration++;

  Dio get client => _dio;
}
