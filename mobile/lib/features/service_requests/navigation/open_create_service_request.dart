import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../customer/providers/customer_location_provider.dart';
import '../screens/create_service_request_screen.dart';

void openCreateServiceRequest(BuildContext context, {String? categoryHint}) {
  final snapshot = context.read<CustomerLocationProvider?>()?.freshSuggestion;
  Navigator.of(context).push(
    MaterialPageRoute<void>(
      builder: (_) => CreateServiceRequestScreen(
        initialCategoryPreference: categoryHint,
        initialLocationSuggestion: snapshot,
      ),
    ),
  );
}
