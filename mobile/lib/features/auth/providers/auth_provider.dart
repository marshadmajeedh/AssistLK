import 'package:flutter/foundation.dart';

import '../../../core/auth/token_storage.dart';
import '../models/auth_user.dart';
import '../services/auth_service.dart';

class AuthProvider extends ChangeNotifier {
  final AuthService authService;
  final TokenStorage tokenStorage;

  AuthProvider({required this.authService, required this.tokenStorage}) {
    authService.apiClient.onSessionExpired = logout;
  }

  AuthUser? _user;
  int _sessionGeneration = 0;
  int get sessionGeneration => _sessionGeneration;

  bool _isLoading = false;
  bool _isInitializing = false;
  int _generation = 0;
  Future<void>? _cleanup;
  Future<void>? _tokenWrite;
  bool get isInitializing => _isInitializing;

  String? _error;

  AuthUser? get user => _user;

  bool get isLoading => _isLoading;

  String? get error => _error;

  bool get isAuthenticated => _user != null;

  void clearError() {
    _error = null;
    notifyListeners();
  }

  Future<void> initialize() async {
    final generation = ++_generation;
    _isInitializing = true;
    notifyListeners();
    try {
      final token = await tokenStorage.getToken();
      if (generation != _generation || token == null || token.isEmpty) return;
      final restored = await authService.getMe();
      if (generation != _generation) return;
      if (restored.role == 'Customer' || restored.role == 'Provider') {
        _user = restored;
        _sessionGeneration++;
      } else {
        await tokenStorage.deleteToken();
      }
    } catch (_) {
      if (generation != _generation) return;
      _user = null;
      try {
        await tokenStorage.deleteToken();
      } catch (_) {
        // Storage may be unavailable in a browser privacy mode.
      }
    } finally {
      if (generation == _generation) {
        _isInitializing = false;
        notifyListeners();
      }
    }
  }

  Future<bool> login({required String email, required String password}) async {
    final generation = ++_generation;
    _setLoading(true);

    _error = null;

    try {
      await _cleanup;
      if (generation != _generation) return false;
      final result = await authService.login(email: email, password: password);
      if (generation != _generation) return false;

      // Mobile application is for
      // Customers and Providers.
      if (result.user.role == 'Admin') {
        await tokenStorage.deleteToken();

        _error = 'Administrator accounts must use the AssistLK web portal.';

        return false;
      }

      await (_tokenWrite = tokenStorage.saveToken(result.token));
      if (generation != _generation) return false;
      authService.apiClient.invalidateSession();

      _user = result.user;
      _sessionGeneration++;

      return true;
    } catch (error) {
      if (generation != _generation) return false;
      _error = authService.getErrorMessage(error);

      return false;
    } finally {
      if (generation == _generation) _setLoading(false);
    }
  }

  Future<bool> register({
    required String fullName,
    required String email,
    required String password,
    required String phoneNumber,
    required String role,
  }) async {
    final generation = ++_generation;
    _setLoading(true);

    _error = null;

    try {
      await _cleanup;
      if (generation != _generation) return false;
      if (role != 'Customer' && role != 'Provider') {
        _error = 'Please select Customer or Provider.';

        return false;
      }

      final result = await authService.register(
        fullName: fullName,
        email: email,
        password: password,
        phoneNumber: phoneNumber,
        role: role,
      );
      if (generation != _generation) return false;
      await (_tokenWrite = tokenStorage.saveToken(result.token));
      if (generation != _generation) return false;
      authService.apiClient.invalidateSession();

      _user = result.user;
      _sessionGeneration++;

      return true;
    } catch (error) {
      if (generation != _generation) return false;
      _error = authService.getErrorMessage(error);

      return false;
    } finally {
      if (generation == _generation) _setLoading(false);
    }
  }

  Future<void> logout() async {
    _sessionGeneration++;
    _generation++;
    authService.apiClient.invalidateSession();
    _user = null;
    _error = null;
    _isLoading = false;
    _isInitializing = false;
    final cleanup = _clearToken();
    _cleanup = cleanup;
    notifyListeners();
    await cleanup;
  }

  Future<void> _clearToken() async {
    try {
      await _tokenWrite;
    } catch (_) {
      // A failed write must not prevent deletion of an earlier stored token.
    }
    await tokenStorage.deleteToken();
  }

  void _setLoading(bool value) {
    _isLoading = value;

    notifyListeners();
  }
}
