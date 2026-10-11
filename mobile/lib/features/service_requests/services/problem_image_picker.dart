import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:image_picker/image_picker.dart';

import '../models/problem_photo.dart';

abstract class ProblemImagePicker {
  Future<ProblemPhoto?> pick(
    PhotoSource source, {
    required String owner,
    required String scope,
  });
  Future<List<ProblemPhoto>> recover({
    required String owner,
    required String scope,
  }) async => [];
  Future<void> clearRecovery() async {}
}

/// Only a small ownership marker is persisted, never a photo or draft.
class NativeProblemImagePicker extends ProblemImagePicker {
  NativeProblemImagePicker({ImagePicker? picker, FlutterSecureStorage? storage})
    : _picker = picker ?? ImagePicker(),
      _storage = storage ?? const FlutterSecureStorage();
  final ImagePicker _picker;
  final FlutterSecureStorage _storage;
  static const _key = 'assistlk_pending_photo_owner';
  Future<void>? _recovery;
  List<ProblemPhoto> _recovered = [];
  String? _owner;
  String? _scope;
  int _generation = 0;
  Future<void> _markerWrites = Future.value();
  Future<void> _marker(Future<void> Function() action) {
    final next = _markerWrites.then((_) => action());
    _markerWrites = next.catchError((Object _) {});
    return next;
  }

  Future<void> prepareRecovery(String? owner) => _recovery ??= _prepare(owner);
  Future<void> _prepare(String? owner) async {
    if (kIsWeb || defaultTargetPlatform != TargetPlatform.android) return;
    final generation = _generation;
    try {
      await _markerWrites;
      final marker = await _storage.read(key: _key);
      final lost = await _picker.retrieveLostData();
      await _marker(() async {
        if (generation == _generation) await _storage.delete(key: _key);
      });
      if (generation != _generation || marker == null || owner == null) return;
      final saved = jsonDecode(marker) as Map<String, dynamic>;
      if (saved['owner'] != owner) return;
      _owner = owner;
      _scope = saved['scope'] as String;
      _recovered = (lost.files ?? [])
          .take(3)
          .map(
            (file) => ProblemPhoto(
              file: file,
              source: PhotoSource.values.byName(saved['source'] as String),
            ),
          )
          .toList();
    } catch (_) {
      // No upload or request is ever performed by recovery.
      _recovered = [];
    }
  }

  @override
  Future<List<ProblemPhoto>> recover({
    required String owner,
    required String scope,
  }) async {
    await prepareRecovery(owner);
    if (_owner != owner || _scope != scope) return [];
    final photos = _recovered;
    _recovered = [];
    return photos;
  }

  @override
  Future<ProblemPhoto?> pick(
    PhotoSource source, {
    required String owner,
    required String scope,
  }) async {
    final generation = _generation;
    await _marker(() async {
      if (generation == _generation) {
        await _storage.write(
          key: _key,
          value: jsonEncode({
            'owner': owner,
            'scope': scope,
            'source': source.name,
          }),
        );
      }
    });
    if (generation != _generation) return null;
    try {
      final file = await _picker.pickImage(
        source: source == PhotoSource.camera
            ? ImageSource.camera
            : ImageSource.gallery,
        requestFullMetadata: false,
      );
      if (file == null || generation != _generation) return null;
      return ProblemPhoto(file: file, source: source);
    } finally {
      await _marker(() async {
        if (generation == _generation) await _storage.delete(key: _key);
      });
    }
  }

  @override
  Future<void> clearRecovery() async {
    _generation++;
    _recovered = [];
    _owner = null;
    _scope = null;
    await _marker(() => _storage.delete(key: _key));
  }
}
