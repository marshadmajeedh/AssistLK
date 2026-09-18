import 'dart:typed_data';

import 'package:flutter/material.dart';

import '../../../../shared/theme/app_radius.dart';
import '../../../../shared/theme/app_spacing.dart';
import '../../../../shared/theme/app_text_styles.dart';
import '../../../../shared/widgets/app_button.dart';
import '../../../../shared/widgets/app_card.dart';
import '../models/problem_photo.dart';
import '../providers/problem_photos_controller.dart';

Future<void> chooseProblemPhoto(
  BuildContext context,
  ProblemPhotosController controller,
) async {
  final source = await showModalBottomSheet<PhotoSource>(
    context: context,
    builder: (sheet) => SafeArea(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          ListTile(
            leading: const Icon(Icons.photo_camera_outlined),
            title: const Text('Take Photo'),
            onTap: () => Navigator.pop(sheet, PhotoSource.camera),
          ),
          ListTile(
            leading: const Icon(Icons.photo_library_outlined),
            title: const Text('Choose from Gallery'),
            onTap: () => Navigator.pop(sheet, PhotoSource.gallery),
          ),
        ],
      ),
    ),
  );
  if (source != null && controller.active) await controller.pick(source);
}

/// Decode bounded previews and evict private image cache entries when their view closes.
class PrivatePhotoImage extends StatefulWidget {
  const PrivatePhotoImage({
    super.key,
    this.photo,
    this.bytes,
    this.preview = false,
  });
  final ProblemPhoto? photo;
  final Uint8List? bytes;
  final bool preview;
  @override
  State<PrivatePhotoImage> createState() => _PrivatePhotoImageState();
}

class _PrivatePhotoImageState extends State<PrivatePhotoImage> {
  ImageProvider? _image;
  bool _failed = false;
  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final bytes = widget.bytes ?? await widget.photo!.file.readAsBytes();
      if (!mounted) return;
      setState(
        () => _image = ResizeImage(
          MemoryImage(bytes),
          width: widget.preview ? 1200 : 256,
        ),
      );
    } catch (_) {
      if (mounted) setState(() => _failed = true);
    }
  }

  @override
  void dispose() {
    _image?.evict();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => ClipRRect(
    borderRadius: BorderRadius.circular(AppRadius.medium),
    child: _image != null
        ? Image(
            image: _image!,
            fit: widget.preview ? BoxFit.contain : BoxFit.cover,
            excludeFromSemantics: true,
            errorBuilder: (_, _, _) => const Icon(Icons.broken_image_outlined),
          )
        : _failed
        ? const Icon(Icons.broken_image_outlined)
        : const Center(
            child: SizedBox(
              width: 20,
              height: 20,
              child: CircularProgressIndicator(strokeWidth: 2),
            ),
          ),
  );
}

class DraftProblemPhotos extends StatelessWidget {
  const DraftProblemPhotos({
    super.key,
    required this.controller,
    this.review = false,
  });
  final ProblemPhotosController controller;
  final bool review;
  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: controller,
    builder: (context, _) {
      if (controller.sessionEnded) {
        return const Text('Your session has ended. Please sign in again.');
      }
      return AppCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              review ? 'Problem Photos' : 'Problem Photos (Optional)',
              style: AppTextStyles.cardHeading,
            ),
            const SizedBox(height: AppSpacing.xs),
            Text(
              review
                  ? (controller.uploads.isEmpty
                        ? 'No photos added'
                        : '${controller.uploads.length} selected')
                  : 'Add up to 3 photos to help AssistLK understand the issue.',
            ),
            const SizedBox(height: AppSpacing.sm),
            Wrap(
              spacing: AppSpacing.sm,
              runSpacing: AppSpacing.sm,
              children: [
                for (var i = 0; i < controller.uploads.length; i++)
                  SizedBox(
                    width: 96,
                    child: Column(
                      children: [
                        Semantics(
                          label: 'Selected problem photo ${i + 1}',
                          image: true,
                          child: SizedBox(
                            width: 96,
                            height: 88,
                            child: PrivatePhotoImage(
                              key: ValueKey(
                                controller.uploads[i].photo.identity,
                              ),
                              photo: controller.uploads[i].photo,
                            ),
                          ),
                        ),
                        if (!review)
                          IconButton(
                            tooltip: 'Remove selected photo ${i + 1}',
                            onPressed: controller.busy
                                ? null
                                : () => controller.removeSelected(
                                    controller.uploads[i].photo,
                                  ),
                            icon: const Icon(Icons.close),
                          ),
                      ],
                    ),
                  ),
              ],
            ),
            if (!review) ...[
              if (controller.uploads.length >= 3)
                const Text('Maximum 3 photos'),
              Semantics(
                label: 'Add problem photo',
                button: true,
                child: OutlinedButton.icon(
                  onPressed: controller.canAdd
                      ? () => chooseProblemPhoto(context, controller)
                      : null,
                  icon: const Icon(Icons.add_photo_alternate_outlined),
                  label: Text(
                    controller.picking ? 'Opening photos…' : 'Add Photo',
                  ),
                ),
              ),
            ],
            if (controller.error != null)
              Text(
                controller.error!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
          ],
        ),
      );
    },
  );
}

