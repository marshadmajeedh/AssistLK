import 'package:flutter/material.dart';
import 'package:geocoding/geocoding.dart';
import 'package:geolocator/geolocator.dart';
import 'package:dio/dio.dart';
import 'package:provider/provider.dart';

import '../providers/provider_dashboard_provider.dart';

class UpdateProfileBottomSheet extends StatefulWidget {
  const UpdateProfileBottomSheet({super.key});

  @override
  State<UpdateProfileBottomSheet> createState() =>
      _UpdateProfileBottomSheetState();
}

class _UpdateProfileBottomSheetState extends State<UpdateProfileBottomSheet> {
  final _formKey = GlobalKey<FormState>();
  final TextEditingController _businessNameController = TextEditingController();
  final TextEditingController _addressController = TextEditingController();
  final Dio _dio = Dio();

  late double _operatingRadiusKm;
  double? _latitude;
  double? _longitude;
  bool _isResolvingLocation = false;
  bool _isSaving = false;
  String? _resolvedAddressSummary;

  @override
  void initState() {
    super.initState();
    final dashboard = context.read<ProviderDashboardProvider>();
    _businessNameController.text = dashboard.businessName;
    _operatingRadiusKm = dashboard.operatingRadiusKm.clamp(1.0, 50.0);
    _latitude = dashboard.latitude;
    _longitude = dashboard.longitude;
    if (_latitude != null && _longitude != null && _latitude != 0 && _longitude != 0) {
      _resolvedAddressSummary =
          'Lat: ${_latitude!.toStringAsFixed(4)}, Lng: ${_longitude!.toStringAsFixed(4)}';
    }
  }

  @override
  void dispose() {
    _businessNameController.dispose();
    _addressController.dispose();
    super.dispose();
  }

  Future<void> _searchAddress() async {
    final addressText = _addressController.text.trim();
    if (addressText.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Please enter an address to search.')),
      );
      return;
    }

    setState(() => _isResolvingLocation = true);

