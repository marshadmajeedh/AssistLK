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

void main() {
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
