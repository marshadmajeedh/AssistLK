import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:mobile/features/auth/providers/auth_provider.dart';
import 'package:mobile/features/service_requests/models/analysis_visual_evidence.dart';
import 'package:mobile/features/service_requests/models/problem_analysis_summary_model.dart';
import 'package:mobile/features/service_requests/models/problem_understanding_result_model.dart';
import 'package:mobile/features/service_requests/models/service_request_model.dart';
import 'package:mobile/features/service_requests/models/service_request_status.dart';
import 'package:mobile/features/service_requests/models/service_request_urgency.dart';
import 'package:mobile/features/service_requests/providers/service_request_provider.dart';
import 'package:mobile/features/service_requests/screens/service_request_detail_screen.dart';
import 'package:mobile/features/service_requests/widgets/analysis_result_card.dart';
import 'package:mobile/features/service_requests/widgets/photo_insights.dart';
import 'package:mobile/shared/theme/app_theme.dart';

import 'mocks/fake_problem_photos.dart';

Map<String, dynamic> evidenceJson({
  String status = 'used',
  bool limitations = true,
}) => {
  'visionStatus': status,
  'attachmentIdsUsed': ['photo-1', 'photo-2'],
  'observations': [
    {
      'attachmentId': 'photo-1',
      'observation': 'Moisture appears visible near the pipe connection.',
    },
    {
      'attachmentId': 'photo-2',
      'observation': 'A puddle may be visible below the sink.',
    },
  ],
  'limitations': limitations
      ? ['The exact internal cause cannot be confirmed from the photo.']
      : [],
  'provider': 'gemini',
  'storageKey': '/private/photo.jpg',
  'dataBase64': 'PRIVATE_IMAGE_CONTENT',
};

class VisualPhotoApi extends PhotoApi {
  @override
  Future<ServiceRequestModel> getById(String id) async =>
      request(status: status).copyWith(
        latestAnalysis: ProblemAnalysisSummaryModel.fromJson({
          'id': 'analysis',
          'detectedProblem': 'Possible pipe leak.',
          'confidence': 0.85,
          'createdAt': '2026-09-13T09:00:00Z',
          'visualEvidence': evidenceJson(),
        }),
      );
}