    try {
      final locations = await locationFromAddress(addressText);
      if (locations.isNotEmpty) {
        final loc = locations.first;
        if (mounted) {
          setState(() {
            _latitude = loc.latitude;
            _longitude = loc.longitude;
            _resolvedAddressSummary =
                'Lat: ${_latitude!.toStringAsFixed(4)}, Lng: ${_longitude!.toStringAsFixed(4)}';
            _isResolvingLocation = false;
          });
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              backgroundColor: Colors.green,
              content: Text('Address geocoded successfully!'),
            ),
          );
          return;
        }
      }
    } catch (e) {
      debugPrint('Native geocoding failed, trying fallback: $e');
    }

    // Nominatim Fallback
    try {
      final response = await _dio.get(
        'https://nominatim.openstreetmap.org/search?q=${Uri.encodeComponent(addressText)}&format=json&limit=1',
        options: Options(
          headers: {'User-Agent': 'AssistLK-Mobile/1.0'},
          receiveTimeout: const Duration(seconds: 10),
          sendTimeout: const Duration(seconds: 10),
        ),
      );

      if (response.statusCode == 200 &&
          response.data is List &&
          (response.data as List).isNotEmpty) {
        final item = response.data[0];
        final double? lat = double.tryParse(item['lat'].toString());
        final double? lon = double.tryParse(item['lon'].toString());
        if (lat != null && lon != null && mounted) {
          setState(() {
            _latitude = lat;
            _longitude = lon;
            _resolvedAddressSummary =
                'Lat: ${_latitude!.toStringAsFixed(4)}, Lng: ${_longitude!.toStringAsFixed(4)}';
            _isResolvingLocation = false;
          });
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              backgroundColor: Colors.green,
              content: Text('Address geocoded via Nominatim!'),
            ),
          );
          return;
        }
      }

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            backgroundColor: Colors.orange,
            content: Text('Could not resolve location. Try using Device GPS.'),
          ),
        );
      }
    } catch (e) {
      debugPrint('Nominatim geocoding failed: $e');
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Address search failed: $e')),
        );
      }
    } finally {
      if (mounted) {
        setState(() => _isResolvingLocation = false);
      }
    }
  }

  Future<void> _useCurrentGPS() async {
    setState(() => _isResolvingLocation = true);
    try {
      final pos = await Geolocator.getCurrentPosition();
      if (mounted) {
        setState(() {
          _latitude = pos.latitude;
          _longitude = pos.longitude;
          _resolvedAddressSummary =
              'Lat: ${_latitude!.toStringAsFixed(4)}, Lng: ${_longitude!.toStringAsFixed(4)}';
          _isResolvingLocation = false;
        });
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            backgroundColor: Colors.green,
            content: Text('Current GPS position acquired!'),
          ),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Failed to get GPS location: $e')),
        );
      }
    } finally {
      if (mounted) {
        setState(() => _isResolvingLocation = false);
      }
    }
  }

  Future<void> _saveProfile() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() => _isSaving = true);
    try {
      final dashboard = context.read<ProviderDashboardProvider>();
      await dashboard.updateProfile(
        businessName: _businessNameController.text.trim(),
        operatingRadiusKm: _operatingRadiusKm,
        latitude: _latitude,
        longitude: _longitude,
      );

      if (!mounted) return;
      Navigator.pop(context);
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          backgroundColor: Colors.green,
          content: Text('Provider profile & territory updated successfully!'),
        ),
      );
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          backgroundColor: Colors.red,
          content: Text('Update failed: $e'),
        ),
      );
    } finally {
      if (mounted) {
        setState(() => _isSaving = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.only(
        bottom: MediaQuery.of(context).viewInsets.bottom,
      ),
      child: SingleChildScrollView(
        child: Padding(
          padding: const EdgeInsets.all(20),
          child: Form(
            key: _formKey,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    const Text(
                      'Update Profile & Territory',
                      style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                    ),
                    IconButton(
                      icon: const Icon(Icons.close),
                      onPressed: () => Navigator.pop(context),
                    ),
                  ],
                ),
                const Divider(),
                const SizedBox(height: 10),

                // Business Name Field
                TextFormField(
                  controller: _businessNameController,
                  decoration: const InputDecoration(
                    labelText: 'Business Name',
                    hintText: 'e.g., Sunil Quick Repairs & Drainage',
                    prefixIcon: Icon(Icons.business),
                    border: OutlineInputBorder(),
                  ),
                  validator: (val) {
                    if (val == null || val.trim().isEmpty) {
                      return 'Business Name is required';
                    }
                    if (val.trim().length < 3) {
                      return 'Business Name must be at least 3 characters';
                    }
                    return null;
                  },
                ),
                const SizedBox(height: 16),

                // Address Field with Geocoding
                TextFormField(
                  controller: _addressController,
                  decoration: InputDecoration(
                    labelText: 'Update Base Address',
                    hintText: 'e.g., 45 Galle Road, Colombo 03',
                    prefixIcon: const Icon(Icons.location_on_outlined),
                    border: const OutlineInputBorder(),
                    suffixIcon: IconButton(
                      icon: _isResolvingLocation
                          ? const SizedBox(
                              width: 18,
                              height: 18,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Icon(Icons.search),
                      tooltip: 'Geocode Address',
                      onPressed: _isResolvingLocation ? null : _searchAddress,
                    ),
                  ),
                  onFieldSubmitted: (_) => _searchAddress(),
                ),
                const SizedBox(height: 8),

                OutlinedButton.icon(
                  icon: const Icon(Icons.my_location, size: 18),
                  label: const Text('Use Current Device GPS'),
                  onPressed: _isResolvingLocation ? null : _useCurrentGPS,
                ),

                if (_resolvedAddressSummary != null) ...[
                  const SizedBox(height: 8),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                    decoration: BoxDecoration(
                      color: Colors.green.shade50,
                      borderRadius: BorderRadius.circular(8),
                      border: Border.all(color: Colors.green.shade300),
                    ),
                    child: Row(
                      children: [
                        Icon(Icons.check_circle, size: 16, color: Colors.green.shade700),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            'Active Base: $_resolvedAddressSummary',
                            style: TextStyle(
                              fontSize: 12,
                              fontWeight: FontWeight.bold,
                              color: Colors.green.shade900,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ],

                const SizedBox(height: 16),

                // Operating Radius Slider (1–50 km)
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    const Text(
                      'Operating Radius',
                      style: TextStyle(fontWeight: FontWeight.bold, fontSize: 14),
                    ),
                    Text(
                      '${_operatingRadiusKm.toInt()} km',
                      style: TextStyle(
                        fontWeight: FontWeight.bold,
                        color: Colors.blue.shade700,
                      ),
                    ),
                  ],
                ),
                Slider(
                  value: _operatingRadiusKm,
                  min: 1.0,
                  max: 50.0,
                  divisions: 49,
                  label: '${_operatingRadiusKm.toInt()} km',
                  onChanged: (val) => setState(() => _operatingRadiusKm = val),
                ),

                const SizedBox(height: 16),

                ElevatedButton(
                  style: ElevatedButton.styleFrom(
                    backgroundColor: Colors.blue.shade700,
                    foregroundColor: Colors.white,
                    padding: const EdgeInsets.symmetric(vertical: 14),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(8),
                    ),
                  ),
                  onPressed: _isSaving ? null : _saveProfile,
                  child: _isSaving
                      ? const SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            color: Colors.white,
                          ),
                        )
                      : const Text('Save Changes', style: TextStyle(fontSize: 15)),
                ),
                const SizedBox(height: 10),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
