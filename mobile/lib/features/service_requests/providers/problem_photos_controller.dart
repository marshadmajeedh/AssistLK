import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';

import '../../auth/providers/auth_provider.dart';
import '../models/problem_photo.dart';
import '../models/service_request_attachment.dart';
import '../models/service_request_model.dart';
import '../services/problem_image_picker.dart';
import '../services/service_request_service.dart';

/// Route/draft-owned state. Never shared between requests or Customer sessions.
class ProblemPhotosController extends ChangeNotifier {
  ProblemPhotosController({
    required this.service,
    required this.picker,
    this.auth,
    this._requestId,
  }) {
    _session = auth?.sessionGeneration;
    _owner = auth?.user?.userId ?? '';
    auth?.addListener(_sessionChanged);
  }
  final ServiceRequestService service;
  final ProblemImagePicker picker;
  final AuthProvider? auth;
  late final int? _session;
  late final String _owner;
  final CancelToken _cancel = CancelToken();
  bool _disposed = false;
  bool _ended = false;
  bool busy = false;
  bool picking = false;
  bool loading = false;
  bool uncertainUpload = false;
  String? error;
  String? listError;
  String? _requestId;
  List<PhotoUpload> _uploads = [];
  List<ServiceRequestAttachment> _attachments = [];
  final Map<String, Uint8List> _content = {};
  final Map<String, String> _contentErrors = {};
  String get scope => _requestId ?? 'new-request';
  String? get requestId => _requestId;
  bool get active => !_disposed && !_ended;
  bool get sessionEnded => _ended;
  bool get hasPending =>
      _uploads.any((p) => p.state != PhotoUploadState.uploaded);
  bool get canAdd =>
      active &&
      !busy &&
      !picking &&
      !loading &&
      _attachments.length +
              _uploads
                  .where((p) => p.state != PhotoUploadState.uploaded)
                  .length <
          3;
  List<PhotoUpload> get uploads => List.unmodifiable(_uploads);
  List<ServiceRequestAttachment> get attachments =>
      List.unmodifiable(_attachments);
  Uint8List? content(String id) => _content[id];
  String? contentError(String id) => _contentErrors[id];
  int get uploadedCount =>
      _uploads.where((p) => p.state == PhotoUploadState.uploaded).length;

  void _changed() {
    if (!_disposed) notifyListeners();
  }

  void _sessionChanged() {
    if (auth?.sessionGeneration == _session && auth?.user?.userId == _owner) {
      return;
    }
    _ended = true;
    _cancel.cancel('Customer session ended');
    _uploads = [];
    _attachments = [];
    _content.clear();
    _contentErrors.clear();
    _requestId = null;
    busy = false;
    picking = false;
    loading = false;
    error = null;
    listError = null;
    _changed();
  }

  Future<void> recover() async {
    if (!active) return;
    try {
      final photos = await picker.recover(owner: _owner, scope: scope);
      for (final photo in photos) {
        if (!active) return;
        await _add(photo);
      }
    } catch (_) {
      /* Recovery must never auto-submit or invalidate a text draft. */
    }
  }

  Future<void> pick(PhotoSource source) async {
    if (!canAdd) return;
    picking = true;
    error = null;
    _changed();
    try {
      final photo = await picker.pick(source, owner: _owner, scope: scope);
      if (!active || photo == null) return;
      await _add(photo);
    } on PlatformException {
      if (active) error = 'Could not open photos or camera. Check permission in device settings and try again.';
    } catch (_) {
      if (active) error = 'Could not read this photo. Please choose another.';
    } finally {
      if (active) {
        picking = false;
        _changed();
      }
    }
  }

  Future<void> _add(ProblemPhoto photo) async {
    final message = await photo.validate();
    if (!active) return;
    if (message != null) {
      error = message;
      _changed();
      return;
    }
    if (_attachments.length +
            _uploads
                .where((p) => p.state != PhotoUploadState.uploaded)
                .length >=
        3) {
      return;
    }
    if (_uploads.any((p) => p.photo.identity == photo.identity)) return;
    _uploads = [..._uploads, PhotoUpload(photo)];
    _changed();
  }

