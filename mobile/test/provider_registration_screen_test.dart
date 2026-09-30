import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/providers/provider_registration_screen.dart';

Widget _buildTestScreen({int initialStep = 0}) {
  return MaterialApp(
    theme: ThemeData(
      useMaterial3: true,
      primaryColor: Colors.blue,
    ),
    home: ProviderRegistrationScreen(
      initialStep: initialStep,
    ),
  );
}

void main() {
  group('ProviderRegistrationScreen Widget Tests', () {
    testWidgets('renders registration screen with title and 3 stepper steps',
        (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      await tester.pumpWidget(_buildTestScreen());
      await tester.pumpAndSettle();

      expect(find.text('Provider Registration'), findsOneWidget);
      expect(find.byType(Stepper), findsOneWidget);

      expect(find.text('Credentials'), findsOneWidget);
      expect(find.text('Business & Coverage'), findsOneWidget);
      expect(find.text('Skills & Verification'), findsOneWidget);

      expect(find.widgetWithText(TextFormField, 'Full Name'), findsOneWidget);
      expect(find.widgetWithText(TextFormField, 'Email Address'), findsOneWidget);
      expect(find.widgetWithText(TextFormField, 'Password'), findsOneWidget);
      expect(find.widgetWithText(TextFormField, 'Sri Lankan Phone Number'), findsOneWidget);
    });

    testWidgets('toggles password visibility on tapping icon', (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      await tester.pumpWidget(_buildTestScreen());
      await tester.pumpAndSettle();

      // Find password field and inner TextField
      final passwordFieldFinder = find.widgetWithText(TextFormField, 'Password');
      expect(passwordFieldFinder, findsOneWidget);

      final textFieldFinder = find.descendant(
        of: passwordFieldFinder,
        matching: find.byType(TextField),
      );

      // Verify obscureText is initially true
      TextField tfWidget = tester.widget(textFieldFinder);
      expect(tfWidget.obscureText, isTrue);

      // Tap toggle icon
      final toggleButton = find.descendant(
        of: passwordFieldFinder,
        matching: find.byType(IconButton),
      );
      expect(toggleButton, findsOneWidget);
      await tester.tap(toggleButton);
      await tester.pumpAndSettle();

      // Verify obscureText is now false
      tfWidget = tester.widget(textFieldFinder);
      expect(tfWidget.obscureText, isFalse);
    });

    testWidgets('validates Step 1 required fields on tapping Continue',
        (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      await tester.pumpWidget(_buildTestScreen());
      await tester.pumpAndSettle();

      // Tap Continue on Stepper controls
      final continueButton = find.text('Continue').first;
      expect(continueButton, findsOneWidget);
      await tester.ensureVisible(continueButton);
      await tester.tap(continueButton);
      await tester.pumpAndSettle();

      // Should show validation error messages
      expect(find.text('Full Name is required'), findsOneWidget);
      expect(find.text('Email is required'), findsOneWidget);
      expect(find.text('Password is required'), findsOneWidget);
      expect(find.text('Phone number is required'), findsOneWidget);
    });

    testWidgets('advances from Step 1 to Step 2 when valid credentials are provided',
        (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      await tester.pumpWidget(_buildTestScreen());
      await tester.pumpAndSettle();

      // Fill in Step 1 fields with valid input
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Full Name'),
        'Kamal Gunaratne',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Email Address'),
        'kamal@example.com',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Password'),
        'Password@123',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Sri Lankan Phone Number'),
        '0771234567',
      );
      await tester.pumpAndSettle();

      // Tap Continue
      final continueBtn = find.text('Continue').first;
      await tester.ensureVisible(continueBtn);
      await tester.tap(continueBtn);
      await tester.pumpAndSettle();

      // Verify Step 2 is now active
      expect(find.widgetWithText(TextFormField, 'Business Name'), findsOneWidget);
      expect(find.text('Base Location Acquisition'), findsOneWidget);
      expect(find.text('Operating Radius: 10 km'), findsOneWidget);
      expect(find.byType(Slider), findsOneWidget);
    });

    testWidgets('renders Step 2 Operating Radius slider and responds to updates',
        (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      await tester.pumpWidget(_buildTestScreen(initialStep: 1));
      await tester.pumpAndSettle();

      expect(find.text('Operating Radius: 10 km'), findsOneWidget);
      final sliderFinder = find.byType(Slider);
      expect(sliderFinder, findsOneWidget);

      // Drag the slider
      await tester.ensureVisible(sliderFinder);
      await tester.drag(sliderFinder, const Offset(120, 0));
      await tester.pumpAndSettle();

      // Verify label changed from 10 km
      expect(find.text('Operating Radius: 10 km'), findsNothing);
    });

    testWidgets('renders Step 3 multi-skill cards with primary service',
        (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      await tester.pumpWidget(_buildTestScreen(initialStep: 2));
      await tester.pumpAndSettle();

      // Verify Step 3 header
      expect(find.text('Services Offered (1)'), findsOneWidget);
      expect(find.text('Service #1 (Primary)'), findsOneWidget);

      // Category Dropdown
      expect(find.text('Plumbing'), findsOneWidget);

      // Skill Competency Field
      expect(find.widgetWithText(TextFormField, 'Skill Competency'), findsOneWidget);

      // PDF Certificate Button
      expect(find.text('Select PDF Certificate'), findsOneWidget);

      // Add Another Service Button
      expect(find.text('+ Add Another Service (1/4)'), findsOneWidget);

      // Initially no delete icon on primary card
      expect(find.byIcon(Icons.delete_outline), findsNothing);
    });

    testWidgets('dynamically adds a second service card on tapping Add Another Service',
        (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      await tester.pumpWidget(_buildTestScreen(initialStep: 2));
      await tester.pumpAndSettle();

      // Initial state: 1 service
      expect(find.text('Services Offered (1)'), findsOneWidget);

      // Tap "+ Add Another Service (1/4)"
      final addServiceButton = find.text('+ Add Another Service (1/4)');
      expect(addServiceButton, findsOneWidget);
      await tester.ensureVisible(addServiceButton);
      await tester.tap(addServiceButton);
      await tester.pumpAndSettle();

      // Verified: 2 services configured
      expect(find.text('Services Offered (2)'), findsOneWidget);
      expect(find.text('Service #1 (Primary)'), findsOneWidget);
      expect(find.text('Service #2'), findsOneWidget);

      // Next category automatically selected (Electrical)
      expect(find.text('Electrical'), findsOneWidget);

      // Delete icon buttons are now visible on both cards
      expect(find.byIcon(Icons.delete_outline), findsNWidgets(2));

      // Button now indicates 2/4
      expect(find.text('+ Add Another Service (2/4)'), findsOneWidget);
    });

    testWidgets('removes secondary service card on tapping delete icon',
        (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      await tester.pumpWidget(_buildTestScreen(initialStep: 2));
      await tester.pumpAndSettle();

      // Add 2nd service
      final addBtn = find.text('+ Add Another Service (1/4)');
      await tester.ensureVisible(addBtn);
      await tester.tap(addBtn);
      await tester.pumpAndSettle();
      expect(find.text('Services Offered (2)'), findsOneWidget);

      // Tap delete icon on 2nd card
      final deleteButtons = find.byIcon(Icons.delete_outline);
      expect(deleteButtons, findsNWidgets(2));
      await tester.ensureVisible(deleteButtons.last);
      await tester.tap(deleteButtons.last);
      await tester.pumpAndSettle();

      // Decreased back to 1 service
      expect(find.text('Services Offered (1)'), findsOneWidget);
      expect(find.text('Service #2'), findsNothing);
      expect(find.byIcon(Icons.delete_outline), findsNothing);
      expect(find.text('+ Add Another Service (1/4)'), findsOneWidget);
    });

    testWidgets('allows adding up to 4 distinct categories', (tester) async {
      tester.view.physicalSize = const Size(1080, 3200);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      await tester.pumpWidget(_buildTestScreen(initialStep: 2));
      await tester.pumpAndSettle();

      // Add 2nd, 3rd, 4th services
      for (int i = 1; i <= 3; i++) {
        final addBtn = find.textContaining('+ Add Another Service');
        expect(addBtn, findsOneWidget);
        await tester.ensureVisible(addBtn);
        await tester.tap(addBtn);
        await tester.pumpAndSettle();
      }

      // Max reached: 4 services
      expect(find.text('Services Offered (4)'), findsOneWidget);
      expect(find.text('Service #1 (Primary)'), findsOneWidget);
      expect(find.text('Service #2'), findsOneWidget);
      expect(find.text('Service #3'), findsOneWidget);
      expect(find.text('Service #4'), findsOneWidget);

      // Button is replaced with max limit info container
      expect(find.textContaining('+ Add Another Service'), findsNothing);
      expect(find.byIcon(Icons.delete_outline), findsNWidgets(4));
    });
  });
}
