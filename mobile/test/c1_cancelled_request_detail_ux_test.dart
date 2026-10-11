import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:mobile/core/api/api_client.dart';
import 'package:mobile/features/auth/providers/auth_provider.dart';
import 'package:mobile/features/service_requests/models/analysis_visual_evidence.dart';
import 'package:mobile/features/service_requests/models/problem_analysis_summary_model.dart';
import 'package:mobile/features/service_requests/models/service_request_model.dart';
import 'package:mobile/features/service_requests/models/service_request_status.dart';
import 'package:mobile/features/service_requests/models/service_request_urgency.dart';
import 'package:mobile/features/service_requests/providers/service_request_provider.dart';
import 'package:mobile/features/service_requests/screens/service_request_detail_screen.dart';
import 'package:mobile/features/service_requests/widgets/analysis_result_card.dart';
import 'package:mobile/features/service_requests/widgets/status_badge.dart';
import 'package:mobile/shared/theme/app_colors.dart';
import 'package:mobile/shared/theme/app_theme.dart';

import 'session_foundation_test.dart'
    show MemoryStorage, AuthStub, RequestsStub, request;

class CancelledRequestsStub extends RequestsStub {
  CancelledRequestsStub(super.apiClient);

  bool simulateApiFailure = false;
  String? apiFailureMessage;

  @override
  Future<ServiceRequestModel> getById(String id) async {
    if (simulateApiFailure) {
      throw Exception(apiFailureMessage ?? 'Failed to load request: 404 Not Found');
    }
    return items.firstWhere(
      (item) => item.serviceRequestId == id,
      orElse: () => request(id),
    );
  }
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  late MemoryStorage storage;
  late ApiClient api;
  late AuthStub authService;
  late AuthProvider auth;
  late CancelledRequestsStub requests;

  setUp(() {
    storage = MemoryStorage();
    api = ApiClient(tokenStorage: storage);
    authService = AuthStub(api);
    auth = AuthProvider(authService: authService, tokenStorage: storage);
    requests = CancelledRequestsStub(api);
  });

