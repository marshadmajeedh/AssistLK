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

/**
 * TEST SUITE: Problem Photo Recovery (Android Activity Death) Tests
 *
 * 1. What this test suite tests:
 *    - Verifies native Android camera/gallery activity death photo recovery in `NativeProblemImagePicker`.
 *    - On low-memory Android devices, the OS can kill the parent activity while the camera or gallery intent is open.
 *    - Tests `retrieveLostData()`, ownership markers stored in `FlutterSecureStorage` (`key: 'assistlk_pending_photo_owner'`),
 *      single-claim consumption, tenant/user isolation, and clearing recovery on user logout.
 * 2. Why these tests are needed:
 *    - Prevents permanent loss of photos taken by users on low-memory Android devices, while ensuring privacy and tenant safety:
 *      a photo taken by Customer A must never be recovered into Customer B's session or an unrelated draft request.
 * 3. What is real and what is mocked:
 *    - Real `NativeProblemImagePicker` with `TargetPlatform.android` override.
 *    - Mocked: `LostPhotoPicker` simulates native `ImagePicker.retrieveLostData()`, `FlutterSecureStorage.setMockInitialValues` mocks secure local storage.
 * 4. Prerequisites: None.
 * 5. PowerShell command to run this suite from repository root:
 *    Push-Location mobile; flutter test test/problem_photo_recovery_test.dart; Pop-Location
 */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that recovered lost photos can only be claimed by the matching owner and scope ('new-request'), and only once.
   *
   * Why: Why this scenario matters
   * - Prevents duplicate injection or cross-scope leakage of recovered images into the wrong service request.
   *
   * Scenario: Input or condition being tested
   * - Recover with different-request scope (expect empty), recover with new-request scope (expect 1 photo), repeat recover (expect empty).
   *
   * Pass condition: What result or assertion makes it pass
   * - First matching recover returns 1 photo, subsequent recover returns empty, native `reads` is 1, storage key is deleted.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/problem_photo_recovery_test.dart --plain-name "startup recovery consumes native lost data once and only matching draft can claim it"; Pop-Location
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies tenant isolation: a different user cannot recover photos initiated by a previous user.
   *
   * Why: Why this scenario matters
   * - Strict data privacy requirement: prevents personal/sensitive customer photos from appearing in another user's session.
   *
   * Scenario: Input or condition being tested
   * - Storage contains owner `'customer'`; new session initializes recovery for `'another-customer'`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Both recovery attempts return empty lists.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/problem_photo_recovery_test.dart --plain-name "another Customer cannot recover previous Customer photo"; Pop-Location
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that if secure storage lacks an ownership marker, unowned native lost data is discarded.
   *
   * Why: Why this scenario matters
   * - Avoids attaching stray, orphan images with unknown provenance to an active request.
   *
   * Scenario: Input or condition being tested
   * - Storage is empty when `prepareRecovery` is called.
   *
   * Pass condition: What result or assertion makes it pass
   * - `recover` returns an empty list.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/problem_photo_recovery_test.dart --plain-name "missing ownership marker discards unclaimed native results"; Pop-Location
   */
  test('missing ownership marker discards unclaimed native results', () async {
    FlutterSecureStorage.setMockInitialValues({});
    final picker = NativeProblemImagePicker(picker: LostPhotoPicker());
    await picker.prepareRecovery('customer');
    expect(
      await picker.recover(owner: 'customer', scope: 'new-request'),
      isEmpty,
    );
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that logging out (`clearRecovery()`) while native retrieval is in-flight invalidates and drops late results.
   *
   * Why: Why this scenario matters
   * - Ensures that signing out immediately purges any pending recovery data before a new user logs in.
   *
   * Scenario: Input or condition being tested
   * - Initiate recovery with delayed `Completer`, invoke `clearRecovery()`, then complete native retrieval.
   *
   * Pass condition: What result or assertion makes it pass
   * - Subsequent `recover` call returns empty.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/problem_photo_recovery_test.dart --plain-name "logout during recovery invalidates late native results"; Pop-Location
   */
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
