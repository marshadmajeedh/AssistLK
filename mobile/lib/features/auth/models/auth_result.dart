import 'auth_user.dart';

class AuthResult {
  final AuthUser user;
  final String token;
  final DateTime expiresAtUtc;

  const AuthResult({
    required this.user,
    required this.token,
    required this.expiresAtUtc,
  });

  factory AuthResult.fromJson(Map<String, dynamic> json) {
    return AuthResult(
      user: AuthUser.fromAuthResponse(json),

      token: json['token'] as String,

      expiresAtUtc: DateTime.parse(json['expiresAtUtc'] as String),
    );
  }
}
