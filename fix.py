import re

with open('mobile/lib/features/providers/provider_home_screen.dart', 'r', encoding='utf-8') as f:
    content = f.read()

imports = """import 'screens/provider_dashboard_tab.dart';
import 'screens/provider_profile_tab.dart';
import '../quotations/screens/booking_management_tab.dart';"""

content = content.replace("import 'providers/web_notifications.dart';", f"import 'providers/web_notifications.dart';\n{imports}")
content = content.replace('class _ProviderHomeViewState extends State<_ProviderHomeView> {', 'class _ProviderHomeViewState extends State<_ProviderHomeView> {\n  int _currentIndex = 0;')

match = re.search(r'    return Scaffold\([\s\S]*', content)
if match:
    old_scaffold = match.group(0)
    
    new_scaffold = """    return Scaffold(
      backgroundColor: const Color(0xFFF8FAFC),
      body: Stack(
        children: [
          SafeArea(
            bottom: false,
            child: IndexedStack(
              index: _currentIndex,
              children: [
                const ProviderDashboardTab(),
                _buildMapTab(dashboard),
                const BookingManagementTab(),
                const ProviderProfileTab(),
              ],
            ),
          ),
          
          if (dashboard.isOnline && dashboard.activeJobMatch != null)
            Positioned(
              bottom: 16,
              left: 16,
              right: 16,
              child: SafeArea(
                child: Center(
                  child: ConstrainedBox(
                    constraints: const BoxConstraints(maxWidth: 580),
                    child: _buildJobAlertOrCockpit(dashboard),
                  ),
                ),
              ),
            ),
        ],
      ),
      bottomNavigationBar: Container(
        decoration: BoxDecoration(
          boxShadow: [
            BoxShadow(
              color: Colors.black.withOpacity(0.05),
              blurRadius: 10,
              offset: const Offset(0, -5),
            ),
          ],
        ),
        child: BottomNavigationBar(
          currentIndex: _currentIndex,
          onTap: (index) {
            setState(() {
              _currentIndex = index;
            });
          },
          type: BottomNavigationBarType.fixed,
          backgroundColor: Colors.white,
          selectedItemColor: const Color(0xFF1F4E78),
          unselectedItemColor: Colors.grey.shade400,
          selectedLabelStyle: const TextStyle(fontWeight: FontWeight.bold, fontSize: 11),
          unselectedLabelStyle: const TextStyle(fontWeight: FontWeight.normal, fontSize: 11),
          elevation: 0,
          items: const [
            BottomNavigationBarItem(
              icon: Icon(Icons.dashboard_rounded),
              label: 'Dashboard',
            ),
            BottomNavigationBarItem(
              icon: Icon(Icons.map_rounded),
              label: 'Dispatch Map',
            ),
            BottomNavigationBarItem(
              icon: Icon(Icons.receipt_long_rounded),
              label: 'Bookings',
            ),
            BottomNavigationBarItem(
              icon: Icon(Icons.person_rounded),
              label: 'Profile',
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildJobAlertOrCockpit(ProviderDashboardProvider dashboard) {
    return (!widget.hasAcceptedActiveMatch && dashboard.activeJobMatch!['status'] != 'Accepted')
      ? JobAlertCard(
          category: dashboard.activeJobMatch!['category']?.toString() ?? 'Service Request',
          distance: dashboard.liveDistanceKm != null
              ? '${dashboard.liveDistanceKm!.toStringAsFixed(1)} km'
              : (dashboard.activeJobMatch!['distanceKm'] != null
                  ? '${dashboard.activeJobMatch!['distanceKm']} km'
                  : 'Nearby'),
          urgency: dashboard.activeJobMatch!['urgency']?.toString() ?? 'Standard',
          description: dashboard.activeJobMatch!['description']?.toString(),
          aiRationale: (dashboard.activeJobMatch!['detectedProblem'] ??
                  dashboard.activeJobMatch!['aiRationale'] ??
                  dashboard.activeJobMatch!['rationale'])
              ?.toString(),
          isOutOfRange: dashboard.liveDistanceKm != null &&
              dashboard.liveDistanceKm! > dashboard.operatingRadiusKm,
          remainingSeconds: dashboard.remainingAcceptSeconds,
          totalTimeoutSeconds: dashboard.totalTimeoutSeconds,
          onAccept: widget.onAcceptMatch,
          onDecline: widget.onDeclineMatch,
        )
      : Card(
          elevation: 8,
          shadowColor: Colors.black26,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(18),
          ),
          color: Colors.white,
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Row(
                      children: [
                        Container(
                          padding: const EdgeInsets.all(8),
                          decoration: BoxDecoration(
                            color: Colors.green.shade50,
                            borderRadius: BorderRadius.circular(10),
                          ),
                          child: Icon(Icons.handyman, color: Colors.green.shade700, size: 22),
                        ),
                        const SizedBox(width: 12),
                        const Text(
                          'Active Job',
                          style: TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.bold,
                            color: Colors.black87,
                          ),
                        ),
                      ],
                    ),
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                      decoration: BoxDecoration(
                        color: Colors.blue.shade50,
                        borderRadius: BorderRadius.circular(20),
                        border: Border.all(color: Colors.blue.shade200),
                      ),
                      child: Text(
                        dashboard.activeJobMatch!['status'] ?? 'In Progress',
                        style: TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.bold,
                          color: Colors.blue.shade700,
                        ),
                      ),
                    ),
                  ],
                ),
                const Divider(height: 24),
                Text(
                  dashboard.activeJobMatch!['description']?.toString() ?? 'Service requested',
                  style: TextStyle(fontSize: 13, color: Colors.grey.shade700),
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                ),
                const SizedBox(height: 16),
                SizedBox(
                  width: double.infinity,
                  child: ElevatedButton.icon(
                    style: ElevatedButton.styleFrom(
                      backgroundColor: Colors.green.shade600,
                      foregroundColor: Colors.white,
                      padding: const EdgeInsets.symmetric(vertical: 14),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(12),
                      ),
                    ),
                    icon: const Icon(Icons.check_circle_outline),
                    label: const Text(
                      'Mark Job as Completed',
                      style: TextStyle(fontSize: 15, fontWeight: FontWeight.bold),
                    ),
                    onPressed: () async {
                      try {
                        await dashboard.completeJob();
                      } catch (e) {
                        debugPrint('Error marking completed: $e');
                      }
                      dashboard.clearActiveJobMatch();
                    },
                  ),
                ),
              ],
            ),
          ),
        );
  }

  Widget _buildMapTab(ProviderDashboardProvider dashboard) {
"""
    map_code_start = old_scaffold.find('FlutterMap(')
    end_boundary = old_scaffold.rfind('          if (dashboard.isOnline')
    if end_boundary == -1:
        end_boundary = len(old_scaffold)
    map_content = old_scaffold[map_code_start:end_boundary]
    
    new_scaffold += '''    return Stack(
      children: [
''' + map_content + '''
      ],
    );
  }
}
'''
    content = content[:match.start()] + new_scaffold

with open('mobile/lib/features/providers/provider_home_screen.dart', 'w', encoding='utf-8') as f:
    f.write(content)
print("Done")