  void removeSelected(ProblemPhoto photo) {
    if (!active || busy || _requestId != null) return;
    _uploads = _uploads.where((p) => p.photo != photo).toList();
    _changed();
  }

  /// The created identity survives partial failures. Retrying never calls create again.
  Future<bool> submit({Future<ServiceRequestModel?> Function()? create}) async {
    if (!active || busy || picking || uncertainUpload) return false;
    busy = true;
    error = null;
    _changed();
    try {
      if (_requestId == null) {
        final request = await create!();
        if (!active || request == null) return false;
        _requestId = request.serviceRequestId;
      }
      for (var i = 0; i < _uploads.length; i++) {
        if (!active) return false;
        final photo = _uploads[i].photo;
        if (_uploads[i].state == PhotoUploadState.uploaded) continue;
        _uploads[i] = PhotoUpload(photo, state: PhotoUploadState.uploading);
        _changed();
        try {
          final attachment = await service.uploadAttachment(
            _requestId!,
            photo,
            cancelToken: _cancel,
          );
          if (!active) return false;
          _attachments.add(attachment);
          _uploads[i] = PhotoUpload(photo, state: PhotoUploadState.uploaded);
        } catch (e) {
          if (!active) return false;
          uncertainUpload = service.isTimeoutOrUncertainTransport(e);
          _uploads[i] = PhotoUpload(
            photo,
            state: PhotoUploadState.failed,
            error: uncertainUpload
                ? 'Upload could not be confirmed. Continue to Request Details to check before adding this photo again.'
                : service.photoError(e),
          );
          if (uncertainUpload) break;
        }
        _changed();
      }
      return !hasPending;
    } finally {
      if (active) {
        busy = false;
        _changed();
      }
    }
  }

  void continueWithoutFailed() {
    if (!active || busy) return;
    _uploads = [];
    uncertainUpload = false;
    error = null;
    _changed();
  }

  Future<void> load() async {
    if (!active || loading || _requestId == null) return;
    loading = true;
    listError = null;
    _changed();
    try {
      final result = await service.listAttachments(
        _requestId!,
        cancelToken: _cancel,
      );
      if (!active) return;
      _attachments = result;
      final ids = result.map((p) => p.id).toSet();
      _content.removeWhere((id, _) => !ids.contains(id));
      _contentErrors.removeWhere((id, _) => !ids.contains(id));
      for (final item in result.take(3)) {
        if (!active) return;
        if (!_content.containsKey(item.id)) await loadContent(item.id);
      }
    } catch (e) {
      if (active) listError = service.photoError(e);
    } finally {
      if (active) {
        loading = false;
        _changed();
      }
    }
  }

  Future<void> loadContent(String id) async {
    if (!active) return;
    _contentErrors.remove(id);
    try {
      final bytes = await service.getAttachmentContent(
        _requestId!,
        id,
        cancelToken: _cancel,
      );
      if (active) _content[id] = bytes;
    } catch (e) {
      if (active) _contentErrors[id] = service.photoError(e);
    }
    _changed();
  }

  Future<bool> delete(String id) async {
    if (!active || busy) return false;
    busy = true;
    error = null;
    _changed();
    try {
      await service.deleteAttachment(_requestId!, id, cancelToken: _cancel);
      if (!active) return false;
      // Successful deletion immediately removes private bytes; then reconcile metadata.
      _attachments.removeWhere((p) => p.id == id);
      _content.remove(id);
      await load();
      return active;
    } catch (e) {
      if (active) {
        error = service.photoError(e);
        await load();
      }
      return false;
    } finally {
      if (active) {
        busy = false;
        _changed();
      }
    }
  }

  @override
  void dispose() {
    _disposed = true;
    _cancel.cancel('Photo view closed');
    auth?.removeListener(_sessionChanged);
    _uploads = [];
    _attachments = [];
    _content.clear();
    _contentErrors.clear();
    super.dispose();
  }
}
