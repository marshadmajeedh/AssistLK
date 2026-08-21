import 'package:flutter/foundation.dart';

import '../../../core/auth/token_storage.dart';
import '../models/auth_user.dart';
import '../services/auth_service.dart';

class AuthProvider extends ChangeNotifier {
  final AuthService authService;
  final TokenStorage tokenStorage;

  AuthProvider({required this.authService, required this.tokenStorage});

  AuthUser? _user;

  bool _isLoading = false;

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
    String? token;

    try {
      token = await tokenStorage.getToken();
    } catch (_) {
      return;
    }

    if (token == null || token.isEmpty) {
      return;
    }

    try {
      _user = await authService.getMe();
    } catch (_) {
      _user = null;

      try {
        await tokenStorage.deleteToken();
      } catch (_) {
        // Storage may be unavailable in a browser privacy mode.
      }
    }
  }

  Future<bool> login({required String email, required String password}) async {
    _setLoading(true);

    _error = null;

    try {
      final result = await authService.login(email: email, password: password);

      // Mobile application is for
      // Customers and Providers.
      if (result.user.role == 'Admin') {
        await tokenStorage.deleteToken();

        _error = 'Administrator accounts must use the AssistLK web portal.';

        return false;
      }

      await tokenStorage.saveToken(result.token);

      _user = result.user;

      return true;
    } catch (error) {
      _error = authService.getErrorMessage(error);

      return false;
    } finally {
      _setLoading(false);
    }
  }

  Future<bool> register({
    required String fullName,
    required String email,
    required String password,
    required String phoneNumber,
    required String role,
  }) async {
    _setLoading(true);

    _error = null;

    try {
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

      await tokenStorage.saveToken(result.token);

      _user = result.user;

      return true;
    } catch (error) {
      _error = authService.getErrorMessage(error);

      return false;
    } finally {
      _setLoading(false);
    }
  }

  Future<void> logout() async {
    await tokenStorage.deleteToken();

    _user = null;
    _error = null;

    notifyListeners();
  }

  void _setLoading(bool value) {
    _isLoading = value;

    notifyListeners();
  }
}
