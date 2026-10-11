import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:mobile/app/app.dart';
import 'package:mobile/core/api/api_client.dart';
import 'package:mobile/core/auth/token_storage.dart';
import 'package:mobile/features/auth/models/auth_result.dart';
import 'package:mobile/features/auth/models/auth_user.dart';
import 'package:mobile/features/auth/providers/auth_provider.dart';
import 'package:mobile/features/auth/screens/login_screen.dart';
import 'package:mobile/features/auth/screens/account_type_selection_screen.dart';
import 'package:mobile/features/auth/screens/customer_register_screen.dart';
import 'package:mobile/features/auth/services/auth_service.dart';
import 'package:mobile/features/providers/provider_home_screen.dart';
import 'package:mobile/features/service_requests/models/service_request_model.dart';
import 'package:mobile/features/service_requests/models/problem_understanding_result_model.dart';
import 'package:mobile/features/service_requests/models/service_request_clarification_model.dart';
import 'package:mobile/features/service_requests/models/submit_clarification_answers_dto.dart';
import 'package:mobile/features/service_requests/providers/service_request_provider.dart';
import 'package:mobile/features/service_requests/screens/customer_home_screen.dart';
import 'package:mobile/features/service_requests/screens/create_service_request_screen.dart';
import 'package:mobile/features/service_requests/screens/service_request_detail_screen.dart';
import 'package:mobile/features/service_requests/screens/edit_service_request_screen.dart';
import 'package:mobile/features/service_requests/services/service_request_service.dart';

class MemoryStorage extends TokenStorage {
  String? token;
  @override
  Future<String?> getToken() async => token;
  @override
  Future<void> saveToken(String token) async {
    this.token = token;
  }

  @override
  Future<void> deleteToken() async {
    token = null;
  }
}

class AuthStub extends AuthService {
  AuthStub(ApiClient apiClient) : super(apiClient: apiClient);
  AuthUser nextUser = user('A');
  Completer<AuthUser>? restoration;
  static AuthUser user(String id, [String role = 'Customer']) => AuthUser(
    userId: id,
    fullName: 'Customer $id',
    email: '$id@example.com',
    role: role,
  );
  @override
  Future<AuthUser> getMe() async =>
      restoration == null ? nextUser : await restoration!.future;
  AuthResult get result => AuthResult(
    user: nextUser,
    token: 'token-${nextUser.userId}',
    expiresAtUtc: DateTime(2030),
  );
  @override
  Future<AuthResult> login({
    required String email,
    required String password,
  }) async => result;
  @override
  Future<AuthResult> register({
    required String fullName,
    required String email,
    required String password,
    required String phoneNumber,
    required String role,
  }) async => result;
}

ServiceRequestModel request(String id) => ServiceRequestModel.fromJson({
  'serviceRequestId': id,
  'customerId': id,
  'description': 'Leaking kitchen pipe $id',
  'category': 'Plumbing',
  'locationText': 'Colombo',
  'status': 'Created',
  'urgency': 'Low',
  'createdAt': '2026-01-01T00:00:00Z',
  'updatedAt': '2026-01-01T00:00:00Z',
});

class RequestsStub extends ServiceRequestService {
  RequestsStub(ApiClient apiClient) : super(apiClient: apiClient);
  Completer<ProblemUnderstandingResultModel>? analysis;
  Completer<List<ServiceRequestClarificationModel>>? answers;
  int analysisCalls = 0;
  int detailCalls = 0;
  @override
  Future<ProblemUnderstandingResultModel> analyze(String id) {
    analysisCalls++;
    return analysis!.future;
  }

  @override
  Future<List<ServiceRequestClarificationModel>> submitClarificationAnswers(
    String id,
    SubmitClarificationAnswersDto dto,
  ) => answers!.future;
  List<ServiceRequestModel> items = [request('A')];
  Completer<List<ServiceRequestModel>>? pending;
  Completer<ServiceRequestModel>? detail;
  @override
  Future<List<ServiceRequestModel>> getMyRequests() async =>
      pending == null ? items : await pending!.future;
  @override
  Future<ServiceRequestModel> getById(String id) async {
    detailCalls++;
    return detail == null ? request(id) : await detail!.future;
  }
}

class ReplyAdapter implements HttpClientAdapter {
  final Future<ResponseBody> Function(RequestOptions) reply;
  ReplyAdapter(this.reply);
  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) => reply(options);
  @override
  void close({bool force = false}) {}
}

