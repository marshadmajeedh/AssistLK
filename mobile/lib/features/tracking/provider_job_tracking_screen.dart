import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

import '../../core/api/api_client.dart';
import '../providers/services/provider_service.dart';
import 'services/provider_location_stream_service.dart';

class ProviderJobTrackingScreen extends StatefulWidget {
  final String jobId;
  const ProviderJobTrackingScreen({super.key, required this.jobId});

  @override
  State<ProviderJobTrackingScreen> createState() => _ProviderJobTrackingScreenState();
}

class _ProviderJobTrackingScreenState extends State<ProviderJobTrackingScreen> {
  String currentStatus = "Assigned";
  late final ProviderLocationStreamService _locationStreamService;
  bool _isUpdatingStatus = false;

  @override
  void initState() {
    super.initState();
    _locationStreamService = ProviderLocationStreamService();
  }

  Future<void> updateStatus(String nextStatus) async {
    if (nextStatus == "Completed") {
      await _showCompletionModal();
      return;
    }

    if (_isUpdatingStatus) return;
    setState(() => _isUpdatingStatus = true);
    try {
      await ProviderService(apiClient: ApiClient()).updateJobStatus(
        widget.jobId,
        nextStatus,
      );

      if (nextStatus == 'OnTheWay') {
        await _locationStreamService.start(
          jobId: widget.jobId,
          isOnTheWay: true,
          onStopped: () {
            if (mounted) setState(() => currentStatus = 'Arrived');
          },
        );
      } else if (nextStatus == 'Arrived') {
        await _locationStreamService.stop();
      }

      if (mounted) setState(() => currentStatus = nextStatus);
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Status update failed: $error')),
        );
      }
    } finally {
      if (mounted) setState(() => _isUpdatingStatus = false);
    }
  }

  Future<void> _showCompletionModal() async {
    final summaryController = TextEditingController();
    XFile? selectedImage;
    bool isSubmitting = false;

    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      builder: (modalContext) => StatefulBuilder(
        builder: (modalContext, setModalState) {
          Future<void> pickAndUpload(ImageSource source) async {
            final picked = await ImagePicker().pickImage(
              source: source,
              imageQuality: 85,
            );
            if (picked == null) return;

            setModalState(() {
              selectedImage = picked;
            });
          }

          Future<void> submitCompletion() async {
            if (selectedImage == null || isSubmitting) {
              ScaffoldMessenger.of(context).showSnackBar(
                const SnackBar(content: Text('Upload proof of work before submitting.')),
              );
              return;
            }

            final providerService = ProviderService(apiClient: ApiClient());
            setModalState(() => isSubmitting = true);
            try {
              await providerService.completeJob(
                widget.jobId,
                notes: summaryController.text.trim().isEmpty
                    ? 'Work completed'
                    : summaryController.text.trim(),
                proofOfWorkImage: selectedImage,
              );

              if (!mounted) return;

              if (!modalContext.mounted) return;
              Navigator.pop(modalContext);
              await _locationStreamService.stop();
              setState(() => currentStatus = 'Completed');
            } catch (error) {
              if (!mounted || !modalContext.mounted) return;
              setModalState(() => isSubmitting = false);
              ScaffoldMessenger.of(context).showSnackBar(
                SnackBar(content: Text('Completion failed: $error')),
              );
            }
          }

          return Padding(
            padding: EdgeInsets.only(
              bottom: MediaQuery.of(modalContext).viewInsets.bottom,
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
                  controller: summaryController,
                  enabled: !isSubmitting,
                  decoration: const InputDecoration(labelText: 'Work Summary'),
                ),
                const SizedBox(height: 10),
                if (selectedImage != null)
                  Text(
                    'Selected: ${selectedImage!.name}',
                  ),
                ElevatedButton.icon(
                  onPressed: isSubmitting ? null : () async {
                          final source = await showModalBottomSheet<ImageSource>(
                            context: modalContext,
                            builder: (pickerContext) => SafeArea(
                              child: Wrap(
                                children: [
                                  ListTile(
                                    leading: const Icon(Icons.camera_alt),
                                    title: const Text('Take photo'),
                                    onTap: () => Navigator.pop(
                                      pickerContext,
                                      ImageSource.camera,
                                    ),
                                  ),
                                  ListTile(
                                    leading: const Icon(Icons.photo_library),
                                    title: const Text('Choose from gallery'),
                                    onTap: () => Navigator.pop(
                                      pickerContext,
                                      ImageSource.gallery,
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          );
                          if (source != null) await pickAndUpload(source);
                        },
                  icon: const Icon(Icons.camera_alt),
                  label: const Text('Upload Proof of Work'),
                ),
                const SizedBox(height: 15),
                ElevatedButton(
                  onPressed: isSubmitting ? null : submitCompletion,
                  child: isSubmitting
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
        },
      ),
    );
    summaryController.dispose();
  }

  @override
  void dispose() {
    _locationStreamService.stop();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text("Job #${widget.jobId} Status")),
      body: Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Text("Current Status: $currentStatus", style: const TextStyle(fontSize: 20, fontWeight: FontWeight.bold)),
            const SizedBox(height: 30),
            if (currentStatus == "Assigned")
              ElevatedButton(
                onPressed: _isUpdatingStatus ? null : () => updateStatus("OnTheWay"),
                child: const Text("Mark as On The Way"),
              ),
            if (currentStatus == "OnTheWay")
              ElevatedButton(
                onPressed: _isUpdatingStatus ? null : () => updateStatus("Arrived"),
                child: const Text("Mark as Arrived"),
              ),
            if (currentStatus == "Arrived")
              ElevatedButton(onPressed: () => updateStatus("InProgress"), child: const Text("Start Work (In Progress)")),
            if (currentStatus == "InProgress")
              ElevatedButton(onPressed: () => updateStatus("Completed"), child: const Text("Mark as Completed")),
          ],
        ),
      ),
    );
  }
}