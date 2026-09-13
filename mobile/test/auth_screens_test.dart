import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'package:mobile/core/auth/token_storage.dart';
import 'package:mobile/features/auth/models/auth_user.dart';
import 'package:mobile/features/auth/providers/auth_provider.dart';
import 'package:mobile/features/auth/screens/account_type_selection_screen.dart';
import 'package:mobile/features/auth/screens/customer_register_screen.dart';
import 'package:mobile/features/auth/screens/login_screen.dart';
import 'package:mobile/features/auth/services/auth_service.dart';
import 'package:mobile/shared/theme/app_assets.dart';
import 'package:mobile/shared/theme/app_theme.dart';
import 'package:mobile/shared/widgets/app_image_asset.dart';

class FakeAuthProvider extends ChangeNotifier implements AuthProvider {
  @override
  int get sessionGeneration => 0;
  @override
  void sessionExpired() { _user = null; notifyListeners(); }
  AuthUser? _user;
  final bool _isLoading = false;
  String? _error;

  String? lastLoginEmail;
  String? lastLoginPassword;

  String? lastRegisterFullName;
  String? lastRegisterEmail;
  String? lastRegisterPassword;
  String? lastRegisterPhone;
  String? lastRegisterRole;

  bool loginResult = true;
  bool registerResult = true;

  @override
  AuthUser? get user => _user;

  @override
  bool get isLoading => _isLoading;

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
    lastLoginEmail = email;
    lastLoginPassword = password;
    return loginResult;
  }

  @override
  Future<bool> register({
    required String fullName,
    required String email,
    required String password,
    required String phoneNumber,
    required String role,
  }) async {
    lastRegisterFullName = fullName;
    lastRegisterEmail = email;
    lastRegisterPassword = password;
    lastRegisterPhone = phoneNumber;
    lastRegisterRole = role;
    return registerResult;
  }

  @override
  Future<void> logout() async {
    _user = null;
    notifyListeners();
  }

  @override
  AuthService get authService => throw UnimplementedError();

  @override
  TokenStorage get tokenStorage => throw UnimplementedError();
}

Widget _buildScreen(Widget screen, FakeAuthProvider authProvider) {
  return ChangeNotifierProvider<AuthProvider>.value(
    value: authProvider,
    child: MaterialApp(
      theme: AppTheme.lightTheme,
      home: screen,
    ),
  );
}