  Widget buildApp(Widget child) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider<AuthProvider>.value(value: auth),
        ChangeNotifierProvider<ServiceRequestProvider>(
          create: (_) => ServiceRequestProvider(serviceRequestService: requests),
        ),
      ],
      child: MaterialApp(
        theme: AppTheme.lightTheme,
        home: child,
      ),
    );
  }

  group('C1 Cancelled Request Detail UX', () {
    testWidgets(
      '1. Cancelled request without analysis: loads successfully, shows neutral notice, preserves info, hides actions & error banner',
      (tester) async {
        final cancelledReq = ServiceRequestModel(
          serviceRequestId: 'req-cancelled-1',
          customerId: 'cust-1',
          category: 'Plumbing',
          description: 'Water pipe burst under kitchen sink',
          locationText: '123 Galle Road, Colombo 03',
          urgency: ServiceRequestUrgency.high,
          status: ServiceRequestStatus.cancelled,
          createdAt: DateTime(2026, 10, 1),
          updatedAt: DateTime(2026, 10, 2),
        );
        requests.items = [cancelledReq];

        await tester.pumpWidget(
          buildApp(const ServiceRequestDetailScreen(requestId: 'req-cancelled-1')),
        );
        await tester.pumpAndSettle();

        // 1. Loads successfully
        expect(find.byType(ServiceRequestDetailScreen), findsOneWidget);
        expect(find.text('Request Details'), findsOneWidget);

        // 2. Cancelled status badge is shown
        expect(find.byType(StatusBadge), findsOneWidget);
        expect(find.text('Cancelled'), findsOneWidget);

        // 3. Description remains visible
        expect(find.text('Water pipe burst under kitchen sink'), findsOneWidget);

        // 4. Location remains visible
        expect(find.text('123 Galle Road, Colombo 03'), findsOneWidget);

        // 6. No Analyze action is shown
        expect(find.text('Analyze Request with AssistLK AI'), findsNothing);
        expect(find.text('Analyze with AssistLK AI'), findsNothing);

        // 7. No Edit action is shown
        expect(find.text('Edit Details'), findsNothing);
        expect(find.text('Edit Request'), findsNothing);

        // 8. No Cancel action is shown in app bar or body
        expect(find.byIcon(Icons.cancel_outlined), findsNothing);
        expect(find.byTooltip('Cancel Request'), findsNothing);

        // 9. No ReadyForMatching action is shown
        expect(find.text('Mark Ready for Matching'), findsNothing);

        // 10. Neutral "Request cancelled" notice is shown
        expect(find.text('Request cancelled'), findsOneWidget);
        expect(
          find.text('This request is closed and no further actions are available.'),
          findsOneWidget,
        );
        expect(find.byIcon(Icons.info_outline_rounded), findsOneWidget);

        // 11. Generic red destructive error banner is NOT shown solely because status is Cancelled
        expect(
          find.text('This service request has been cancelled.'),
          findsNothing,
        );
        expect(find.byIcon(Icons.cancel_rounded), findsNothing);
      },
    );

    testWidgets(
      '2. Cancelled request with existing AI analysis: preserves analysis card, shows neutral notice, suppresses actions',
      (tester) async {
        final cancelledWithAnalysis = ServiceRequestModel(
          serviceRequestId: 'req-cancelled-analysis',
          customerId: 'cust-1',
          category: 'Plumbing',
          description: 'Main valve leaking heavily',
          locationText: '45 Peradeniya Road, Kandy',
          urgency: ServiceRequestUrgency.critical,
          status: ServiceRequestStatus.cancelled,
          createdAt: DateTime(2026, 10, 1),
          updatedAt: DateTime(2026, 10, 2),
          latestAnalysis: ProblemAnalysisSummaryModel(
            id: 'ana-1',
            detectedProblem: 'Severe plumbing leak at main intake valve',
            confidence: 0.94,
            agentName: 'AssistLK AI',
            createdAt: DateTime(2026, 10, 1),
            visualEvidence: const AnalysisVisualEvidence(),
          ),
        );
        requests.items = [cancelledWithAnalysis];

        await tester.pumpWidget(
          buildApp(
            const ServiceRequestDetailScreen(requestId: 'req-cancelled-analysis'),
          ),
        );
        await tester.pumpAndSettle();

        // 1. Loads successfully
        expect(find.byType(ServiceRequestDetailScreen), findsOneWidget);

        // 2. Status badge is Cancelled
        expect(find.text('Cancelled'), findsOneWidget);

        // 3. Description remains visible
        expect(find.text('Main valve leaking heavily'), findsOneWidget);

        // 4. Location remains visible
        expect(find.text('45 Peradeniya Road, Kandy'), findsOneWidget);

        // 5. Existing AI analysis remains visible when present
        expect(find.byType(AnalysisResultCard), findsOneWidget);
        expect(find.text('AssistLK AI Analysis'), findsOneWidget);
        expect(
          find.text('Severe plumbing leak at main intake valve'),
          findsOneWidget,
        );
        expect(find.text('94% Confidence'), findsOneWidget);

        // 6-9. No action buttons
        expect(find.text('Analyze Request with AssistLK AI'), findsNothing);
        expect(find.text('Edit Details'), findsNothing);
        expect(find.byIcon(Icons.cancel_outlined), findsNothing);
        expect(find.text('Mark Ready for Matching'), findsNothing);

        // 10. Neutral "Request cancelled" notice is shown
        expect(find.text('Request cancelled'), findsOneWidget);
        expect(
          find.text('This request is closed and no further actions are available.'),
          findsOneWidget,
        );

        // 11. Generic red destructive error banner is NOT shown
        expect(
          find.text('This service request has been cancelled.'),
          findsNothing,
        );
        expect(find.byIcon(Icons.cancel_rounded), findsNothing);
      },
    );

    testWidgets(
      '3. Actual API failure still renders the real error state with red error icon and message',
      (tester) async {
        requests.simulateApiFailure = true;
        requests.apiFailureMessage = 'Exception: Network timeout occurred';

        await tester.pumpWidget(
          buildApp(const ServiceRequestDetailScreen(requestId: 'req-err-404')),
        );
        await tester.pumpAndSettle();

        // 12. Actual API failure still renders the real error state
        expect(find.byIcon(Icons.error_outline_rounded), findsOneWidget);
        final iconWidget = tester.widget<Icon>(find.byIcon(Icons.error_outline_rounded));
        expect(iconWidget.color, AppColors.error);

        expect(find.text('Exception: Network timeout occurred'), findsOneWidget);
        expect(find.text('Back to Home'), findsOneWidget);

        // Cancelled notice must NOT be shown when actual API error occurs
        expect(find.text('Request cancelled'), findsNothing);
      },
    );
  });
}