void main() {
  late MemoryStorage storage;
  late ApiClient api;
  late AuthStub service;
  late AuthProvider auth;
  late RequestsStub requests;
  setUp(() {
    storage = MemoryStorage();
    api = ApiClient(tokenStorage: storage);
    service = AuthStub(api);
    auth = AuthProvider(authService: service, tokenStorage: storage);
    requests = RequestsStub(api);
  });
  Future<void> login([String id = 'A', String role = 'Customer']) async {
    service.nextUser = AuthStub.user(id, role);
    await auth.login(email: '$id@example.com', password: 'password');
  }

  Future<void> mount(WidgetTester tester) => tester.pumpWidget(
    ChangeNotifierProvider<AuthProvider>.value(
      value: auth,
      child: AssistLKApp(serviceRequestService: requests),
    ),
  );

  testWidgets('unauthenticated startup finishes at Login', (tester) async {
    final initialized = auth.initialize();
    await mount(tester);
    await initialized;
    await tester.pumpAndSettle();
    expect(find.byType(LoginScreen), findsOneWidget);
    expect(auth.isInitializing, isFalse);
  });

  for (final role in ['Customer', 'Provider']) {
    testWidgets('restoration notifies loading completion and routes $role', (
      tester,
    ) async {
      storage.token = 'saved';
      service.restoration = Completer<AuthUser>();
      int notifications = 0;
      auth.addListener(() => notifications++);
      final initialized = auth.initialize();
      await mount(tester);
      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      service.restoration!.complete(AuthStub.user('A', role));
      await initialized;
      await tester.pumpAndSettle();
      expect(notifications, 2);
      expect(
        find.byType(
          role == 'Customer' ? CustomerHomeScreen : ProviderHomeScreen,
        ),
        findsOneWidget,
      );
      if (role == 'Provider') {
        expect(find.byType(CustomerHomeScreen), findsNothing);
        expect(
          Provider.of<ServiceRequestProvider?>(
            tester.element(find.byType(ProviderHomeScreen)),
            listen: false,
          ),
          isNull,
        );
      }
    });
  }

  test('failed restoration clears token and finishes loading', () async {
    storage.token = 'saved';
    service.restoration = Completer<AuthUser>();
    final initialized = auth.initialize();
    service.restoration!.completeError(Exception('invalid session'));
    await initialized;
    expect(auth.user, isNull);
    expect(storage.token, isNull);
    expect(auth.isInitializing, isFalse);
  });

  testWidgets('Customer login transitions from Login to Home', (tester) async {
    await mount(tester);
    await login();
    await tester.pumpAndSettle();
    expect(find.byType(CustomerHomeScreen), findsOneWidget);
    expect(find.byType(LoginScreen), findsNothing);
  });

  for (final screen in ['home', 'create', 'detail', 'edit']) {
    for (final expiry in [false, true]) {
      testWidgets(
        '${expiry ? '401' : 'logout'} from $screen removes protected history and state',
        (tester) async {
          await login();
          await mount(tester);
          await tester.pumpAndSettle();
          final context = tester.element(find.byType(CustomerHomeScreen));
          final state = context.read<ServiceRequestProvider>();
          if (screen != 'home') {
            final Widget page = switch (screen) {
              'create' => const CreateServiceRequestScreen(),
              'detail' => const ServiceRequestDetailScreen(requestId: 'A'),
              _ => EditServiceRequestScreen(request: request('A')),
            };
            Navigator.of(context)
                .push(MaterialPageRoute<void>(builder: (_) => page));
            await tester.pumpAndSettle();
          }
          state.setCurrentRequest(request('A'));
          requests.pending = Completer<List<ServiceRequestModel>>();
          final oldLoad = state.loadMyRequests();
          if (expiry) {
            api.client.httpClientAdapter = ReplyAdapter(
              (_) async => ResponseBody.fromString(
                '{}',
                401,
                headers: {
                  Headers.contentTypeHeader: ['application/json'],
                },
              ),
            );
            final expired = expectLater(
              api.client.get('/protected'),
              throwsA(isA<DioException>()),
            );
            await tester.pumpAndSettle();
            await expired;
          } else {
            await auth.logout();
          }
          await tester.pumpAndSettle();
          expect(auth.user, isNull);
          expect(storage.token, isNull);
          expect(state.requests, isEmpty);
          expect(state.currentRequest, isNull);
          expect(state.currentAnalysis, isNull);
          expect(state.error, isNull);
          expect(state.isLoading, isFalse);
          expect(find.byType(LoginScreen), findsOneWidget);
          await tester.binding.handlePopRoute();
          await tester.pumpAndSettle();
          expect(find.byType(LoginScreen), findsOneWidget);
          expect(find.byType(CustomerHomeScreen), findsNothing);
          requests.pending!.complete([request('A')]);
          expect(await oldLoad, isFalse);
          expect(state.requests, isEmpty);
          expect(tester.takeException(), isNull);
        },
      );
    }
  }

  testWidgets('Customer B cannot inherit A data or late responses', (
    tester,
  ) async {
    await login();
    await mount(tester);
    await tester.pumpAndSettle();
    final a = tester
        .element(find.byType(CustomerHomeScreen))
        .read<ServiceRequestProvider>();
    requests.detail = Completer<ServiceRequestModel>();
    final oldDetail = a.loadRequestById('A');
    await auth.logout();
    requests.items = [request('B')];
    await login('B');
    await tester.pumpAndSettle();
    final b = tester
        .element(find.byType(CustomerHomeScreen))
        .read<ServiceRequestProvider>();
    expect(identical(a, b), isFalse);
    requests.detail!.complete(request('A'));
    expect(await oldDetail, isNull);
    expect(b.requests.single.customerId, 'B');
    expect(b.currentRequest, isNull);
    expect(b.error, isNull);
    expect(b.isLoading, isFalse);
  });

  test(
    'reset rejects stale failures without changing new loading state',
    () async {
      final state = ServiceRequestProvider(serviceRequestService: requests);
      requests.pending = Completer<List<ServiceRequestModel>>();
      final old = state.loadMyRequests();
      final oldReply = requests.pending!;
      state.reset();
      requests.pending = Completer<List<ServiceRequestModel>>();
      final current = state.loadMyRequests();
      oldReply.completeError(Exception('old failure'));
      expect(await old, isFalse);
      expect(state.error, isNull);
      expect(state.isLoading, isTrue);
      requests.pending!.complete([request('B')]);
      expect(await current, isTrue);
      expect(state.requests.single.customerId, 'B');
    },
  );

  test('late 401 from A does not expire B', () async {
    await login();
    final sent = Completer<void>();
    final response = Completer<ResponseBody>();
    api.client.httpClientAdapter = ReplyAdapter((_) {
      sent.complete();
      return response.future;
    });
    final old = api.client.get('/protected');
    final expectation = expectLater(old, throwsA(isA<DioException>()));
    await sent.future;
    await auth.logout();
    await login('B');
    response.complete(ResponseBody.fromString(jsonEncode({}), 401));
    await expectation;
    expect(auth.user!.userId, 'B');
    expect(storage.token, 'token-B');
  });

  testWidgets(
    'registration success removes selection and registration routes',
    (tester) async {
      await mount(tester);
      final context = tester.element(find.byType(LoginScreen));
      Navigator.of(context).push(
        MaterialPageRoute<void>(
          builder: (_) => const AccountTypeSelectionScreen(),
        ),
      );
      await tester.pumpAndSettle();
      Navigator.of(
        tester.element(find.byType(AccountTypeSelectionScreen)),
      ).push(
        MaterialPageRoute<void>(builder: (_) => const CustomerRegisterScreen()),
      );
      await tester.pumpAndSettle();
      await auth.register(
        fullName: 'Customer A',
        email: 'A@example.com',
        password: 'password',
        phoneNumber: '0771234567',
        role: 'Customer',
      );
      await tester.pumpAndSettle();
      expect(find.byType(CustomerHomeScreen), findsOneWidget);
      expect(find.byType(CustomerRegisterScreen), findsNothing);
      expect(find.byType(AccountTypeSelectionScreen), findsNothing);
      await tester.binding.handlePopRoute();
      await tester.pumpAndSettle();
      expect(find.byType(AccountTypeSelectionScreen), findsNothing);
    },
  );
  test('logout during restoration cannot restore the old session', () async {
    storage.token = 'old';
    service.restoration = Completer<AuthUser>();
    final initialized = auth.initialize();
    await Future<void>.delayed(Duration.zero);
    await auth.logout();
    service.restoration!.complete(AuthStub.user('A'));
    await initialized;
    expect(auth.user, isNull);
    expect(auth.isInitializing, isFalse);
    expect(storage.token, isNull);
  });

  for (final fails in [false, true]) {
    test(
      'stale analysis ${fails ? 'failure' : 'success'} cannot reconcile after reset',
      () async {
        final state = ServiceRequestProvider(serviceRequestService: requests);
        state.setCurrentRequest(request('A'));
        requests.analysis = Completer<ProblemUnderstandingResultModel>();
        final old = state.analyzeRequest('A');
        state.reset();
        if (fails) {
          requests.analysis!.completeError(Exception('old analysis failed'));
        } else {
          requests.analysis!.complete(
            ProblemUnderstandingResultModel.fromJson({
              'serviceRequestId': 'A',
              'category': 'Plumbing',
              'status': 'Analyzed',
              'urgency': 'Low',
            }),
          );
        }
        expect(await old, isNull);
        expect(requests.detailCalls, 0);
        expect(state.currentAnalysis, isNull);
        expect(state.currentRequest, isNull);
        expect(state.isAnalyzing, isFalse);
        expect(state.error, isNull);
      },
    );
  }

  test('stale clarification response cannot start reanalysis', () async {
    final state = ServiceRequestProvider(serviceRequestService: requests);
    state.setCurrentRequest(request('A'));
    requests.answers = Completer<List<ServiceRequestClarificationModel>>();
    final old = state.submitClarificationAnswersAndReanalyze('A', 1, {
      'question': 'answer',
    });
    state.reset();
    requests.answers!.complete([]);
    expect(await old, isNull);
    expect(requests.analysisCalls, 0);
    expect(state.currentRequest, isNull);
  });
}
