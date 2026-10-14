import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/shared/theme/app_assets.dart';
import 'package:mobile/shared/theme/app_theme.dart';
import 'package:mobile/features/service_requests/widgets/service_request_card.dart';
import 'package:mobile/features/service_requests/widgets/service_request_category_asset.dart';
import 'package:mobile/features/service_requests/widgets/status_badge.dart';
import 'package:mobile/features/service_requests/widgets/urgency_chip.dart';

import 'session_foundation_test.dart' show request;

const artwork = {
  'Unclassified': AppAssets.unclassifiedService,
  'Plumbing': AppAssets.plumbingService,
  'Electrical': AppAssets.electricalService,
  'Vehicle Repair': AppAssets.vehicleService,
  'Appliance Repair': AppAssets.applianceService,
};

/**
 * TEST SUITE: Service Request Artwork and Card Presentation Tests
 *
 * 1. What this test suite tests:
 *    - Verifies category artwork asset resolution (`serviceRequestCategoryAsset`), watermark rendering, semantics exclusion, and hit-test propagation in `ServiceRequestCard`.
 *    - Confirms decorative artwork mapping across all categories (Unclassified, Plumbing, Electrical, Vehicle Repair, Appliance Repair).
 *    - Confirms fallback behavior for unknown/unsupported categories (no image rendered, card remains tappable).
 *    - Confirms responsive layout stability across small/standard viewport widths (320px, 393px) and high text scaling (1.0x, 2.0x).
 * 2. Why these tests are needed:
 *    - Ensures visual polish and accessibility: decorative artwork must not intercept touch gestures (must be wrapped in IgnorePointer),
 *      must not clutter screen readers (must be ExcludeSemantics), and must scale gracefully without breaking card layouts on diverse mobile screen sizes.
 * 3. What is real and what is mocked:
 *    - Real Flutter widgets (`ServiceRequestCard`, `AppCard`, `Image.asset`, `ShaderMask`, `IgnorePointer`).
 *    - No network mocks needed (uses in-memory `ServiceRequestModel`).
 * 4. Prerequisites: None.
 * 5. PowerShell command to run this suite from repository root:
 *    Push-Location mobile; flutter test test/service_request_artwork_test.dart; Pop-Location
 */
