import 'package:flutter/material.dart';

class ProviderJobTrackingScreen extends StatefulWidget {
  final int jobId;
  const ProviderJobTrackingScreen({Key? key, required this.jobId}) : super(key: key);

  @override
  _ProviderJobTrackingScreenState createState() => _ProviderJobTrackingScreenState();
}

class _ProviderJobTrackingScreenState extends State<ProviderJobTrackingScreen> {
  String currentStatus = "Assigned";

  void updateStatus(String nextStatus) async {
    if (nextStatus == "Completed") {
      _showCompletionModal();
    } else {
      setState(() => currentStatus = nextStatus);
    }
  }

  void _showCompletionModal() {
    final summaryController = TextEditingController();
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      builder: (context) => Padding(
        padding: EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom, left: 16, right: 16, top: 16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Text("Complete Service Job", style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
            TextField(controller: summaryController, decoration: const InputDecoration(labelText: "Work Summary")),
            const SizedBox(height: 10),
            ElevatedButton.icon(
              onPressed: () {},
              icon: const Icon(Icons.camera_alt),
              label: const Text("Upload Proof of Work"),
            ),
            const SizedBox(height: 15),
            ElevatedButton(
              onPressed: () {
                Navigator.pop(context);
                setState(() => currentStatus = "Completed");
              },
              child: const Text("Submit Completion"),
            )
          ],
        ),
      ),
    );
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
              ElevatedButton(onPressed: () => updateStatus("OnTheWay"), child: const Text("Mark as On The Way")),
            if (currentStatus == "OnTheWay")
              ElevatedButton(onPressed: () => updateStatus("Arrived"), child: const Text("Mark as Arrived")),
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