class PhotoUploadProgress extends StatelessWidget {
  const PhotoUploadProgress({
    super.key,
    required this.controller,
    required this.onContinue,
    required this.onRetry,
    this.allowRetry = true,
  });
  final ProblemPhotosController controller;
  final VoidCallback onContinue;
  final VoidCallback onRetry;
  final bool allowRetry;
  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: controller,
    builder: (context, _) => AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Semantics(
            liveRegion: true,
            child: Text(
              '${controller.uploadedCount} of ${controller.uploads.length} photos uploaded',
              style: AppTextStyles.cardHeading,
            ),
          ),
          if (controller.busy)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: AppSpacing.sm),
              child: LinearProgressIndicator(),
            ),
          for (var i = 0; i < controller.uploads.length; i++) ...[
            Text('Photo ${i + 1}: ${controller.uploads[i].state.name}'),
            if (controller.uploads[i].error != null)
              Padding(
                padding: const EdgeInsets.only(bottom: AppSpacing.sm),
                child: Text(
                  controller.uploads[i].error!,
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
              ),
          ],
          if (!controller.busy && controller.hasPending) ...[
            const SizedBox(height: AppSpacing.sm),
            if (allowRetry && !controller.uncertainUpload)
              AppButton(text: 'Retry failed photos', onPressed: onRetry),
            const SizedBox(height: AppSpacing.sm),
            AppButton(
              text: controller.uncertainUpload
                  ? 'Continue to Request Details'
                  : 'Continue without failed photos',
              onPressed: onContinue,
            ),
          ],
        ],
      ),
    ),
  );
}

class RequestProblemPhotos extends StatelessWidget {
  const RequestProblemPhotos({
    super.key,
    required this.controller,
    required this.editable,
    required this.onMutation,
  });
  final ProblemPhotosController controller;
  final bool editable;
  final Future<void> Function() onMutation;

  Future<void> _upload() async {
    await controller.submit();
    if (!controller.active) return;
    if (!controller.hasPending) controller.continueWithoutFailed();
    await controller.load();
    if (controller.active) await onMutation();
  }

