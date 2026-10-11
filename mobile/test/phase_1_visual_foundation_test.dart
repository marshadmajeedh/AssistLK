import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/shared/theme/app_assets.dart';
import 'package:mobile/shared/theme/app_colors.dart';
import 'package:mobile/shared/theme/app_text_styles.dart';
import 'package:mobile/shared/theme/app_theme.dart';
import 'package:mobile/shared/widgets/app_image_asset.dart';
import 'package:mobile/shared/widgets/empty_state_card.dart';
import 'package:mobile/shared/widgets/section_header.dart';

Widget _buildTestable(Widget child) {
  return MaterialApp(
    theme: AppTheme.lightTheme,
    home: Scaffold(
      body: child,
    ),
  );
}

void main() {
  group('Phase 1 Design Tokens & AppAssets', () {
    test('AppColors provides all required semantic surfaces and category tints', () {
      expect(AppColors.primarySurface, isNotNull);
      expect(AppColors.secondarySurface, isNotNull);
      expect(AppColors.successSurface, isNotNull);
      expect(AppColors.warningSurface, isNotNull);
      expect(AppColors.aiSurface, isNotNull);

      expect(AppColors.plumbingTint, isNotNull);
      expect(AppColors.electricalTint, isNotNull);
      expect(AppColors.vehicleTint, isNotNull);
      expect(AppColors.applianceTint, isNotNull);

      // Verify distinct color values
      expect(AppColors.primarySurface, const Color(0xFFEFF6FF));
      expect(AppColors.secondarySurface, const Color(0xFFF0FDFA));
      expect(AppColors.successSurface, const Color(0xFFF0FDF4));
      expect(AppColors.warningSurface, const Color(0xFFFFFBEB));
      expect(AppColors.aiSurface, const Color(0xFFF5F3FF));
    });

    test('AppTextStyles provides metadata text style', () {
      expect(AppTextStyles.metadata.fontSize, 11);
      expect(AppTextStyles.metadata.fontWeight, FontWeight.w500);
      expect(AppTextStyles.metadata.color, AppColors.textSecondary);
    });

    test('AppAssets constants declare valid paths for all auth and service assets', () {
      expect(AppAssets.authWelcome, 'assets/images/auth/auth_welcome.png');
      expect(AppAssets.authRegister, 'assets/images/auth/auth_register.png');
      expect(AppAssets.authCustomerRegister, 'assets/images/auth/auth_customer_register.png');
      expect(AppAssets.homeServiceHero, 'assets/images/hero/home_service_hero.png');
      expect(AppAssets.plumbingService, 'assets/images/categories/plumbing_service.png');
      expect(AppAssets.electricalService, 'assets/images/categories/electrical_service.png');
      expect(AppAssets.vehicleService, 'assets/images/categories/vehicle_service.png');
      expect(AppAssets.applianceService, 'assets/images/categories/appliance_service.png');
      expect(AppAssets.aiDiagnosisSpark, 'assets/images/illustrations/ai_diagnosis_spark.png');
      expect(AppAssets.emptyRequests, 'assets/images/illustrations/empty_requests.png');
      expect(AppAssets.locationPin, 'assets/images/illustrations/location_pin.png');
      expect(AppAssets.readyForMatching, 'assets/images/illustrations/ready_for_matching.png');
    });
  });

  group('AppImageAsset Widget', () {
    testWidgets('renders local asset image with specified dimensions and semantics',
        (tester) async {
      await tester.pumpWidget(
        _buildTestable(
          const AppImageAsset(
            assetPath: AppAssets.homeServiceHero,
            width: 160,
            height: 90,
            fit: BoxFit.cover,
            semanticLabel: 'Home Service Hero',
          ),
        ),
      );
      await tester.pump();

      final imageFinder = find.byType(Image);
      expect(imageFinder, findsOneWidget);

      final Image image = tester.widget(imageFinder);
      expect(image.width, 160);
      expect(image.height, 90);
      expect(image.fit, BoxFit.cover);
      expect(image.semanticLabel, 'Home Service Hero');

      // Verify semantics
      expect(find.bySemanticsLabel('Home Service Hero'), findsOneWidget);
    });

    testWidgets('falls back to icon safely without throwing when asset fails to load',
        (tester) async {
      await tester.pumpWidget(
        _buildTestable(
          const AppImageAsset(
            assetPath: 'assets/images/non_existent.jpg',
            width: 80,
            height: 80,
            fallbackIcon: Icons.handyman_rounded,
          ),
        ),
      );
      await tester.pump();

      // The image widget renders
      expect(find.byType(AppImageAsset), findsOneWidget);

      // Trigger error builder by checking fallback container
      final fallbackFinder = find.byIcon(Icons.handyman_rounded);
      expect(fallbackFinder, findsOneWidget);

      expect(tester.takeException(), isNull);
    });

    testWidgets('applies borderRadius with ClipRRect when specified',
        (tester) async {
      await tester.pumpWidget(
        _buildTestable(
          const AppImageAsset(
            assetPath: AppAssets.plumbingService,
            width: 48,
            height: 48,
            borderRadius: BorderRadius.all(Radius.circular(8)),
          ),
        ),
      );
      await tester.pump();

      expect(find.byType(ClipRRect), findsOneWidget);
      final ClipRRect clipRRect = tester.widget(find.byType(ClipRRect));
      expect(clipRRect.borderRadius, const BorderRadius.all(Radius.circular(8)));
    });
  });

  group('SectionHeader Widget', () {
    testWidgets('renders title and optional subtitle', (tester) async {
      await tester.pumpWidget(
        _buildTestable(
          const SectionHeader(
            title: 'Featured Services',
            subtitle: 'Choose a trade to get started quickly',
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Featured Services'), findsOneWidget);
      expect(find.text('Choose a trade to get started quickly'), findsOneWidget);
    });

    testWidgets('renders action widget alongside title with bounded layout',
        (tester) async {
      bool actionTapped = false;

      await tester.pumpWidget(
        _buildTestable(
          SectionHeader(
            title: 'My Service Requests',
            action: TextButton(
              onPressed: () => actionTapped = true,
              child: const Text('View All'),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('My Service Requests'), findsOneWidget);
      expect(find.text('View All'), findsOneWidget);

      await tester.tap(find.text('View All'));
      await tester.pump();

      expect(actionTapped, isTrue);
      expect(tester.takeException(), isNull);
    });
  });

  group('EmptyStateCard Widget', () {
    testWidgets('renders icon, title, description, and action button',
        (tester) async {
      bool buttonPressed = false;

      await tester.pumpWidget(
        _buildTestable(
          EmptyStateCard(
            title: 'No Service Requests Yet',
            description:
                'You have not created any service requests. Describe your problem to get started.',
            icon: Icons.assignment_outlined,
            actionText: 'Create Request',
            onAction: () => buttonPressed = true,
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('No Service Requests Yet'), findsOneWidget);
      expect(
        find.text(
            'You have not created any service requests. Describe your problem to get started.'),
        findsOneWidget,
      );
      expect(find.byIcon(Icons.assignment_outlined), findsOneWidget);
      expect(find.text('Create Request'), findsOneWidget);

      await tester.tap(find.text('Create Request'));
      await tester.pump();

      expect(buttonPressed, isTrue);
      expect(tester.takeException(), isNull);
    });

    testWidgets('renders with imageAsset and operates within safe finite constraints',
        (tester) async {
      tester.view.physicalSize = const Size(360, 640);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      await tester.pumpWidget(
        _buildTestable(
          const EmptyStateCard(
            title: 'Nothing Here',
            description: 'This is a test description for mobile viewport.',
            imageAsset: AppAssets.emptyRequests,
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Nothing Here'), findsOneWidget);
      expect(find.byType(AppImageAsset), findsOneWidget);
      expect(tester.takeException(), isNull);
    });
  });
}
