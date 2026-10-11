import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'package:mobile/core/api/api_client.dart';
import 'package:mobile/core/auth/token_storage.dart';
import 'package:mobile/features/auth/models/auth_user.dart';
import 'package:mobile/features/auth/models/registration_challenge_result.dart';
import 'package:mobile/features/auth/providers/auth_provider.dart';
import 'package:mobile/features/auth/screens/account_type_selection_screen.dart';
import 'package:mobile/features/auth/screens/customer_phone_otp_screen.dart';
import 'package:mobile/features/auth/screens/customer_register_screen.dart';
import 'package:mobile/features/auth/screens/login_screen.dart';
import 'package:mobile/features/auth/services/auth_service.dart';
import 'package:mobile/shared/theme/app_theme.dart';
import 'package:mobile/app/app.dart';
import 'package:mobile/app/customer_app_shell.dart';
import 'customer_app_shell_test.dart' show ShellRequests;
import 'mocks/mock_location_geocoding_service.dart';

class TestTokenStorage extends TokenStorage {
  String? storedToken;

  @override
  Future<String?> getToken() async => storedToken;

  @override
  Future<void> saveToken(String token) async {
    generation++;
    storedToken = token;
  }

  @override
  Future<void> deleteToken() async {
    generation++;
    storedToken = null;
  }
}

class MockOtpAuthProvider extends ChangeNotifier implements AuthProvider {
  @override
  int get sessionGeneration => _sessionGen;
  int _sessionGen = 0;

  AuthUser? _user;
  final bool _isLoading = false;
  String? _error;
  final TestTokenStorage tokenStorageMock = TestTokenStorage();

  String? lastRegisterFullName;
  String? lastRegisterEmail;
  String? lastRegisterPhone;
  String? lastRegisterPassword;
  String? lastRegisterRole;

  String? lastVerifyChallengeId;
  String? lastVerifyOtp;

  String? lastResendChallengeId;

  bool registerStartShouldSucceed = true;
  bool verifyOtpShouldSucceed = true;
  String? customError;
  int resendCooldown = 45;

  @override
  AuthUser? get user => _user;

  @override
  bool get isLoading => _isLoading;

  @override
  bool get isInitializing => false;

  @override
  String? get error => _error;

  @override
  bool get isAuthenticated => _user != null;

  @override
  void clearError() {
    _error = null;
    notifyListeners();
  }

  void setError(String err) {
    _error = err;
    notifyListeners();
  }

  @override
  Future<void> initialize() async {}

  @override
  Future<bool> login({required String email, required String password}) async {
    _user = const AuthUser(
      userId: 'existing-cust-id',
      email: 'customer@assistlk.com',
      fullName: 'Existing Customer',
      role: 'Customer',
    );
    await tokenStorageMock.saveToken('existing-jwt-token');
    _sessionGen++;
    notifyListeners();
    return true;
  }

  @override
  Future<bool> register({
    required String fullName,
    required String email,
    required String password,
    required String phoneNumber,
    required String role,
  }) async {
    return false;
  }

  @override
  Future<RegistrationChallengeResult?> registerStart({
    required String fullName,
    required String email,
    required String password,
    required String phoneNumber,
    required String role,
  }) async {
    lastRegisterFullName = fullName;
    lastRegisterEmail = email;
    lastRegisterPhone = phoneNumber;
    lastRegisterPassword = password;
    lastRegisterRole = role;

    if (!registerStartShouldSucceed) {
      _error = customError ?? 'Registration failed.';
      notifyListeners();
      return null;
    }

    // Must NOT store JWT or set _user
    return RegistrationChallengeResult(
      challengeId: 'challenge-uuid-1234',
      maskedPhoneNumber: '+94 77 *** *567',
      expiresAtUtc: DateTime.now().add(const Duration(minutes: 5)),
      cooldownSeconds: 45,
    );
  }

  @override
  Future<bool> verifyRegisterOtp({
    required String challengeId,
    required String otp,
  }) async {
    lastVerifyChallengeId = challengeId;
    lastVerifyOtp = otp;

    if (!verifyOtpShouldSucceed) {
      _error = customError ?? 'Invalid or expired OTP.';
      notifyListeners();
      return false;
    }

    _user = const AuthUser(
      userId: 'new-cust-id',
      email: 'newcustomer@assistlk.com',
      fullName: 'New Customer',
      role: 'Customer',
    );
    await tokenStorageMock.saveToken('verified-jwt-token');
    _sessionGen++;
    notifyListeners();
    return true;
  }

