import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:mobile/core/api/api_client.dart';
import 'package:mobile/features/service_requests/models/service_request_model.dart';
import 'package:mobile/features/service_requests/models/service_request_urgency.dart';
import 'package:mobile/features/service_requests/providers/service_request_provider.dart';
import 'package:mobile/features/service_requests/screens/service_request_detail_screen.dart';
import 'package:mobile/features/service_requests/widgets/request_summary_artwork.dart';
import 'package:mobile/shared/theme/app_assets.dart';
import 'package:mobile/shared/theme/app_theme.dart';

import 'session_foundation_test.dart' show RequestsStub, MemoryStorage, request;

/**
 * TEST SUITE: RequestSummaryArtwork and Detail Presentation Tests
 *
 * 1. What this test suite tests:
 *    - Verifies the `RequestSummaryArtwork` wrapper widget used on the `ServiceRequestDetailScreen` summary header.
 *    - Tests responsive rendering across 4 screen widths (320px compact, 393px standard, 412px large, 700px tablet) and 2 text scales (1.0x, 2.0x accessibility).
 *    - Confirms triggering "Analyze Request with AssistLK AI" directly from the summary screen.
 *    - Tests category-to-asset mapping for all supported categories (Unclassified, Plumbing, Electrical, Vehicle Repair, Vehicle Assistance alias, Appliance Repair).
 *    - Tests unsupported category fallback (renders child without any decorative Image widget).
 * 2. Why these tests are needed:
 *    - Detail screen header provides the visual summary of the service request. The decorative artwork must fit within bounded dimensions (48-80px width),
 *      must not block touch events, must not repeat semantic text to screen readers, and must not break layout under extreme text zoom or narrow mobile widths.
 * 3. What is real and what is mocked:
 *    - Real Flutter widgets (`ServiceRequestDetailScreen`, `RequestSummaryArtwork`, `ServiceRequestProvider`).
 *    - Mocked: `RequestsStub` provides in-memory `ServiceRequestModel` detail and handles analysis calls.
 * 4. Prerequisites: None.
 * 5. PowerShell command to run this suite from repository root:
 *    Push-Location mobile; flutter test test/request_summary_artwork_test.dart; Pop-Location
 */
