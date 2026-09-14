import 'dart:async';
import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:image_picker/image_picker.dart';
import 'package:mobile/features/service_requests/services/problem_image_picker.dart';

import 'mocks/fake_problem_photos.dart';

class LostPhotoPicker extends ImagePicker {
  int reads = 0;
  Completer<LostDataResponse>? pending;
  @override
  Future<LostDataResponse> retrieveLostData() async {
    reads++;
    return pending?.future ??
        LostDataResponse(files: [photo(1).file], type: RetrieveType.image);
  }
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  const key = 'assistlk_pending_photo_owner';
  setUp(() {
    debugDefaultTargetPlatformOverride = TargetPlatform.android;
    FlutterSecureStorage.setMockInitialValues({
      key: jsonEncode({
        'owner': 'customer',
        'scope': 'new-request',
        'source': 'gallery',
      }),
    });
  });
  tearDown(() => debugDefaultTargetPlatformOverride = null);
  test('startup recovery consumes native lost data once and only matching draft can claim it', () async {
    final native = LostPhotoPicker();
    final picker = NativeProblemImagePicker(picker: native);
    await picker.prepareRecovery('customer');
    expect(
      await picker.recover(owner: 'customer', scope: 'different-request'),
      isEmpty,
    );
    expect(
      (await picker.recover(owner: 'customer', scope: 'new-request')).length,
      1,
    );
    expect(
      await picker.recover(owner: 'customer', scope: 'new-request'),
      isEmpty,
    );
    expect(native.reads, 1);
    expect(await const FlutterSecureStorage().read(key: key), isNull);
  });
  test('another Customer cannot recover previous Customer photo', () async {
    final picker = NativeProblemImagePicker(picker: LostPhotoPicker());
    await picker.prepareRecovery('another-customer');
    expect(
      await picker.recover(owner: 'another-customer', scope: 'new-request'),
      isEmpty,
    );
    expect(
      await picker.recover(owner: 'customer', scope: 'new-request'),
      isEmpty,
    );
  });
  test('missing ownership marker discards unclaimed native results', () async {
    FlutterSecureStorage.setMockInitialValues({});
    final picker = NativeProblemImagePicker(picker: LostPhotoPicker());
    await picker.prepareRecovery('customer');
    expect(
      await picker.recover(owner: 'customer', scope: 'new-request'),
      isEmpty,
    );
  });
  test('logout during recovery invalidates late native results', () async {
    final native = LostPhotoPicker()..pending = Completer<LostDataResponse>();
    final picker = NativeProblemImagePicker(picker: native);
    final startup = picker.prepareRecovery('customer');
    await Future<void>.delayed(Duration.zero);
    await picker.clearRecovery();
    native.pending!.complete(
      LostDataResponse(files: [photo(1).file], type: RetrieveType.image),
    );
    await startup;
    expect(
      await picker.recover(owner: 'customer', scope: 'new-request'),
      isEmpty,
    );
  });
}
