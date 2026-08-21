import 'package:mobile/features/auth/models/auth_user.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('AuthUser should parse authentication response', () {
    final json = {
      'userId': '123',
      'fullName': 'Test Customer',
      'email': 'customer@test.com',
      'role': 'Customer',
    };

    final user = AuthUser.fromAuthResponse(json);

    expect(user.userId, '123');

    expect(user.fullName, 'Test Customer');

    expect(user.role, 'Customer');
  });
}