void main() {
  /**
   * Parameterized Responsive Test Loops: Viewport Width x Text Scale Factor
   * Note: Parameterized across viewport widths (320.0, 393.0, 412.0, 700.0) and text scales (1.0, 2.0).
   * The test runner executes all 8 combinations.
   *
   * Purpose: What this specific test checks
   * - Verifies `RequestSummaryArtwork` sizing, semantics exclusion, IgnorePointer pass-through, and interactive "Analyze" button execution across responsive viewports.
   *
   * Why: Why this scenario matters
   * - Verifies that even on small devices with high text zoom, the summary artwork scales safely and the customer can tap "Analyze Request with AssistLK AI".
   *
   * Scenario: Input or condition being tested
   * - Detail screen loaded with an unclassified request under various width/scale combinations.
   *
   * Pass condition: What result or assertion makes it pass
   * - Image renders with `AppAssets.unclassifiedService`, width between 48 and 80px, `excludeFromSemantics == true`, tapping Analyze increments `analysisCalls` to 1.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/request_summary_artwork_test.dart --plain-name "Unclassified detail artwork fits 393.0 at 1.0 and Analyze works"; Pop-Location
   */
  for (final width in [320.0, 393.0, 412.0, 700.0]) {
    for (final scale in [1.0, 2.0]) {
      testWidgets(
        'Unclassified detail artwork fits $width at $scale and Analyze works',
        (tester) async {
          tester.view.physicalSize = Size(width, 900);
          tester.view.devicePixelRatio = 1;
          tester.platformDispatcher.textScaleFactorTestValue = scale;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);
          addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
          final service = RequestsStub(ApiClient(tokenStorage: MemoryStorage()))
            ..detail = Completer<ServiceRequestModel>()
            ..analysis = Completer();
          service.detail!.complete(
            request('detail').copyWith(
              category: 'Unclassified',
              urgency: ServiceRequestUrgency.unknown,
            ),
          );
          final provider = ServiceRequestProvider(
            serviceRequestService: service,
          );
          await tester.pumpWidget(
            ChangeNotifierProvider.value(
              value: provider,
              child: MaterialApp(
                theme: AppTheme.lightTheme,
                home: const ServiceRequestDetailScreen(requestId: 'detail'),
              ),
            ),
          );
          await tester.pumpAndSettle();
          final art = find.descendant(
            of: find.byType(RequestSummaryArtwork),
            matching: find.byType(Image),
          );
          expect(art, findsOneWidget);
          final image = tester.widget<Image>(art);
          expect(
            (image.image as AssetImage).assetName,
            AppAssets.unclassifiedService,
          );
          expect(image.excludeFromSemantics, isTrue);
          expect(image.fit, BoxFit.contain);
          expect(tester.getSize(art).width, inInclusiveRange(48, 80));
          expect(
            tester
                .widget<IgnorePointer>(
                  find
                      .ancestor(of: art, matching: find.byType(IgnorePointer))
                      .first,
                )
                .ignoring,
            isTrue,
          );
          for (final text in [
            'Created',
            'Service preference: ',
            'AssistLK AI classification: ',
            'Urgency pending',
          ]) {
            expect(find.text(text), findsOneWidget);
          }
          expect(find.text('Pending AI analysis'), findsWidgets);
          expect(tester.takeException(), isNull);
          final analyze = find.text('Analyze Request with AssistLK AI');
          await tester.ensureVisible(analyze);
          await tester.tap(analyze);
          await tester.pump();
          expect(service.analysisCalls, 1);
          expect(tester.takeException(), isNull);
          await tester.pumpWidget(const SizedBox.shrink());
          provider.dispose();
        },
      );
    }
  }

  const supportedArtwork = {
    'Unclassified': AppAssets.unclassifiedService,
    'Plumbing': AppAssets.plumbingService,
    'Electrical': AppAssets.electricalService,
    'Vehicle Repair': AppAssets.vehicleService,
    'Vehicle Assistance': AppAssets.vehicleService,
    'Appliance Repair': AppAssets.applianceService,
  };

  /**
   * Parameterized Test Loop: Supported Category Artwork Mapping
   * Note: Parameterized across all supported artwork map entries (Unclassified, Plumbing, Electrical, Vehicle Repair, Vehicle Assistance, Appliance Repair).
   * The test runner executes each combination.
   *
   * Purpose: What this specific test checks
   * - Verifies that `RequestSummaryArtwork` displays the correct asset for each canonical category and legacy alias ('Vehicle Assistance').
   *
   * Why: Why this scenario matters
   * - Guarantees visual consistency across categories on the service request detail screen.
   *
   * Scenario: Input or condition being tested
   * - Wrap summary text with `RequestSummaryArtwork` for each category.
   *
   * Pass condition: What result or assertion makes it pass
   * - Exactly one Image widget found with the corresponding asset path, child text rendered.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/request_summary_artwork_test.dart --plain-name "detail category Plumbing renders expected decorative artwork"; Pop-Location
   */
  for (final entry in supportedArtwork.entries) {
    testWidgets('detail category ${entry.key} renders expected decorative artwork', (
      tester,
    ) async {
      await tester.pumpWidget(
        MaterialApp(
          home: RequestSummaryArtwork(
            category: entry.key,
            child: const Text('summary content'),
          ),
        ),
      );
      expect(find.text('summary content'), findsOneWidget);
      final art = find.byType(Image);
      expect(art, findsOneWidget);
      final image = tester.widget<Image>(art);
      expect((image.image as AssetImage).assetName, entry.value);
    });
  }

  /**
   * Purpose: What this specific test checks
   * - Verifies fallback when an unexpected or unsupported category string is provided.
   *
   * Why: Why this scenario matters
   * - Protects against missing asset errors or blank image placeholders.
   *
   * Scenario: Input or condition being tested
   * - Category set to `'Unexpected Category'`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Child text renders, no Image widget is created.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/request_summary_artwork_test.dart --plain-name "unsupported detail categories remain without added artwork"; Pop-Location
   */
  testWidgets('unsupported detail categories remain without added artwork', (
    tester,
  ) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: RequestSummaryArtwork(
          category: 'Unexpected Category',
          child: Text('summary'),
        ),
      ),
    );
    expect(find.text('summary'), findsOneWidget);
    expect(find.byType(Image), findsNothing);
  });
}
