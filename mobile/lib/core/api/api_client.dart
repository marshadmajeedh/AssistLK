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
          final generation = _tokenStorage.generation;
          final token = await _tokenStorage.getToken();
          if (generation != _tokenStorage.generation ||
              options.cancelToken?.isCancelled == true) {
            handler.reject(
              DioException(
                requestOptions: options,
                type: DioExceptionType.cancel,
              ),
            );
            return;
          }
          options.extra['sessionGeneration'] = generation;
          if (token != null && token.isNotEmpty) {
            options.headers['Authorization'] = 'Bearer $token';
          }
          handler.next(options);
        },
        onError: (error, handler) async {
          if (error.response?.statusCode == 401) {
            final authorization =
                error.requestOptions.headers['Authorization'] as String?;
            final generation =
                error.requestOptions.extra['sessionGeneration'] as int?;
            if (authorization != null &&
                generation != null &&
                await _tokenStorage.deleteIfCurrent(
                  authorization.replaceFirst('Bearer ', ''),
                  generation,
                )) {
              onUnauthorized?.call();
            }
          }
          handler.next(error);
        },
      ),
    );
  }

  final Dio _dio;
  final TokenStorage _tokenStorage;
  void Function()? onUnauthorized;

  Dio get client => _dio;
}