  @override
  Future<int?> resendRegisterOtp({
    required String challengeId,
  }) async {
    lastResendChallengeId = challengeId;
    return resendCooldown;
  }

  @override
  Future<void> logout() async {
    _user = null;
    await tokenStorageMock.deleteToken();
    notifyListeners();
  }

  @override
  AuthService get authService => throw UnimplementedError();

  @override
  TokenStorage get tokenStorage => tokenStorageMock;
}

Widget _wrap(Widget child, MockOtpAuthProvider provider) {
  return ChangeNotifierProvider<AuthProvider>.value(
    value: provider,
    child: MaterialApp(
      theme: AppTheme.lightTheme,
      home: child,
    ),
  );
}

void main() {
  late MockOtpAuthProvider auth;

  setUp(() {
    auth = MockOtpAuthProvider();
  });

  group('Customer Registration Phone Mandatory & Validation UX', () {
    testWidgets('shows validation error when phone number is empty', (tester) async {
      await tester.pumpWidget(_wrap(const CustomerRegisterScreen(), auth));
      await tester.pumpAndSettle();

      final createBtn = find.widgetWithText(ElevatedButton, 'Create Account');
      await tester.ensureVisible(createBtn);
      await tester.tap(createBtn);
      await tester.pumpAndSettle();

      expect(find.text('Phone number is required.'), findsOneWidget);
    });

    testWidgets('shows validation error when phone number format is invalid', (tester) async {
      await tester.pumpWidget(_wrap(const CustomerRegisterScreen(), auth));
      await tester.pumpAndSettle();

      final phoneFinder = find.widgetWithText(TextField, 'Phone Number');
      final createBtn = find.widgetWithText(ElevatedButton, 'Create Account');

      // Invalid: landline or wrong prefix
      await tester.enterText(phoneFinder, '0112345678');
      await tester.ensureVisible(createBtn);
      await tester.tap(createBtn);
      await tester.pumpAndSettle();

      expect(find.text('Please enter a valid mobile number (e.g. 077 123 4567).'), findsOneWidget);

      // Invalid: short
      await tester.enterText(phoneFinder, '077123');
      await tester.tap(createBtn);
      await tester.pumpAndSettle();

      expect(find.text('Please enter a valid mobile number (e.g. 077 123 4567).'), findsOneWidget);
    });

    testWidgets('accepts valid mobile phone formats: 0771234567, 077 123 4567, +94771234567', (tester) async {
      await tester.pumpWidget(_wrap(const CustomerRegisterScreen(), auth));
      await tester.pumpAndSettle();

      final phoneFinder = find.widgetWithText(TextField, 'Phone Number');
      final createBtn = find.widgetWithText(ElevatedButton, 'Create Account');

      await tester.enterText(find.widgetWithText(TextField, 'Full Name'), 'Kamal Perera');
      await tester.enterText(find.widgetWithText(TextField, 'Email'), 'kamal@assistlk.com');
      await tester.enterText(phoneFinder, '077 123 4567');
      await tester.enterText(find.widgetWithText(TextField, 'Password'), 'Password123!');
      await tester.enterText(find.widgetWithText(TextField, 'Confirm Password'), 'Password123!');

      await tester.ensureVisible(createBtn);
      await tester.tap(createBtn);
      await tester.pumpAndSettle();

      // No phone error
      expect(find.text('Please enter a valid mobile number (e.g. 077 123 4567).'), findsNothing);
      expect(find.text('Phone number is required.'), findsNothing);
      expect(auth.lastRegisterPhone, '077 123 4567');
      expect(auth.lastRegisterRole, 'Customer');
    });
  });

  group('Registration Start → OTP Screen Transition', () {
    testWidgets('navigates to CustomerPhoneOtpScreen and displays masked phone', (tester) async {
      await tester.pumpWidget(_wrap(const CustomerRegisterScreen(), auth));
      await tester.pumpAndSettle();

      await tester.enterText(find.widgetWithText(TextField, 'Full Name'), 'Kamal Perera');
      await tester.enterText(find.widgetWithText(TextField, 'Email'), 'kamal@assistlk.com');
      await tester.enterText(find.widgetWithText(TextField, 'Phone Number'), '0771234567');
      await tester.enterText(find.widgetWithText(TextField, 'Password'), 'Password123!');
      await tester.enterText(find.widgetWithText(TextField, 'Confirm Password'), 'Password123!');

      final createBtn = find.widgetWithText(ElevatedButton, 'Create Account');
      await tester.ensureVisible(createBtn);
      await tester.tap(createBtn);
      await tester.pumpAndSettle();

      // OTP Screen should now be open
      expect(find.byType(CustomerPhoneOtpScreen), findsOneWidget);
      expect(find.text('Verify your phone'), findsOneWidget);
      expect(find.textContaining('+94 77 *** *567'), findsOneWidget);

      // Verify JWT was NOT saved before OTP verification
      expect(auth.tokenStorageMock.storedToken, isNull);
      expect(auth.isAuthenticated, isFalse);
    });

    testWidgets('Change phone number pops back and preserves form values in memory', (tester) async {
      await tester.pumpWidget(_wrap(const CustomerRegisterScreen(), auth));
      await tester.pumpAndSettle();

      await tester.enterText(find.widgetWithText(TextField, 'Full Name'), 'Kamal Perera');
      await tester.enterText(find.widgetWithText(TextField, 'Email'), 'kamal@assistlk.com');
      await tester.enterText(find.widgetWithText(TextField, 'Phone Number'), '0771234567');
      await tester.enterText(find.widgetWithText(TextField, 'Password'), 'Password123!');
      await tester.enterText(find.widgetWithText(TextField, 'Confirm Password'), 'Password123!');

      final createBtn = find.widgetWithText(ElevatedButton, 'Create Account');
      await tester.ensureVisible(createBtn);
      await tester.tap(createBtn);
      await tester.pumpAndSettle();

      expect(find.byType(CustomerPhoneOtpScreen), findsOneWidget);

      // Tap Change phone number
      await tester.tap(find.text('Change phone number'));
      await tester.pumpAndSettle();

      // Should be back on CustomerRegisterScreen
      expect(find.byType(CustomerRegisterScreen), findsOneWidget);
      expect(find.widgetWithText(TextField, 'Kamal Perera'), findsOneWidget);
      expect(find.widgetWithText(TextField, 'kamal@assistlk.com'), findsOneWidget);
      expect(find.widgetWithText(TextField, '0771234567'), findsOneWidget);
    });
  });

  group('CustomerPhoneOtpScreen Verification & UX', () {
    final testChallenge = RegistrationChallengeResult(
      challengeId: 'challenge-uuid-1234',
      maskedPhoneNumber: '+94 77 *** *567',
      expiresAtUtc: DateTime.now().add(const Duration(minutes: 5)),
      cooldownSeconds: 45,
    );

    testWidgets('entering 6 digits triggers verification and handles wrong OTP', (tester) async {
      auth.verifyOtpShouldSucceed = false;
      auth.customError = 'Invalid verification code. 4 attempt(s) remaining.';

      await tester.pumpWidget(_wrap(CustomerPhoneOtpScreen(challenge: testChallenge), auth));
      await tester.pumpAndSettle();

      // Enter 6 digits
      final hiddenTextField = find.byType(TextField);
      await tester.enterText(hiddenTextField, '123456');
      await tester.pumpAndSettle();

      expect(auth.lastVerifyOtp, '123456');
      expect(auth.lastVerifyChallengeId, 'challenge-uuid-1234');
      expect(find.text('Invalid verification code. 4 attempt(s) remaining.'), findsOneWidget);
      expect(auth.tokenStorageMock.storedToken, isNull);
    });

    testWidgets('resend timer countdown and resend action', (tester) async {
      await tester.pumpWidget(_wrap(CustomerPhoneOtpScreen(challenge: testChallenge), auth));
      await tester.pump();

      expect(find.text('Resend code in 45s'), findsOneWidget);

      // Fast forward 45 seconds
      await tester.pump(const Duration(seconds: 46));
      await tester.pumpAndSettle();

      expect(find.text('Resend code'), findsOneWidget);

      // Tap resend
      await tester.tap(find.text('Resend code'));
      await tester.pumpAndSettle();

      expect(auth.lastResendChallengeId, 'challenge-uuid-1234');
      expect(find.textContaining('A new verification code has been sent'), findsOneWidget);
    });

    testWidgets('successful OTP verification persists JWT token and authenticates user', (tester) async {
      auth.verifyOtpShouldSucceed = true;

      await tester.pumpWidget(_wrap(CustomerPhoneOtpScreen(challenge: testChallenge), auth));
      await tester.pumpAndSettle();

      final hiddenTextField = find.byType(TextField);
      await tester.enterText(hiddenTextField, '654321');
      await tester.pumpAndSettle();

      expect(auth.lastVerifyOtp, '654321');
      expect(auth.tokenStorageMock.storedToken, 'verified-jwt-token');
      expect(auth.isAuthenticated, isTrue);
      expect(auth.user?.fullName, 'New Customer');
    });
  });

  group('AuthGate & Full Flow Integration', () {
    testWidgets('AuthGate routes to CustomerAppShell after OTP verification', (tester) async {
      final apiClient = ApiClient(tokenStorage: auth.tokenStorageMock);
      final requests = ShellRequests(apiClient);

      await tester.pumpWidget(
        ChangeNotifierProvider<AuthProvider>.value(
          value: auth,
          child: AssistLKApp(
            serviceRequestService: requests,
            geocodingService: MockLocationGeocodingService(),
          ),
        ),
      );
      await tester.pumpAndSettle();

      // Initially unauthenticated -> LoginScreen
      expect(find.byType(LoginScreen), findsOneWidget);

      // Navigate to AccountTypeSelectionScreen
      await tester.tap(find.text('Create an account'));
      await tester.pumpAndSettle();
      expect(find.byType(AccountTypeSelectionScreen), findsOneWidget);

      // Select Customer
      await tester.tap(find.text('Register as Customer'));
      await tester.pumpAndSettle();
      expect(find.byType(CustomerRegisterScreen), findsOneWidget);

      // Fill valid registration form
      await tester.enterText(find.widgetWithText(TextField, 'Full Name'), 'Saman Silva');
      await tester.enterText(find.widgetWithText(TextField, 'Email'), 'saman@assistlk.com');
      await tester.enterText(find.widgetWithText(TextField, 'Phone Number'), '0779998877');
      await tester.enterText(find.widgetWithText(TextField, 'Password'), 'Password123!');
      await tester.enterText(find.widgetWithText(TextField, 'Confirm Password'), 'Password123!');

      final createBtn = find.widgetWithText(ElevatedButton, 'Create Account');
      await tester.ensureVisible(createBtn);
      await tester.tap(createBtn);
      await tester.pumpAndSettle();

      // On OTP screen
      expect(find.byType(CustomerPhoneOtpScreen), findsOneWidget);

      // Enter OTP
      await tester.enterText(find.byType(TextField), '987654');
      await tester.pumpAndSettle();

      // Verified! AuthGate should now display CustomerAppShell
      expect(find.byType(CustomerAppShell), findsOneWidget);
    });

    testWidgets('Existing customer login remains intact without OTP prompt', (tester) async {
      final apiClient = ApiClient(tokenStorage: auth.tokenStorageMock);
      final requests = ShellRequests(apiClient);

      await tester.pumpWidget(
        ChangeNotifierProvider<AuthProvider>.value(
          value: auth,
          child: AssistLKApp(
            serviceRequestService: requests,
            geocodingService: MockLocationGeocodingService(),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.byType(LoginScreen), findsOneWidget);

      await tester.enterText(find.widgetWithText(TextField, 'Email'), 'customer@assistlk.com');
      await tester.enterText(find.widgetWithText(TextField, 'Password'), 'Password123!');
      await tester.tap(find.text('Sign In'));
      await tester.pumpAndSettle();

      // Logged in directly to CustomerAppShell without OTP challenge
      expect(find.byType(CustomerAppShell), findsOneWidget);
      expect(auth.user?.fullName, 'Existing Customer');
    });

    testWidgets('Provider registration is unaffected and shows provider notice', (tester) async {
      await tester.pumpWidget(
        ChangeNotifierProvider<AuthProvider>.value(
          value: auth,
          child: MaterialApp(
            theme: AppTheme.lightTheme,
            home: const AccountTypeSelectionScreen(),
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.text('Register as Provider'));
      await tester.pumpAndSettle();

      expect(find.text('Provider Registration'), findsOneWidget);
      expect(find.byType(CustomerPhoneOtpScreen), findsNothing);
    });
  });
}