  Future<void> _remove(BuildContext context, String id) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialog) => AlertDialog(
        title: const Text('Remove problem photo?'),
        content: const Text('This photo will be removed from your request.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialog, false),
            child: const Text('Keep Photo'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(dialog, true),
            child: const Text('Remove Photo'),
          ),
        ],
      ),
    );
    if (confirmed == true && controller.active) {
      await controller.delete(id);
      if (controller.active) await onMutation();
    }
  }

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: controller,
    builder: (context, _) {
      if (controller.sessionEnded) return const SizedBox.shrink();
      return AppCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('Problem Photos', style: AppTextStyles.cardHeading),
            const SizedBox(height: AppSpacing.sm),
            if (controller.loading) const LinearProgressIndicator(),
            if (controller.listError != null) ...[
              Text(controller.listError!),
              TextButton(
                onPressed: controller.load,
                child: const Text('Retry photos'),
              ),
            ] else if (!controller.loading && controller.attachments.isEmpty)
              const Text('No photos added'),
            Wrap(
              spacing: AppSpacing.sm,
              runSpacing: AppSpacing.sm,
              children: [
                for (final photo in controller.attachments.take(3))
                  SizedBox(
                    width: 96,
                    child: Column(
                      children: [
                        Semantics(
                          label: 'View problem photo ${photo.slot}',
                          button: true,
                          child: InkWell(
                            onTap: controller.content(photo.id) == null
                                ? null
                                : () => showDialog<void>(
                                    context: context,
                                    builder: (dialog) => ListenableBuilder(
                                      listenable: controller,
                                      builder: (context, _) => Dialog(
                                        child: Column(
                                          mainAxisSize: MainAxisSize.min,
                                          children: [
                                            Align(
                                              alignment: Alignment.centerRight,
                                              child: IconButton(
                                                tooltip: 'Close photo',
                                                icon: const Icon(Icons.close),
                                                onPressed: () =>
                                                    Navigator.pop(dialog),
                                              ),
                                            ),
                                            if (controller.active &&
                                                controller.content(photo.id) !=
                                                    null)
                                              Flexible(
                                                child: PrivatePhotoImage(
                                                  bytes: controller.content(
                                                    photo.id,
                                                  ),
                                                  preview: true,
                                                ),
                                              ),
                                          ],
                                        ),
                                      ),
                                    ),
                                  ),
                            child: SizedBox(
                              width: 96,
                              height: 88,
                              child: controller.content(photo.id) == null
                                  ? const Icon(Icons.image_outlined)
                                  : PrivatePhotoImage(
                                      key: ValueKey(photo.id),
                                      bytes: controller.content(photo.id),
                                    ),
                            ),
                          ),
                        ),
                        if (controller.contentError(photo.id) != null)
                          TextButton(
                            onPressed: () => controller.loadContent(photo.id),
                            child: const Text('Retry photo'),
                          ),
                        if (editable)
                          IconButton(
                            tooltip:
                                'Remove uploaded problem photo ${photo.slot}',
                            onPressed: controller.busy
                                ? null
                                : () => _remove(context, photo.id),
                            icon: const Icon(Icons.delete_outline),
                          ),
                      ],
                    ),
                  ),
              ],
            ),
            if (controller.error != null) Text(controller.error!),
            if (editable &&
                controller.listError == null &&
                !controller.hasPending) ...[
              if (controller.attachments.length >= 3)
                const Text('Maximum 3 photos'),
              OutlinedButton.icon(
                onPressed: controller.canAdd
                    ? () => chooseProblemPhoto(context, controller)
                    : null,
                icon: const Icon(Icons.add_photo_alternate_outlined),
                label: const Text('Add Photo'),
              ),
            ],
            if (controller.hasPending ||
                controller.busy && controller.uploads.isNotEmpty) ...[
              for (final item in controller.uploads.where(
                (p) => p.state != PhotoUploadState.uploaded,
              ))
                SizedBox(
                  width: 96,
                  height: 88,
                  child: PrivatePhotoImage(
                    key: ValueKey(item.photo.identity),
                    photo: item.photo,
                  ),
                ),
              if (editable &&
                  controller.uploads.every(
                    (p) => p.state == PhotoUploadState.pending,
                  ))
                AppButton(
                  text: 'Upload Photos',
                  onPressed: controller.busy ? null : _upload,
                )
              else
                PhotoUploadProgress(
                  controller: controller,
                  onRetry: _upload,
                  allowRetry: editable,
                  onContinue: () async {
                    controller.continueWithoutFailed();
                    await controller.load();
                    if (controller.active) await onMutation();
                  },
                ),
              if (!controller.busy &&
                  controller.uploads.every(
                    (p) => p.state == PhotoUploadState.pending,
                  ))
                TextButton(
                  onPressed: controller.continueWithoutFailed,
                  child: const Text('Discard selected photos'),
                ),
            ],
          ],
        ),
      );
    },
  );
}
