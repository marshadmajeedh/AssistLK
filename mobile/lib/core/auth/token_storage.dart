import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class TokenStorage {
  static const _tokenKey = 'assistlk_token';

  final FlutterSecureStorage _storage;
  int generation = 0;
  Future<void> _writes = Future.value();
  Future<void> _write(Future<void> Function() action) {
    final next = _writes.then((_) => action());
    _writes = next.catchError((Object _) {});
    return next;
  }

  TokenStorage({FlutterSecureStorage? storage})
    : _storage = storage ?? const FlutterSecureStorage();

  Future<void> saveToken(String token) async {
    generation++;
    await _write(() => _storage.write(key: _tokenKey, value: token));
  }

  Future<String?> getToken() async {
    await _writes;
    return await _storage.read(key: _tokenKey);
  }

  Future<void> deleteToken() async {
    generation++;
    await _write(() => _storage.delete(key: _tokenKey));
  }

  Future<bool> deleteIfCurrent(String token, int expectedGeneration) async {
    final current = await getToken();
    if (generation != expectedGeneration || current != token) return false;
    await deleteToken();
    return generation == expectedGeneration + 1;
  }
}
