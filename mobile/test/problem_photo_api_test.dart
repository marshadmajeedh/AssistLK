import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/api/api_client.dart';
import 'package:mobile/features/service_requests/models/create_service_request_dto.dart';
import 'package:mobile/features/service_requests/services/service_request_service.dart';

import 'mocks/fake_problem_photos.dart';

class RecordingAdapter implements HttpClientAdapter {
  final List<RequestOptions> requests = [];
  final List<List<int>> bodies = [];
  Future<ResponseBody> Function(RequestOptions)? respond;
  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? stream,
    Future<void>? cancelFuture,
  ) async {
    requests.add(options);
    bodies.add(stream == null ? [] : await stream.expand((e) => e).toList());
    return respond!(options);
  }

  @override
  void close({bool force = false}) {}
}

void main() {
  late RecordingAdapter adapter;
  late Dio dio;
  late MemoryTokens tokens;
  late ApiClient client;
  late ServiceRequestService service;
  final metadata = {
    'id': 'a1',
    'slot': 1,
    'contentType': 'image/jpeg',
    'fileSizeBytes': 120,
    'width': 10,
    'height': 20,
    'createdAt': '2026-09-13T00:00:00Z',
  };
  ResponseBody json(Object value, [int status = 200]) =>
      ResponseBody.fromString(
        jsonEncode(value),
        status,
        headers: {
          Headers.contentTypeHeader: ['application/json'],
        },
      );
  setUp(() {
    adapter = RecordingAdapter();
    dio = Dio()..httpClientAdapter = adapter;
    tokens = MemoryTokens();
    client = ApiClient(dio: dio, tokenStorage: tokens);
    service = ServiceRequestService(apiClient: client);
  });
  tearDown(() => dio.close());
  test('upload uses authenticated single-file multipart and leaves JSON defaults intact', () async {
    adapter.respond = (_) async => json(metadata, 201);
    final result = await service.uploadAttachment('r1', photo(1));
    final sent = adapter.requests.single;
    expect(sent.path, '/service-requests/r1/attachments');
    expect(sent.method, 'POST');
    expect(sent.headers['Authorization'], 'Bearer old-token');
    expect(sent.contentType, startsWith('multipart/form-data'));
    expect((sent.data as FormData).files.length, 1);
    expect((sent.data as FormData).files.single.key, 'file');
    expect(
      (sent.data as FormData).files.single.value.contentType.toString(),
      'image/png',
    );
    expect(adapter.bodies.single, isNotEmpty);
    expect(result.id, 'a1');
    expect(result.createdAt.isUtc, true);
    expect(dio.options.headers['Content-Type'], 'application/json');
    expect(dio.options.receiveTimeout, const Duration(seconds: 15));
    expect(sent.sendTimeout, const Duration(seconds: 60));
    adapter.respond = (_) async => json({
      'serviceRequestId': 'r1',
      'customerId': 'customer',
      'category': '',
      'description': 'Leaking sink',
      'locationText': 'Colombo',
      'status': 'Created',
      'urgency': 'Unknown',
      'createdAt': '2026-09-13T00:00:00Z',
      'updatedAt': '2026-09-13T00:00:00Z',
    });
    await service.create(
      const CreateServiceRequestDto(
        description: 'Leaking sink',
        locationText: 'Colombo',
      ),
    );
    expect(adapter.requests.last.contentType, 'application/json');
  });
  test('list parses real metadata fields and orders by slot', () async {
    adapter.respond = (_) async => json([
      {...metadata, 'id': 'a2', 'slot': 2},
      metadata,
    ]);
    final list = await service.listAttachments('r1');
    expect(list.map((p) => p.id), ['a1', 'a2']);
    expect(adapter.requests.single.path, '/service-requests/r1/attachments');
  });
  test('content is retrieved through authenticated shared Dio', () async {
    adapter.respond = (_) async => ResponseBody.fromBytes(photoBytes, 200);
    expect(await service.getAttachmentContent('r1', 'a1'), photoBytes);
    expect(
      adapter.requests.single.path,
      '/service-requests/r1/attachments/a1/content',
    );
    expect(
      adapter.requests.single.headers['Authorization'],
      'Bearer old-token',
    );
  });
  test('delete uses actual authorized endpoint', () async {
    adapter.respond = (_) async => ResponseBody.fromString('', 204);
    await service.deleteAttachment('r1', 'a1');
    expect(adapter.requests.single.method, 'DELETE');
    expect(adapter.requests.single.path, '/service-requests/r1/attachments/a1');
  });
  test('current 401 clears session through shared interceptor', () async {
    var expired = 0;
    client.onSessionExpired = () async {
      expired++;
      await tokens.deleteToken();
    };
    adapter.respond = (_) async => json({}, 401);
    await expectLater(
      service.listAttachments('r1'),
      throwsA(isA<DioException>()),
    );
    expect(tokens.token, isNull);
    expect(expired, 1);
  });
  test('old 401 cannot erase a newer sign-in', () async {
    var expired = 0;
    client.onSessionExpired = () async {
      expired++;
      await tokens.deleteToken();
    };
    final started = Completer<void>();
    final reply = Completer<ResponseBody>();
    adapter.respond = (_) {
      started.complete();
      return reply.future;
    };
    final old = service.listAttachments('r1');
    final assertion = expectLater(old, throwsA(isA<DioException>()));
    await started.future;
    await tokens.saveToken('new-token');
    reply.complete(json({}, 401));
    await assertion;
    expect(tokens.token, 'new-token');
    expect(expired, 0);
  });
  test('cancelled attachment operation sends no request', () async {
    final cancel = CancelToken()..cancel();
    adapter.respond = (_) async => json(metadata);
    await expectLater(
      service.uploadAttachment('r1', photo(1), cancelToken: cancel),
      throwsA(isA<DioException>()),
    );
    expect(adapter.requests, isEmpty);
  });
}
