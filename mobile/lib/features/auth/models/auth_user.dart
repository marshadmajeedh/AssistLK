class AuthUser {
  final String userId;
  final String fullName;
  final String email;
  final String role;
  final String? phoneNumber;
  final bool isActive;

  const AuthUser({
    required this.userId,
    required this.fullName,
    required this.email,
    required this.role,
    this.phoneNumber,
    this.isActive = true,
  });

  factory AuthUser.fromAuthResponse(Map<String, dynamic> json) {
    return AuthUser(
      userId: json['userId'] as String,
      fullName: json['fullName'] as String,
      email: json['email'] as String,
      role: json['role'] as String,
    );
  }

  factory AuthUser.fromProfile(Map<String, dynamic> json) {
    return AuthUser(
      userId: json['userId'] as String,
      fullName: json['fullName'] as String,
      email: json['email'] as String,
      phoneNumber: json['phoneNumber'] as String?,
      role: json['role'] as String,
      isActive: json['isActive'] as bool? ?? true,
    );
  }
}
