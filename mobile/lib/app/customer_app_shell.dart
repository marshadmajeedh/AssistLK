import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'customer_bottom_navigation.dart';

import '../features/auth/screens/customer_account_screen.dart';
import '../features/service_requests/providers/service_request_provider.dart';
import '../features/service_requests/screens/customer_activity_screen.dart';
import '../features/service_requests/screens/customer_home_screen.dart';
import '../features/service_requests/screens/customer_services_screen.dart';

class CustomerAppShell extends StatefulWidget {
  const CustomerAppShell({super.key});

  @override
  State<CustomerAppShell> createState() => _CustomerAppShellState();
}

class _CustomerAppShellState extends State<CustomerAppShell> {
  int _selectedIndex = 0;
  static const _titles = ['Home', 'Services', 'Activity', 'Account'];

  @override
  void initState() {
    super.initState();
    // Exactly one automatic list load per shell/session. Tabs only consume it.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) context.read<ServiceRequestProvider>().loadMyRequests();
    });
  }

  @override
  Widget build(BuildContext context) {
    return PopScope<void>(
      canPop: _selectedIndex == 0,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop && _selectedIndex != 0) {
          setState(() => _selectedIndex = 0);
        }
      },
      child: Scaffold(
        appBar: AppBar(
          title: Text(
            _selectedIndex == 0 ? 'AssistLK Customer' : _titles[_selectedIndex],
          ),
        ),
        body: SafeArea(
          bottom: false,
          child: IndexedStack(
            index: _selectedIndex,
            children: [
              CustomerHomeScreen(
                onViewAll: () => setState(() => _selectedIndex = 2),
              ),
              const CustomerServicesScreen(),
              const CustomerActivityScreen(),
              const CustomerAccountScreen(),
            ],
          ),
        ),
        bottomNavigationBar: CustomerBottomNavigation(
          selectedIndex: _selectedIndex,
          onDestinationSelected: (index) =>
              setState(() => _selectedIndex = index),
        ),
      ),
    );
  }
}
