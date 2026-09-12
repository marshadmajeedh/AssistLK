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

void main() {
  for (final width in [320.0, 393.0, 700.0]) {
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
            'Unclassified',
            'Created',
            'Unknown',
            'Service preference: ',
            'AssistLK AI classification: ',
            'Not analyzed yet',
          ]) {
            expect(find.text(text), findsOneWidget);
          }
          expect(tester.takeException(), isNull);
          final analyze = find.text('Analyze with AssistLK AI');
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
  testWidgets('other detail categories remain without added artwork', (
    tester,
  ) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: RequestSummaryArtwork(
          category: 'Plumbing',
          child: Text('summary'),
        ),
      ),
    );
    expect(find.text('summary'), findsOneWidget);
    expect(find.byType(Image), findsNothing);
  });
}