void main() {
  for (final status in ['used', 'not_requested', 'unsupported', 'failed']) {
    test('model parses $status and round-trips safe fields', () {
      final model = ProblemAnalysisSummaryModel.fromJson({
        'visualEvidence': evidenceJson(status: status),
      });
      expect(model.visualEvidence.status.value, status);
      expect(
        model.visualEvidence.observations.length,
        status == 'used' ? 2 : 0,
      );
      final json = model.toJson().toString();
      for (final private in [
        'gemini',
        'storageKey',
        '/private',
        'dataBase64',
        'PRIVATE_IMAGE_CONTENT',
      ]) {
        expect(json.contains(private), isFalse);
      }
    });
  }
  test('legacy analysis needs no visual fields', () {
    expect(
      ProblemAnalysisSummaryModel.fromJson({}).visualEvidence.status,
      AnalysisVisionStatus.notRequested,
    );
  });
  test('unknown and partial statuses do not claim photo usage', () {
    for (final status in ['partial', 'invented']) {
      final value = AnalysisVisualEvidence.fromJson(
        evidenceJson(status: status),
      );
      expect(value.status, AnalysisVisionStatus.notRequested);
      expect(value.observations, isEmpty);
    }
  });
  test('malformed and untraceable observations are ignored', () {
    final json = evidenceJson();
    json['observations'] = [
      null,
      {'attachmentId': 'unknown', 'observation': 'Unknown photo.'},
      {'attachmentId': 'photo-1', 'observation': 2},
    ];
    expect(AnalysisVisualEvidence.fromJson(json).observations, isEmpty);
    expect(
      AnalysisVisualEvidence.fromJson('bad').status,
      AnalysisVisionStatus.notRequested,
    );
  });

  Widget insights(
    String status, {
    bool hasPhotos = true,
    bool limitations = true,
  }) => MaterialApp(
    theme: AppTheme.lightTheme,
    home: Scaffold(
      body: PhotoInsights(
        evidence: AnalysisVisualEvidence.fromJson(
          evidenceJson(status: status, limitations: limitations),
        ),
        hasPhotos: hasPhotos,
      ),
    ),
  );
  testWidgets('used renders cautious observations and useful limitations', (
    tester,
  ) async {
    await tester.pumpWidget(insights('used'));
    expect(find.text('Photo evidence used'), findsOneWidget);
    expect(
      find.text('Moisture appears visible near the pipe connection.'),
      findsOneWidget,
    );
    expect(
      find.text('A puddle may be visible below the sink.'),
      findsOneWidget,
    );
    expect(find.text('Photo limitations'), findsOneWidget);
    expect(
      find.text('The exact internal cause cannot be confirmed from the photo.'),
      findsOneWidget,
    );
    for (final private in [
      'used',
      'visionStatus',
      'multimodal',
      'gemini',
      'openai',
      'Offline provider',
      '/private/photo.jpg',
      'PRIVATE_IMAGE_CONTENT',
    ]) {
      expect(find.text(private), findsNothing);
    }
  });
  testWidgets('no empty limitations heading', (tester) async {
    await tester.pumpWidget(insights('used', limitations: false));
    expect(find.text('Photo limitations'), findsNothing);
  });
  testWidgets('not requested adds no noise', (tester) async {
    await tester.pumpWidget(insights('not_requested'));
    expect(find.text('Photo evidence used'), findsNothing);
    expect(find.byType(Icon), findsNothing);
  });
  testWidgets(
    'unsupported explains attached photos without provider terminology',
    (tester) async {
      await tester.pumpWidget(insights('unsupported'));
      expect(
        find.text('Photos were attached but were not used in this analysis.'),
        findsOneWidget,
      );
      expect(find.text('Photo evidence used'), findsNothing);
    },
  );
  testWidgets('unsupported hidden if photos are absent', (tester) async {
    await tester.pumpWidget(insights('unsupported', hasPhotos: false));
    expect(
      find.text('Photos were attached but were not used in this analysis.'),
      findsNothing,
    );
  });
  testWidgets('explicit failed result never claims photo use', (tester) async {
    await tester.pumpWidget(insights('failed'));
    expect(
      find.text('Photos could not be used in this analysis.'),
      findsOneWidget,
    );
    expect(
      find.text('Moisture appears visible near the pipe connection.'),
      findsNothing,
    );
  });
  testWidgets('screen readers receive findings without decorative labels', (
    tester,
  ) async {
    final semantics = tester.ensureSemantics();
    await tester.pumpWidget(insights('used'));
    expect(find.bySemanticsLabel('Photo evidence used'), findsOneWidget);
    expect(
      find.bySemanticsLabel(
        'Moisture appears visible near the pipe connection.',
      ),
      findsOneWidget,
    );
    expect(find.bySemanticsLabel('camera icon'), findsNothing);
    semantics.dispose();
  });

  for (final width in [320.0, 412.0]) {
    for (final scale in [1.0, 2.0]) {
      testWidgets('AI card wraps bounded text at $width and scale $scale', (
        tester,
      ) async {
        tester.view.physicalSize = Size(width, 900);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        final long = List.filled(12, 'Possible moisture.').join(' ');
        await tester.pumpWidget(
          MaterialApp(
            theme: AppTheme.lightTheme,
            home: Scaffold(
              body: MediaQuery(
                data: MediaQueryData(textScaler: TextScaler.linear(scale)),
                child: SingleChildScrollView(
                  child: AnalysisResultCard(
                    analysis: ProblemUnderstandingResultModel(
                      workflowId: '',
                      executionId: '',
                      serviceRequestId: 'request-1',
                      status: ServiceRequestStatus.analyzed,
                      category: 'Plumbing',
                      problemSummary: 'Possible leak.',
                      urgency: ServiceRequestUrgency.medium,
                      confidence: .85,
                      needsMoreInformation: false,
                      followUpQuestions: const [],
                    ),
                    visualEvidence: AnalysisVisualEvidence(
                      status: AnalysisVisionStatus.used,
                      attachmentIdsUsed: const ['photo-1'],
                      observations: [AnalysisPhotoObservation('photo-1', long)],
                      limitations: [long],
                    ),
                  ),
                ),
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();
        expect(find.text(long), findsNWidgets(2));
        expect(tester.takeException(), isNull);
      });
    }
  }

  for (final (status, width, scale) in [
    (ServiceRequestStatus.analyzed, 320.0, 1.0),
    (ServiceRequestStatus.analyzed, 320.0, 2.0),
    (ServiceRequestStatus.analyzed, 412.0, 1.0),
    (ServiceRequestStatus.analyzed, 412.0, 2.0),
    (ServiceRequestStatus.awaitingInformation, 320.0, 2.0),
    (ServiceRequestStatus.cancelled, 320.0, 2.0),
    (ServiceRequestStatus.readyForMatching, 320.0, 2.0),
  ]) {
    testWidgets(
      'detail uses persisted insights in $status at $width/$scale without extra downloads',
      (tester) async {
        tester.view.physicalSize = Size(width, 900);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        final api = VisualPhotoApi()..status = status;
        api.rows.add(attachment(1));
        final provider = ServiceRequestProvider(serviceRequestService: api);
        final auth = PhotoAuth();
        await tester.pumpWidget(
          MultiProvider(
            providers: [
              ChangeNotifierProvider<ServiceRequestProvider>.value(
                value: provider,
              ),
              ChangeNotifierProvider<AuthProvider>.value(value: auth),
            ],
            child: MaterialApp(
              theme: AppTheme.lightTheme,
              builder: (context, child) => MediaQuery(
                data: MediaQuery.of(context)
                    .copyWith(textScaler: TextScaler.linear(scale)),
                child: child!,
              ),
              home: ServiceRequestDetailScreen(
                requestId: 'request-1',
                imagePicker: FakePhotoPicker(),
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();
        await tester.ensureVisible(find.text('Photo evidence used'));
        await tester.pumpAndSettle();
        expect(find.text('Photo evidence used'), findsOneWidget);
        final downloads = api.events
            .where((e) => e.startsWith('content:'))
            .length;
        provider.notifyListeners();
        await tester.pumpAndSettle();
        expect(
          api.events.where((e) => e.startsWith('content:')).length,
          downloads,
        );
        expect(tester.takeException(), isNull);
        await tester.pumpWidget(const SizedBox());
        provider.dispose();
        auth.dispose();
      },
    );
  }
}
