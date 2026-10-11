import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:http_parser/http_parser.dart';
import 'package:image_picker/image_picker.dart';

import '../../../core/auth/token_storage.dart';
import '../../../core/config/app_config.dart';

class ProofOfWorkUploadWidget extends StatefulWidget {
  final String jobId;
  final double timeElapsedMinutes;
  final Future<void> Function(Map<String, dynamic> response) onCompleted;

  const ProofOfWorkUploadWidget({
    super.key,
    required this.jobId,
    required this.timeElapsedMinutes,
    required this.onCompleted,
  });

  @override
  State<ProofOfWorkUploadWidget> createState() =>
      _ProofOfWorkUploadWidgetState();
}

class _ProofOfWorkUploadWidgetState extends State<ProofOfWorkUploadWidget> {
  final TextEditingController _summaryController = TextEditingController();
  final ImagePicker _imagePicker = ImagePicker();
  final TokenStorage _tokenStorage = TokenStorage();
  XFile? _selectedImage;
  bool _isSubmitting = false;

  @override
  void dispose() {
    _summaryController.dispose();
    super.dispose();
  }

  Future<void> _pickImage(ImageSource source) async {
    final image = await _imagePicker.pickImage(
      source: source,
      imageQuality: 85,
    );
    if (image != null && mounted) {
      setState(() => _selectedImage = image);
    }
  }

  Future<void> _chooseImageSource() async {
    final source = await showModalBottomSheet<ImageSource>(
      context: context,
      builder: (pickerContext) => SafeArea(
        child: Wrap(
          children: [
            ListTile(
              leading: const Icon(Icons.camera_alt),
              title: const Text('Take photo'),
              onTap: () => Navigator.pop(pickerContext, ImageSource.camera),
            ),
            ListTile(
              leading: const Icon(Icons.photo_library),
              title: const Text('Choose from gallery'),
              onTap: () => Navigator.pop(pickerContext, ImageSource.gallery),
            ),
          ],
        ),
      ),
    );
    if (source != null) await _pickImage(source);
  }

  Future<void> _submit() async {
    final image = _selectedImage;
    if (image == null || _isSubmitting) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Upload proof of work before submitting.'),
        ),
      );
      return;
    }

    final contentType = _imageContentType(image.name);
    if (contentType == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Choose a JPEG, PNG, or WebP image for proof of work.'),
        ),
      );
      return;
    }

    final imageBytes = await image.readAsBytes();
    if (!mounted) return;
    if (imageBytes.isEmpty || imageBytes.length > 10 * 1024 * 1024) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Choose a proof image smaller than 10 MB.'),
        ),
      );
      return;
    }

    setState(() => _isSubmitting = true);
    try {
      final token = await _tokenStorage.getToken();
      if (token == null || token.isEmpty) {
        throw StateError(
          'Cannot complete the job without an authenticated session.',
        );
      }

      final request =
          http.MultipartRequest(
              'PUT',
              Uri.parse(
                '${AppConfig.apiBaseUrl}/service-jobs/${widget.jobId}/complete',
              ),
            )
            ..headers['Authorization'] = ['Bearer', token].join(' ')
            ..fields['notes'] = _summaryController.text.trim().isEmpty
                ? 'Work completed'
                : _summaryController.text.trim()
            ..fields['timeElapsedMinutes'] = widget.timeElapsedMinutes
                .toString();

      request.files.add(
        http.MultipartFile.fromBytes(
          'proofOfWorkImage',
          imageBytes,
          filename: image.name,
          contentType: contentType,
        ),
      );

      final response = await request.send();
      final responseBody = await response.stream.bytesToString();
      var decoded = <String, dynamic>{};
      if (responseBody.isNotEmpty) {
        try {
          final payload = jsonDecode(responseBody);
          if (payload is Map<String, dynamic>) decoded = payload;
        } on FormatException {
          // Use the HTTP status when the server returns a non-JSON error.
        }
      }

      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw StateError(
          decoded['message']?.toString() ??
              (responseBody.isNotEmpty ? responseBody : null) ??
              'Completion failed with status ${response.statusCode}.',
        );
      }

      if (mounted) await widget.onCompleted(decoded);
    } catch (error) {
      if (!mounted) return;
      setState(() => _isSubmitting = false);
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text('Completion failed: $error')));
    }
  }

  MediaType? _imageContentType(String fileName) {
    switch (fileName.split('.').last.toLowerCase()) {
      case 'jpg':
      case 'jpeg':
        return MediaType('image', 'jpeg');
      case 'png':
        return MediaType('image', 'png');
      case 'webp':
        return MediaType('image', 'webp');
      default:
        return null;
    }
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.only(
        bottom: MediaQuery.of(context).viewInsets.bottom,
        left: 16,
        right: 16,
        top: 16,
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Text(
            'Complete Service Job',
            style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
          ),
          TextField(
            controller: _summaryController,
            enabled: !_isSubmitting,
            decoration: const InputDecoration(labelText: 'Work Summary'),
          ),
          const SizedBox(height: 10),
          if (_selectedImage != null) Text('Selected: ${_selectedImage!.name}'),
          ElevatedButton.icon(
            onPressed: _isSubmitting ? null : _chooseImageSource,
            icon: const Icon(Icons.camera_alt),
            label: const Text('Upload Proof of Work'),
          ),
          const SizedBox(height: 15),
          ElevatedButton(
            onPressed: _isSubmitting ? null : _submit,
            child: _isSubmitting
                ? const SizedBox(
                    width: 20,
                    height: 20,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Text('Submit Completion'),
          ),
        ],
      ),
    );
  }
}