void main() {
  /**
   * Parameterized Test Loop: Category Artwork & Metadata
   * Note: Parameterized across all artwork map entries ('Unclassified', 'Plumbing', 'Electrical', 'Vehicle Repair', 'Appliance Repair').
   * The test runner executes each category combination.
   *
   * Purpose: What this specific test checks
   * - Verifies decorative category artwork image, semantics exclusion, tap passthrough via IgnorePointer, and request metadata (category, description, date, status, urgency).
   *
   * Why: Why this scenario matters
   * - Guarantees artwork visually enriches cards without interfering with user interaction or accessibility.
   *
   * Scenario: Input or condition being tested
   * - Render `ServiceRequestCard` for each service category.
   *
   * Pass condition: What result or assertion makes it pass
   * - Correct asset is mapped, `excludeFromSemantics == true`, `IgnorePointer.ignoring == true`, metadata widgets render, tap on image triggers card `onTap`.
   *   (Note: 'Unclassified' tests UI label expectation discrepancy where UI renders 'Pending AI analysis').
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_artwork_test.dart --plain-name "Plumbing uses decorative artwork and preserves request information"; Pop-Location
   */
  for (final entry in artwork.entries) {
    testWidgets(
      '${entry.key} uses decorative artwork and preserves request information',
      (tester) async {
        var taps = 0;
        final model = request('artwork').copyWith(category: entry.key);
        await tester.pumpWidget(
          MaterialApp(
            theme: AppTheme.lightTheme,
            home: Scaffold(
              body: ServiceRequestCard(request: model, onTap: () => taps++),
            ),
          ),
        );
        await tester.pumpAndSettle();
        expect(serviceRequestCategoryAsset(entry.key), entry.value);
        final image = tester.widget<Image>(find.byType(Image));
        expect((image.image as AssetImage).assetName, entry.value);
        expect(image.excludeFromSemantics, isTrue);
        expect(
          tester.widget<IgnorePointer>(find.ancestor(
            of: find.byType(Image),
            matching: find.byType(IgnorePointer),
          ).first).ignoring,
          isTrue,
        );
        expect(find.text(entry.key), findsOneWidget);
        expect(find.text(model.description), findsOneWidget);
        expect(find.text('2026-01-01'), findsOneWidget);
        expect(
          tester.widget<StatusBadge>(find.byType(StatusBadge)).status,
          model.status,
        );
        expect(
          tester.widget<UrgencyChip>(find.byType(UrgencyChip)).urgency,
          model.urgency,
        );
        expect(find.text('Created'), findsOneWidget);
        await tester.tapAt(tester.getCenter(find.byType(Image)));
        expect(taps, 1);
        expect(tester.takeException(), isNull);
      },
    );
  }

  /**
   * Purpose: What this specific test checks
   * - Verifies fallback handling when a service request has an unrecognized or arbitrary category name.
   *
   * Why: Why this scenario matters
   * - Protects against missing asset crashes or broken layouts if a new category is introduced on the backend.
   *
   * Scenario: Input or condition being tested
   * - Category set to `'Unexpected category'`.
   *
   * Pass condition: What result or assertion makes it pass
   * - `serviceRequestCategoryAsset` returns null, no Image widget is found, card remains tappable.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_artwork_test.dart --plain-name "unsupported category has no image and remains tappable"; Pop-Location
   */
  testWidgets('unsupported category has no image and remains tappable', (
    tester,
  ) async {
    var taps = 0;
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: ServiceRequestCard(
            request: request('unknown')
                .copyWith(category: 'Unexpected category'),
            onTap: () => taps++,
          ),
        ),
      ),
    );
    expect(serviceRequestCategoryAsset('Unexpected category'), isNull);
    expect(find.byType(Image), findsNothing);
    await tester.tap(find.byType(ServiceRequestCard));
    expect(taps, 1);
    expect(tester.takeException(), isNull);
  });

  /**
   * Parameterized Test Loops: Viewport Width x Text Scale Factor
   * Note: Parameterized across viewport widths (320.0, 393.0) and text scale factors (1.0, 2.0).
   * The test runner executes all 4 combinations.
   *
   * Purpose: What this specific test checks
   * - Verifies responsive layout and text overflow protection under extreme text scaling and compact viewports.
   *
   * Why: Why this scenario matters
   * - Ensures accessibility compliance (WCAG) for users with enlarged text settings without visual clipping or overflow errors.
   *
   * Scenario: Input or condition being tested
   * - Test widths 320 and 393 at text scale 1.0 and 2.0 across all categories.
   *
   * Pass condition: What result or assertion makes it pass
   * - Stack height matches Column height, image width is ~33% of stack, category heading does not exceed max lines, no exceptions thrown.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_artwork_test.dart --plain-name "artwork preserves card layout at 320.0 scale 1.0"; Pop-Location
   */
  for (final width in [320.0, 393.0]) {
    for (final scale in [1.0, 2.0]) {
      testWidgets('artwork preserves card layout at $width scale $scale', (
        tester,
      ) async {
        tester.view.physicalSize = Size(width, 852);
        tester.view.devicePixelRatio = 1;
        tester.platformDispatcher.textScaleFactorTestValue = scale;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
        for (final category in artwork.keys) {
          await tester.pumpWidget(
            MaterialApp(
              theme: AppTheme.lightTheme,
              home: Scaffold(
                body: SingleChildScrollView(
                  padding: const EdgeInsets.all(24),
                  child: ServiceRequestCard(
                    request: request('layout').copyWith(category: category),
                  ),
                ),
              ),
            ),
          );
          await tester.pumpAndSettle();
          final stack = find
              .descendant(
                of: find.byType(ServiceRequestCard),
                matching: find.byType(Stack),
              )
              .first;
          final column = find
              .descendant(of: stack, matching: find.byType(Column))
              .first;
          expect(tester.getSize(stack).height, tester.getSize(column).height);
          expect(
            tester.getSize(find.byType(Image)).width,
            closeTo(tester.getSize(stack).width * .33, .1),
          );
          expect(
            tester
                .renderObject<RenderParagraph>(find.text(category))
                .didExceedMaxLines,
            isFalse,
          );
          expect(tester.takeException(), isNull);
        }
      });
    }
  }
}
