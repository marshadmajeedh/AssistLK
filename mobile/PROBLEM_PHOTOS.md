# Optional problem photos — Image Understanding Phase 2

This phase adds Customer selection/upload/view/remove UX only. Photos are stored as evidence; Python analysis remains text-only. No backend, React, Python, lifecycle, CategoryHint, Smart Location, clarification-round, or ReadyForMatching business rules changed.

## Dependency and platforms

`image_picker: ^1.2.3` is the only picker dependency, locked to 1.2.3. Its [package metadata](https://pub.dev/packages/image_picker) and [SDK requirements](https://github.com/flutter/packages/blob/main/packages/image_picker/image_picker/pubspec.yaml) were checked before adding it: Dart 3.10+/Flutter 3.38+, compatible with the installed Dart 3.13.1/Flutter 3.47.1. Existing direct dependencies were not upgraded.

iOS camera/photo-library descriptions are in Info.plist; no microphone/video permission. The existing iOS deployment target is 15.0. Android uses the plugin's system picker/camera intents with the existing singleTop activity; no obsolete storage, broad media-library, startup permission request, or manual CAMERA permission was added. Permission prompts follow explicit source selection. HEIC and photos above 5 MiB receive a friendly local rejection; the server remains authoritative for format, signatures, dimensions and normalization.

Flutter generated five legitimate plugin file changes: Linux registrant and CMake list, Windows registrant and CMake list, and macOS Swift registrant. These register image_picker's transitive file_selector platform implementations; none were hand-edited. Camera support and physical verification target Android/iOS, not desktop camera delegates.

## Customer flow

- Details: optional photo section below description; Add Photo opens Take Photo / Choose from Gallery. Up to three route-owned picker files, bounded thumbnail decoding, accessible remove actions. Cancellation is a no-op.
- Location and Review preserve the same draft. Review displays the selected photos or a subtle no-photo message. Disposing the draft drops its selections.
- Submit creates the normal JSON ServiceRequest once, saves its returned identity, then sends one multipart file per sequential upload. Upload-specific send/receive timeouts are 60 seconds; default JSON timeout/content type are unchanged.
- Pending/uploading/uploaded/failed states and an indeterminate progress indicator reflect actual operations. No fabricated percentages. HTTP failures keep successful uploads and the created request; Retry failed photos never recreates the request or retries confirmed successes. Continue without failed photos drops local queue references and retains server evidence.
- A timeout/connection loss can hide a successful server commit because Phase 1 has no idempotency key/content-hash response. Such uncertain uploads are **not blindly repeated**. The UI asks the Customer to continue to Request Details, inspect authoritative photos, and add again only if absent. This avoids silently creating duplicate attachments.
- Request Details uses authenticated shared Dio byte retrieval, caches at most the three displayed photos within the route, and supports a simple read-only preview. List/content failures have local retry and do not replace request details.
- Created/AwaitingInformation allow explicit add/upload and confirmed deletion. Other states retain viewing with no add/remove controls. Metadata/request state is refreshed after mutation; Flutter never calculates a revision. Pending selections must be uploaded or discarded before the next analysis action.
- Photo management is intentionally centralized on Request Details. Edit Request keeps its existing description/location/category behavior; it does not duplicate photo management or import Home location.

## Contract and session isolation

All attachment calls extend ServiceRequestService through the existing ApiClient/Dio. Base URL already includes `/api`. Routes are POST/GET `/service-requests/{id}/attachments`, GET `/{attachmentId}/content`, DELETE `/{attachmentId}`. Multipart field is `file`; its safe filename/declared MIME match the selected supported format. The response model parses exactly id, slot, contentType, fileSizeBytes, width, height, createdAt. No storage keys, paths, hashes, public image URLs or image fields in request JSON.

The actual branch lacked the session-generation machinery described in the task. Small guards were therefore added to the existing auth/API/provider wiring, without changing navigation: Customer changes clear request state, cancel photo operations, invalidate late results and evict image preview cache entries. Token writes are serialized; a 401 clears only the token/generation that sent that request, so a delayed old response cannot clear a newer sign-in. Request service completions are generation-checked before repopulating state. New photo state belongs to the route, not a global provider.

On Android startup, `retrieveLostData()` is called through the picker abstraction. Only an owner/scope/source marker is persisted in secure storage, never photo bytes or a request draft. A matching authenticated Customer can claim the recovered file once when returning to Create or the matching Request Details screen. Missing/mismatched ownership is discarded, logout invalidates late recovery, and marker writes are serialized. Recovery never creates or uploads a request. Description/location drafts are not persisted across process death; the Customer reviews/re-enters them and explicitly submits.

Private previews use bounded decode widths (256 thumbnail, 1200 preview) and evict their image-cache entries on disposal. Original local files remain in platform-managed picker cache; this phase does not promise physical secure erasure of that OS cache or persistent offline draft recovery.

## Automated checks

The unchanged branch baseline was **224 tests passed**. New tests cover fake gallery/camera selection, cancellation/count/removal, wizard navigation/review, duplicate submit, sequential/partial uploads, retry/continue, contract/MIME/authenticated content, session expiry, old 401 results, Android recovery ownership, photo viewing/deletion/lifecycle states and 320/412px layouts at normal/1.8 text scale. Existing mocks now explicitly return empty attachments and scroll to controls below optional photo content; meaningful assertions were retained.

The full enlarged-text flow also exposed existing overflow in the wizard step indicator and service-preference banner. These layouts now adapt without changing navigation or business behavior.

## Physical-device checklist (not yet performed)

1. Details → Add Photo → Camera; allow/deny permission and cancel normally.
2. Details → Add Photo → Gallery; choose JPEG/PNG/static WebP.
3. Select three photos; verify the maximum and disabled Add action.
4. Remove one selected photo; verify the other draft fields remain intact.
5. Navigate through Location/Review and back; check thumbnails.
6. Submit once; verify a single ServiceRequest is created.
7. Observe sequential photo upload states.
8. Open Request Details and verify authenticated thumbnails.
9. Open/close a full preview; confirm there are no mutation controls inside it.
10. Remove a photo while Created, including confirmation cancellation.
11. Add/upload a photo while AwaitingInformation; verify clarification answers/rounds remain unchanged and analysis stays explicit.
12. Verify Analyzed/Analyzing/ReadyForMatching/Cancelled photos have no removal controls.
13. Interrupt networking after request creation; verify the request is retained.
14. Retry a deterministic failed upload; verify successful photos and request creation are not repeated. For uncertain transport, inspect Request Details first.
15. Continue without failed optional photos and verify normal text-based workflow remains usable.
16. Logout/login as another Customer or Provider during picker/upload; verify no old photos appear. Also simulate Android activity destruction and reclaim only matching-owner recovery.

No physical camera/gallery, iOS device, or live backend upload verification is claimed. The primary database migration remains an operator prerequisite for a later live run. Phase 3 vision transport/agent integration and any Admin photo presentation remain unimplemented.

## Verification results

- `flutter pub get`: successful.
- `flutter analyze`: no issues found.
- `flutter test --reporter expanded`: 282 passed (224 baseline + 58 new), 0 failed, 0 skipped.
- `git diff --check`: passed; new files checked for trailing whitespace.
- Changes are limited to mobile/. No commit, live backend run, or physical-device check performed.

## File manifest

Created:

- `mobile/PROBLEM_PHOTOS.md`
- `mobile/lib/features/service_requests/models/problem_photo.dart`
- `mobile/lib/features/service_requests/models/service_request_attachment.dart`
- `mobile/lib/features/service_requests/providers/problem_photos_controller.dart`
- `mobile/lib/features/service_requests/services/problem_image_picker.dart`
- `mobile/lib/features/service_requests/widgets/problem_photos.dart`
- `mobile/test/mocks/fake_problem_photos.dart`
- `mobile/test/problem_photo_api_test.dart`
- `mobile/test/problem_photo_recovery_test.dart`
- `mobile/test/problem_photos_controller_test.dart`
- `mobile/test/problem_photos_widgets_test.dart`

Modified:

- `mobile/ios/Runner/Info.plist`
- `mobile/lib/core/api/api_client.dart`
- `mobile/lib/core/auth/token_storage.dart`
- `mobile/lib/features/auth/providers/auth_provider.dart`
- `mobile/lib/features/service_requests/providers/service_request_provider.dart`
- `mobile/lib/features/service_requests/screens/create_service_request_screen.dart`
- `mobile/lib/features/service_requests/screens/service_request_detail_screen.dart`
- `mobile/lib/features/service_requests/services/service_request_service.dart`
- `mobile/lib/features/service_requests/widgets/step_indicator.dart`
- `mobile/lib/main.dart`
- `mobile/linux/flutter/generated_plugin_registrant.cc`
- `mobile/linux/flutter/generated_plugins.cmake`
- `mobile/macos/Flutter/GeneratedPluginRegistrant.swift`
- `mobile/pubspec.lock`
- `mobile/pubspec.yaml`
- `mobile/test/auth_screens_test.dart`
- `mobile/test/component_1_layout_verification_test.dart`
- `mobile/test/service_request_provider_test.dart`
- `mobile/test/service_request_screens_test.dart`
- `mobile/test/smart_location_test.dart`
- `mobile/windows/flutter/generated_plugin_registrant.cc`
- `mobile/windows/flutter/generated_plugins.cmake`
