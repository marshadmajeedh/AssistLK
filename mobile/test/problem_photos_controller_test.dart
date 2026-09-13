import 'dart:async';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/service_requests/models/create_service_request_dto.dart';
import 'package:mobile/features/service_requests/models/problem_photo.dart';
import 'package:mobile/features/service_requests/models/service_request_attachment.dart';
import 'package:mobile/features/service_requests/models/service_request_model.dart';
import 'package:mobile/features/service_requests/providers/problem_photos_controller.dart';
import 'package:mobile/features/service_requests/providers/service_request_provider.dart';

import 'mocks/fake_problem_photos.dart';

void main() {
  late PhotoApi api;
  late FakePhotoPicker picker;
  late PhotoAuth auth;
  late ProblemPhotosController controller;
  const dto = CreateServiceRequestDto(
    description: 'Water is leaking under the sink',
    locationText: 'Colombo',
  );
  setUp(() {
    api = PhotoApi();
    picker = FakePhotoPicker();
    auth = PhotoAuth();
    controller = ProblemPhotosController(
      service: api,
      picker: picker,
      auth: auth,
    );
  });
  tearDown(() {
    controller.dispose();
    auth.dispose();
  });
  Future<void> add(int i) async {
    picker.next = photo(i);
    await controller.pick(PhotoSource.gallery);
  }

  Future<bool> submit() => controller.submit(create: () => api.create(dto));

  test('third photo allowed fourth blocked without invoking picker', () async {
    for (var i = 1; i <= 4; i++) {
      await add(i);
    }
    expect(controller.uploads.length, 3);
    expect(picker.calls.length, 3);
    expect(controller.canAdd, false);
  });
  test('cancelling gallery is a no-op', () async {
    await controller.pick(PhotoSource.gallery);
    expect(controller.uploads, isEmpty);
    expect(controller.error, isNull);
  });
  test('remove draft photo never calls API and permits replacement', () async {
    await add(1);
    controller.removeSelected(controller.uploads.single.photo);
    expect(controller.uploads, isEmpty);
    expect(api.events, isEmpty);
    await add(2);
    expect(controller.uploads.length, 1);
  });
  test('oversized local file shows friendly message', () async {
    picker.next = photo(1, bytes: Uint8List(5 * 1024 * 1024 + 1));
    await controller.pick(PhotoSource.gallery);
    expect(controller.uploads, isEmpty);
    expect(controller.error, contains('5 MiB'));
  });
  test('unsupported local file is rejected without upload', () async {
    picker.next = photo(1, extension: 'heic');
    await controller.pick(PhotoSource.camera);
    expect(controller.uploads, isEmpty);
    expect(controller.error, contains('JPEG'));
    expect(api.events, isEmpty);
  });
  test(
    'create completes before upload and double submission is ignored',
    () async {
      await add(1);
      api.createPending = Completer<ServiceRequestModel>();
      final first = submit();
      expect(await submit(), false);
      expect(api.events, ['create']);
      api.createPending!.complete(request());
      expect(await first, true);
      expect(api.events, ['create', 'upload:request-1:photo1.png']);
      expect(controller.requestId, 'request-1');
    },
  );
  test(
    'three uploads are sequential and successful photos never retried',
    () async {
      for (var i = 1; i <= 3; i++) {
        await add(i);
      }
      final pending = Completer<ServiceRequestAttachment>();
      api.uploadPending = pending;
      final sending = submit();
      await Future<void>.delayed(Duration.zero);
      expect(api.events, ['create', 'upload:request-1:photo1.png']);
      api.uploadPending = null;
      pending.complete(attachment(1));
      expect(await sending, true);
      expect(api.events.where((e) => e.startsWith('upload:')).length, 3);
      await submit();
      expect(api.events.where((e) => e == 'create').length, 1);
      expect(api.events.where((e) => e.startsWith('upload:')).length, 3);
    },
  );
  test(
    'partial failure retains request and retries only failed photo',
    () async {
      for (var i = 1; i <= 3; i++) {
        await add(i);
      }
      api.failures.add('photo2.png');
      expect(await submit(), false);
      expect(controller.uploadedCount, 2);
      expect(api.rows.length, 2);
      expect(controller.requestId, 'request-1');
      expect(controller.uploads[1].state, PhotoUploadState.failed);
      api.failures.clear();
      expect(await submit(), true);
      expect(api.events.where((e) => e == 'create').length, 1);
      expect(api.events.where((e) => e.endsWith('photo1.png')).length, 1);
      expect(api.events.where((e) => e.endsWith('photo2.png')).length, 2);
      expect(api.events.where((e) => e.endsWith('photo3.png')).length, 1);
    },
  );
  test(
    'continue abandons failed draft files without deleting successful evidence',
    () async {
      await add(1);
      await add(2);
      api.failures.add('photo2.png');
      await submit();
      controller.continueWithoutFailed();
      expect(controller.uploads, isEmpty);
      expect(controller.hasPending, false);
      expect(controller.requestId, 'request-1');
      expect(api.rows.length, 1);
      expect(api.events.where((e) => e.startsWith('delete:')), isEmpty);
    },
  );
  test('no-photo request remains valid', () async {
    expect(await submit(), true);
    expect(api.events, ['create']);
  });
  test('transport uncertainty does not blindly repeat potentially committed upload', () async {
    await add(1);
    api.uploadError = DioException(
      requestOptions: RequestOptions(path: '/photos'),
      type: DioExceptionType.receiveTimeout,
    );
    expect(await submit(), false);
    expect(controller.uncertainUpload, true);
    await submit();
    expect(api.events.length, 2);
    expect(controller.requestId, 'request-1');
    controller.continueWithoutFailed();
    expect(controller.hasPending, false);
  });
  for (final status in [400, 401, 403, 404, 409, 413, 500]) {
    test('HTTP $status maps safely without leaking server details', () async {
      await add(1);
      api.uploadError = photoFailure(status);
      expect(await submit(), false);
      final message = controller.uploads.single.error!;
      expect(message, isNot(contains('secret')));
      expect(message, isNot(contains('stack trace')));
      expect(controller.requestId, 'request-1');
      if (status == 409) expect(message, contains('no longer editable'));
      if (status == 400) expect(message, contains('valid JPEG'));
    });
  }
  test(
    'logout clears selected files and ignores delayed picker completion',
    () async {
      await add(1);
      picker.pending = Completer<ProblemPhoto?>();
      final work = controller.pick(PhotoSource.camera);
      auth.change(null);
      auth.change('new-customer');
      picker.pending!.complete(photo(2));
      await work;
      expect(controller.uploads, isEmpty);
      expect(controller.sessionEnded, true);
      expect(controller.active, false);
    },
  );
  test('old upload completion cannot change newer Customer session', () async {
    await add(1);
    api.uploadPending = Completer<ServiceRequestAttachment>();
    final work = submit();
    await Future<void>.delayed(Duration.zero);
    auth.change('new-customer');
    api.uploadPending!.complete(attachment(1));
    expect(await work, false);
    expect(controller.uploads, isEmpty);
    expect(controller.attachments, isEmpty);
    expect(controller.requestId, isNull);
  });
  test(
    'request provider ignores creation that completes after logout',
    () async {
      final provider = ServiceRequestProvider(serviceRequestService: api);
      api.createPending = Completer<ServiceRequestModel>();
      final work = provider.createRequest(dto);
      provider.clearSession();
      api.createPending!.complete(request());
      expect(await work, isNull);
      expect(provider.requests, isEmpty);
      expect(provider.currentRequest, isNull);
      provider.dispose();
    },
  );
  test(
    'recovered photos remain draft-only until explicit submission',
    () async {
      picker.recovered = [photo(1)];
      await controller.recover();
      expect(controller.uploads.length, 1);
      expect(api.events, isEmpty);
    },
  );
  test('new draft does not inherit discarded route photos', () async {
    await add(1);
    final next = ProblemPhotosController(
      service: api,
      picker: FakePhotoPicker(),
      auth: auth,
    );
    expect(next.uploads, isEmpty);
    next.dispose();
  });
  test(
    'metadata and authenticated bytes load once and deletion refreshes list',
    () async {
      final details = ProblemPhotosController(
        service: api,
        picker: picker,
        auth: auth,
        requestId: 'request-1',
      );
      api.rows.add(attachment(1));
      await details.load();
      await details.load();
      expect(details.content('attachment-1'), photoBytes);
      expect(api.events.where((e) => e == 'content:attachment-1').length, 1);
      expect(await details.delete('attachment-1'), true);
      expect(details.attachments, isEmpty);
      expect(details.content('attachment-1'), isNull);
      expect(api.events.last, 'list');
      details.dispose();
    },
  );
  test('stale attachment listing cannot restore bytes after logout', () async {
    final details = ProblemPhotosController(
      service: api,
      picker: picker,
      auth: auth,
      requestId: 'request-1',
    );
    api.listPending = Completer<List<ServiceRequestAttachment>>();
    final work = details.load();
    auth.change(null);
    api.listPending!.complete([attachment(1)]);
    await work;
    expect(details.attachments, isEmpty);
    details.dispose();
  });
}
