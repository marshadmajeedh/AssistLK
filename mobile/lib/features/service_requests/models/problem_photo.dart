import 'package:image_picker/image_picker.dart';

enum PhotoSource { camera, gallery }

enum PhotoUploadState { pending, uploading, uploaded, failed }

/// A draft owns picker files; bytes are read only for preview or upload.
class ProblemPhoto {
  const ProblemPhoto({required this.file, required this.source});
  final XFile file;
  final PhotoSource source;
  String get identity => file.path;
  String get extension => file.name.split('.').last.toLowerCase();
  String? get mimeType => switch (extension) {
    'jpg' || 'jpeg' => 'image/jpeg',
    'png' => 'image/png',
    'webp' => 'image/webp',
    _ => null,
  };

  Future<String?> validate() async {
    if (mimeType == null) return 'Choose a JPEG, PNG or static WebP photo.';
    final size = await file.length();
    if (size == 0) return 'This photo is empty. Please choose another.';
    if (size > 5 * 1024 * 1024) return 'Choose a photo smaller than 5 MiB.';
    return null;
  }
}

class PhotoUpload {
  const PhotoUpload(
    this.photo, {
    this.state = PhotoUploadState.pending,
    this.error,
  });
  final ProblemPhoto photo;
  final PhotoUploadState state;
  final String? error;
}