void main() {
  late FakeAuthProvider fakeAuth;

  setUp(() {
    fakeAuth = FakeAuthProvider();
  });

  group('LoginScreen UX & Flow Tests', () {
    testWidgets('renders auth_welcome.png, brand title, subtitle, and input fields',
        (tester) async {
      await tester.pumpWidget(_buildScreen(const LoginScreen(), fakeAuth));
      await tester.pumpAndSettle();

      // Asset
      final imageAssetFinder = find.byWidgetPredicate((widget) =>
          widget is AppImageAsset && widget.assetPath == AppAssets.authWelcome);
      expect(imageAssetFinder, findsOneWidget);

      // Typography
      expect(find.text('AssistLK'), findsOneWidget);
      expect(find.text('Sign in to continue'), findsOneWidget);

      // Inputs & Button
      expect(find.text('Email'), findsOneWidget);
      expect(find.text('Password'), findsOneWidget);
      expect(find.text('Sign In'), findsOneWidget);

      // Create account action
      expect(find.text("Don't have an account?"), findsOneWidget);
      expect(find.text('Create an account'), findsOneWidget);
    });

    testWidgets('password visibility toggle changes obscurity', (tester) async {
      await tester.pumpWidget(_buildScreen(const LoginScreen(), fakeAuth));
      await tester.pumpAndSettle();

      final passwordFieldFinder = find.widgetWithText(TextField, 'Password');
      expect(passwordFieldFinder, findsOneWidget);

      // Initially obscureText is true
      TextField passwordField = tester.widget(passwordFieldFinder);
      expect(passwordField.obscureText, isTrue);

      // Tap visibility toggle
      await tester.tap(find.byIcon(Icons.visibility_off_outlined));
      await tester.pumpAndSettle();

      passwordField = tester.widget(passwordFieldFinder);
      expect(passwordField.obscureText, isFalse);
    });

    testWidgets('validates required email and valid email format',
        (tester) async {
      await tester.pumpWidget(_buildScreen(const LoginScreen(), fakeAuth));
      await tester.pumpAndSettle();

      // Tap Sign In with empty fields
      await tester.tap(find.text('Sign In'));
      await tester.pumpAndSettle();

      expect(find.text('Email is required.'), findsOneWidget);
      expect(find.text('Password is required.'), findsOneWidget);

      // Enter invalid email format
      await tester.enterText(
          find.widgetWithText(TextField, 'Email'), 'invalidemail');
      await tester.tap(find.text('Sign In'));
      await tester.pumpAndSettle();

      expect(find.text('Enter a valid email.'), findsOneWidget);
    });

    testWidgets('submits email and password to AuthProvider', (tester) async {
      await tester.pumpWidget(_buildScreen(const LoginScreen(), fakeAuth));
      await tester.pumpAndSettle();

      await tester.enterText(
          find.widgetWithText(TextField, 'Email'), 'customer@assistlk.com');
      await tester.enterText(
          find.widgetWithText(TextField, 'Password'), 'secret123');

      await tester.tap(find.text('Sign In'));
      await tester.pumpAndSettle();

      expect(fakeAuth.lastLoginEmail, 'customer@assistlk.com');
      expect(fakeAuth.lastLoginPassword, 'secret123');
    });

    testWidgets(
        'tapping "Create an account" navigates to AccountTypeSelectionScreen',
        (tester) async {
      await tester.pumpWidget(_buildScreen(const LoginScreen(), fakeAuth));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Create an account'));
      await tester.pumpAndSettle();

      expect(find.byType(AccountTypeSelectionScreen), findsOneWidget);
    });
  });

  group('AccountTypeSelectionScreen Flow Tests', () {
    testWidgets(
        'renders auth_register.png, title, and both Customer and Provider choices',
        (tester) async {
      await tester.pumpWidget(
          _buildScreen(const AccountTypeSelectionScreen(), fakeAuth));
      await tester.pumpAndSettle();

      // Asset
      final imageAssetFinder = find.byWidgetPredicate((widget) =>
          widget is AppImageAsset &&
          widget.assetPath == AppAssets.authRegister);
      expect(imageAssetFinder, findsOneWidget);

      // Title & Subtitle
      expect(find.text('Join AssistLK'), findsWidgets);
      expect(
          find.text('Choose how you would like to get started'), findsOneWidget);

      // Choices
      expect(find.text('Register as Customer'), findsOneWidget);
      expect(
          find.text('Request home, vehicle, electrical and appliance services.'),
          findsOneWidget);

      expect(find.text('Register as Provider'), findsOneWidget);
      expect(find.text('Offer professional services through AssistLK.'),
          findsOneWidget);

      // Must NOT contain registration fields
      expect(find.byType(TextField), findsNothing);
      expect(find.byType(TextFormField), findsNothing);
    });

    testWidgets('tapping "Register as Customer" navigates to CustomerRegisterScreen',
        (tester) async {
      await tester.pumpWidget(
          _buildScreen(const AccountTypeSelectionScreen(), fakeAuth));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Register as Customer'));
      await tester.pumpAndSettle();

      expect(find.byType(CustomerRegisterScreen), findsOneWidget);
    });

    testWidgets(
        'tapping "Register as Provider" shows neutral notice and does not open Customer registration',
        (tester) async {
      await tester.pumpWidget(
          _buildScreen(const AccountTypeSelectionScreen(), fakeAuth));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Register as Provider'));
      await tester.pumpAndSettle();

      // Does NOT navigate to customer registration
      expect(find.byType(CustomerRegisterScreen), findsNothing);

      // Shows neutral provider onboarding notice
      expect(find.text('Provider Registration'), findsOneWidget);
      expect(
        find.textContaining('Provider registration is being prepared'),
        findsOneWidget,
      );
      expect(
        find.textContaining('Provider onboarding will be available soon'),
        findsOneWidget,
      );

      // Ensure internal assignment terminology is NOT exposed to users
      expect(find.textContaining('Component 2'), findsNothing);
    });
  });

  group('CustomerRegisterScreen UX & Validation Tests', () {
    testWidgets('renders auth_customer_register.png, title, and helper text',
        (tester) async {
      await tester.pumpWidget(
          _buildScreen(const CustomerRegisterScreen(), fakeAuth));
      await tester.pumpAndSettle();

      // Asset
      final imageAssetFinder = find.byWidgetPredicate((widget) =>
          widget is AppImageAsset &&
          widget.assetPath == AppAssets.authCustomerRegister);
      expect(imageAssetFinder, findsOneWidget);

      // Header & helper text
      expect(find.text('Join AssistLK'), findsOneWidget);
      expect(
        find.text(
            'Create your customer account to request services with AssistLK.'),
        findsOneWidget,
      );

      // Registration fields
      expect(find.text('Full Name'), findsOneWidget);
      expect(find.text('Email'), findsOneWidget);
      expect(find.text('Phone Number'), findsOneWidget);
      expect(find.text('Password'), findsOneWidget);
      expect(find.text('Confirm Password'), findsOneWidget);

      // Ensure NO Account Type dropdown exists
      expect(find.byType(DropdownButtonFormField<String>), findsNothing);
    });

    testWidgets('validates full name, email, password length, and password match',
        (tester) async {
      await tester.pumpWidget(
          _buildScreen(const CustomerRegisterScreen(), fakeAuth));
      await tester.pumpAndSettle();

      final createBtn = find.widgetWithText(ElevatedButton, 'Create Account');

      // Tap Create Account with empty fields
      await tester.ensureVisible(createBtn);
      await tester.tap(createBtn);
      await tester.pumpAndSettle();

      expect(find.text('Full Name is required.'), findsNothing); // label is 'Full name is required.'
      expect(find.text('Full name is required.'), findsOneWidget);
      expect(find.text('Email is required.'), findsOneWidget);
      expect(
          find.text('Password must contain at least 8 characters.'), findsOneWidget);

      // Fill in invalid email and short password
      await tester.enterText(
          find.widgetWithText(TextField, 'Full Name'), 'Kamal Perera');
      await tester.enterText(
          find.widgetWithText(TextField, 'Email'), 'kamalwithoutat');
      await tester.enterText(
          find.widgetWithText(TextField, 'Password'), 'short');
      await tester.enterText(
          find.widgetWithText(TextField, 'Confirm Password'), 'different');

      await tester.ensureVisible(createBtn);
      await tester.tap(createBtn);
      await tester.pumpAndSettle();

      expect(find.text('Enter a valid email.'), findsOneWidget);
      expect(
          find.text('Password must contain at least 8 characters.'), findsOneWidget);

      // Fill in matching passwords >= 8 chars and valid email
      await tester.enterText(
          find.widgetWithText(TextField, 'Email'), 'kamal@assistlk.com');
      await tester.enterText(
          find.widgetWithText(TextField, 'Password'), 'password123');
      await tester.enterText(
          find.widgetWithText(TextField, 'Confirm Password'), 'password999');

      await tester.ensureVisible(createBtn);
      await tester.tap(createBtn);
      await tester.pumpAndSettle();

      expect(find.text('Passwords do not match.'), findsOneWidget);
    });

    testWidgets('submits registration with role "Customer"', (tester) async {
      await tester.pumpWidget(
          _buildScreen(const CustomerRegisterScreen(), fakeAuth));
      await tester.pumpAndSettle();

      final createBtn = find.widgetWithText(ElevatedButton, 'Create Account');

      await tester.enterText(
          find.widgetWithText(TextField, 'Full Name'), 'Kamal Perera');
      await tester.enterText(
          find.widgetWithText(TextField, 'Email'), 'kamal@assistlk.com');
      await tester.enterText(
          find.widgetWithText(TextField, 'Phone Number'), '0771234567');
      await tester.enterText(
          find.widgetWithText(TextField, 'Password'), 'password123');
      await tester.enterText(
          find.widgetWithText(TextField, 'Confirm Password'), 'password123');

      await tester.ensureVisible(createBtn);
      await tester.tap(createBtn);
      await tester.pumpAndSettle();

      expect(fakeAuth.lastRegisterFullName, 'Kamal Perera');
      expect(fakeAuth.lastRegisterEmail, 'kamal@assistlk.com');
      expect(fakeAuth.lastRegisterPhone, '0771234567');
      expect(fakeAuth.lastRegisterPassword, 'password123');
      expect(fakeAuth.lastRegisterRole, 'Customer');
    });
  });

  group('Narrow Viewport Overflow Safety', () {
    testWidgets('LoginScreen renders safely on compact 360x640 screen without overflow',
        (tester) async {
      tester.view.physicalSize = const Size(360, 640);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      await tester.pumpWidget(_buildScreen(const LoginScreen(), fakeAuth));
      await tester.pumpAndSettle();

      expect(find.text('AssistLK'), findsOneWidget);
      expect(tester.takeException(), isNull);
    });

    testWidgets(
        'AccountTypeSelectionScreen renders safely on compact 360x640 screen without overflow',
        (tester) async {
      tester.view.physicalSize = const Size(360, 640);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      await tester.pumpWidget(
          _buildScreen(const AccountTypeSelectionScreen(), fakeAuth));
      await tester.pumpAndSettle();

      expect(find.text('Register as Customer'), findsOneWidget);
      expect(tester.takeException(), isNull);
    });

    testWidgets(
        'CustomerRegisterScreen renders safely on compact 360x640 screen without overflow',
        (tester) async {
      tester.view.physicalSize = const Size(360, 640);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      await tester.pumpWidget(
          _buildScreen(const CustomerRegisterScreen(), fakeAuth));
      await tester.pumpAndSettle();

      expect(find.text('Join AssistLK'), findsOneWidget);
      expect(tester.takeException(), isNull);
    });
  });
}
