import 'dart:typed_data';
import 'package:flutter/material.dart';
import 'package:file_picker/file_picker.dart';
import 'package:geocoding/geocoding.dart';
import 'package:geolocator/geolocator.dart';
import 'package:dio/dio.dart';

import 'services/provider_registration_service.dart';

class ProviderRegistrationScreen extends StatefulWidget {
  const ProviderRegistrationScreen({super.key});

  @override
  State<ProviderRegistrationScreen> createState() =>
      _ProviderRegistrationScreenState();
}

class _ProviderRegistrationScreenState
    extends State<ProviderRegistrationScreen> {
  int _currentStep = 0;

  final _step1Key = GlobalKey<FormState>();
  final _step2Key = GlobalKey<FormState>();
  final _step3Key = GlobalKey<FormState>();

  final Dio _dio = Dio();
  late final ProviderRegistrationService _apiService;

  // Step 1: Credentials
  String _fullName = '';
  String _email = '';
  String _password = '';
  String _phone = '';
  bool _obscurePassword = true;

  // Step 2: Business & Coverage Location
  String _businessName = '';
  final TextEditingController _addressController = TextEditingController();
  double _radiusKm = 10.0;
  double _latitude = 0.0;
  double _longitude = 0.0;
  bool _isLocationResolved = false;
  bool _isResolvingLocation = false;

  // Step 3: Skills & Certificates
  String _selectedCategory = 'Plumbing';
  final List<String> _categories = [
    'Plumbing',
    'Electrical',
    'Vehicle Assistance',
    'Appliance Repair',
  ];
  String _skillName = '';
  Uint8List? _pdfBytes;
  String? _pdfPath;
  String? _pdfFileName;
  bool _isUploading = false;

  @override
  void initState() {
    super.initState();
    _apiService = ProviderRegistrationService();
  }

  @override
  void dispose() {
    _addressController.dispose();
    super.dispose();
  }

  // --- Geocoding & GPS Location Strategies ---

  Future<void> _searchAddressGeocoding() async {
    final addressText = _addressController.text.trim();
    if (addressText.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Please enter an address to search.')),
      );
      return;
    }

    setState(() => _isResolvingLocation = true);

    // 1. Primary: Native Geocoding
    try {
      final List<Location> locations = await locationFromAddress(addressText);
      if (locations.isNotEmpty) {
        final loc = locations.first;
        if (mounted) {
          setState(() {
            _latitude = loc.latitude;
            _longitude = loc.longitude;
            _isLocationResolved = true;
          });
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              backgroundColor: Colors.green,
              content: Text('Address location resolved via native geocoding!'),
            ),
          );
        }
        return;
      }
    } catch (e) {
      debugPrint('Native geocoding failed, trying OpenStreetMap Nominatim: $e');
    }

    // 2. Fallback: OpenStreetMap Nominatim HTTP lookup
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
            _isLocationResolved = true;
          });
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              backgroundColor: Colors.green,
              content: Text('Address location resolved via Nominatim fallback!'),
            ),
          );
          return;
        }
      }

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            backgroundColor: Colors.orange,
            content: Text(
              'Could not resolve coordinates for this address. Try refining or use GPS.',
            ),
          ),
        );
      }
    } catch (e) {
      debugPrint('Nominatim geocoding failed: $e');
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Geocoding search failed: $e')),
        );
      }
    } finally {
      if (mounted) {
        setState(() => _isResolvingLocation = false);
      }
    }
  }

  Future<void> _fetchGPSLocation() async {
    setState(() => _isResolvingLocation = true);
    try {
      final bool serviceEnabled = await Geolocator.isLocationServiceEnabled();
      if (!serviceEnabled) {
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Location services are disabled. Please enable GPS.'),
          ),
        );
        return;
      }

      LocationPermission permission = await Geolocator.checkPermission();
      if (permission == LocationPermission.denied) {
        permission = await Geolocator.requestPermission();
        if (permission == LocationPermission.denied) {
          if (!mounted) return;
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('Location permissions are denied.')),
          );
          return;
        }
      }

      if (permission == LocationPermission.deniedForever) {
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Location permissions are permanently denied.'),
          ),
        );
        return;
      }

      final Position position = await Geolocator.getCurrentPosition();
      if (mounted) {
        setState(() {
          _latitude = position.latitude;
          _longitude = position.longitude;
          _isLocationResolved = true;
        });
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            backgroundColor: Colors.green,
            content: Text('GPS coordinates acquired successfully!'),
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

  // --- PDF File Picker ---

  Future<void> _pickFile() async {
    try {
      final PlatformFile? file = await FilePicker.pickFile(
        type: FileType.custom,
        allowedExtensions: ['pdf'],
      );

      if (file != null) {
        final int fileLength = file.lengthSync() ?? await file.length() ?? 0;
        // Size check ~ 5MB cap
        if (fileLength > 5 * 1024 * 1024) {
          if (!mounted) return;
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              backgroundColor: Colors.red,
              content: Text('File exceeds the 5MB maximum size limit.'),
            ),
          );
          return;
        }

        // Read bytes directly for robust cross-platform upload support
        final Uint8List bytes = await file.readAsBytes();

        setState(() {
          _pdfBytes = bytes;
          _pdfPath = file.path ?? file.name;
          _pdfFileName = file.name;
        });
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Failed to pick file: $e')),
        );
      }
    }
  }

  // --- Final Submission ---

  Future<void> _submitRegistration() async {
    if (!_step3Key.currentState!.validate()) return;
    _step3Key.currentState!.save();

    if (_pdfBytes == null && _pdfPath == null && _pdfFileName == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          backgroundColor: Colors.red,
          content: Text('Please upload a PDF certificate before submitting.'),
        ),
      );
      return;
    }

    if (!_isLocationResolved || (_latitude == 0.0 && _longitude == 0.0)) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          backgroundColor: Colors.red,
          content: Text('Valid coordinates are required. Please resolve your location.'),
        ),
      );
      return;
    }

    setState(() => _isUploading = true);

    try {
      final String? certUrl = await _apiService.uploadCertificate(
        filePath: _pdfPath,
        bytes: _pdfBytes,
        fileName: _pdfFileName,
      );

      final payload = {
        'fullName': _fullName,
        'email': _email,
        'password': _password,
        'phoneNumber': _phone,
        'businessName': _businessName,
        'latitude': _latitude,
        'longitude': _longitude,
        'operatingRadiusKm': _radiusKm,
        'skills': [
          {
            'category': _selectedCategory,
            'skillName': _skillName,
            'certificationUrl': certUrl,
          },
        ],
      };

      await _apiService.registerProvider(payload);

      if (!mounted) return;
      showDialog(
        context: context,
        barrierDismissible: false,
        builder: (dialogContext) => AlertDialog(
          title: const Row(
            children: [
              Icon(Icons.check_circle, color: Colors.green),
              SizedBox(width: 8),
              Text('Application Submitted'),
            ],
          ),
          content: const Text(
            'Your registration has been submitted successfully and is pending Admin Review.\n\nOnce approved, your account will be activated.',
          ),
          actions: [
            ElevatedButton(
              onPressed: () {
                Navigator.pop(dialogContext);
                Navigator.pop(context); // Return to login / auth selection
              },
              child: const Text('Understood'),
            ),
          ],
        ),
      );
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          backgroundColor: Colors.red,
          content: Text('Registration Failed: $e'),
        ),
      );
    } finally {
      if (mounted) {
        setState(() => _isUploading = false);
      }
    }
  }

  void _onStepContinue() {
    if (_currentStep == 0) {
      if (!_step1Key.currentState!.validate()) return;
      _step1Key.currentState!.save();
      setState(() => _currentStep = 1);
    } else if (_currentStep == 1) {
      if (!_step2Key.currentState!.validate()) return;
      _step2Key.currentState!.save();

      if (!_isLocationResolved || (_latitude == 0.0 && _longitude == 0.0)) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            backgroundColor: Colors.red,
            content: Text(
              'Please resolve your base location via Address Search or Device GPS before continuing.',
            ),
          ),
        );
        return;
      }
      setState(() => _currentStep = 2);
    } else {
      _submitRegistration();
    }
  }

  void _onStepCancel() {
    if (_currentStep > 0) {
      setState(() => _currentStep -= 1);
    } else {
      Navigator.pop(context);
    }
  }

  @override
  Widget build(BuildContext context) {
    final bool hasFile =
        _pdfBytes != null || _pdfPath != null || _pdfFileName != null;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Provider Registration'),
      ),
      body: Stepper(
        currentStep: _currentStep,
        onStepContinue: _isUploading ? null : _onStepContinue,
        onStepCancel: _isUploading ? null : _onStepCancel,
        steps: [
          // -------------------------------------------------------------
          // STEP 1: Credentials (Instant Real-Time Keystroke Validation)
          // -------------------------------------------------------------
          Step(
            title: const Text('Credentials'),
            subtitle: const Text('Identity & account setup'),
            isActive: _currentStep >= 0,
            state: _currentStep > 0 ? StepState.complete : StepState.indexed,
            content: Form(
              key: _step1Key,
              autovalidateMode: AutovalidateMode.onUserInteraction,
              child: Column(
                children: [
                  TextFormField(
                    decoration: const InputDecoration(
                      labelText: 'Full Name',
                      hintText: 'e.g., Kasun Perera',
                      prefixIcon: Icon(Icons.person_outline),
                    ),
                    textCapitalization: TextCapitalization.words,
                    validator: (val) {
                      if (val == null || val.trim().isEmpty) {
                        return 'Full Name is required';
                      }
                      if (val.trim().length < 3) {
                        return 'Full Name must be at least 3 characters';
                      }
                      if (!RegExp(r'^[a-zA-Z\s]+$').hasMatch(val.trim())) {
                        return 'Alphabetic characters and spaces only';
                      }
                      return null;
                    },
                    onSaved: (val) => _fullName = val?.trim() ?? '',
                  ),
                  const SizedBox(height: 14),
                  TextFormField(
                    decoration: const InputDecoration(
                      labelText: 'Email Address',
                      hintText: 'e.g., kasun@example.com',
                      prefixIcon: Icon(Icons.email_outlined),
                    ),
                    keyboardType: TextInputType.emailAddress,
                    validator: (val) {
                      if (val == null || val.trim().isEmpty) {
                        return 'Email is required';
                      }
                      if (!RegExp(r'^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$')
                          .hasMatch(val.trim())) {
                        return 'Enter a valid RFC 5322 email address';
                      }
                      return null;
                    },
                    onSaved: (val) => _email = val?.trim() ?? '',
                  ),
                  const SizedBox(height: 14),
                  TextFormField(
                    decoration: InputDecoration(
                      labelText: 'Password',
                      hintText: 'Min 8 chars, 1 uppercase, 1 lowercase, 1 digit, 1 symbol',
                      prefixIcon: const Icon(Icons.lock_outline),
                      suffixIcon: IconButton(
                        icon: Icon(
                          _obscurePassword
                              ? Icons.visibility_outlined
                              : Icons.visibility_off_outlined,
                        ),
                        onPressed: () =>
                            setState(() => _obscurePassword = !_obscurePassword),
                      ),
                    ),
                    obscureText: _obscurePassword,
                    validator: (val) {
                      if (val == null || val.isEmpty) {
                        return 'Password is required';
                      }
                      if (val.length < 8) {
                        return 'Minimum 8 characters required';
                      }
                      if (!RegExp(r'[A-Z]').hasMatch(val)) {
                        return 'Must contain at least 1 uppercase letter';
                      }
                      if (!RegExp(r'[a-z]').hasMatch(val)) {
                        return 'Must contain at least 1 lowercase letter';
                      }
                      if (!RegExp(r'\d').hasMatch(val)) {
                        return 'Must contain at least 1 digit';
                      }
                      if (!RegExp(r'[!@#\$%^&*(),.?":{}|<>]').hasMatch(val)) {
                        return 'Must contain at least 1 special symbol';
                      }
                      return null;
                    },
                    onSaved: (val) => _password = val ?? '',
                  ),
                  const SizedBox(height: 14),
                  TextFormField(
                    decoration: const InputDecoration(
                      labelText: 'Sri Lankan Phone Number',
                      hintText: '07XXXXXXXX or +947XXXXXXXX',
                      prefixIcon: Icon(Icons.phone_outlined),
                    ),
                    keyboardType: TextInputType.phone,
                    validator: (val) {
                      if (val == null || val.trim().isEmpty) {
                        return 'Phone number is required';
                      }
                      if (!RegExp(r'^(?:\+94|0)[7][0-9]{8}$')
                          .hasMatch(val.trim())) {
                        return 'Must match format 07XXXXXXXX or +947XXXXXXXX';
                      }
                      return null;
                    },
                    onSaved: (val) => _phone = val?.trim() ?? '',
                  ),
                ],
              ),
            ),
          ),

          // -------------------------------------------------------------
          // STEP 2: Business Profile & Hybrid Base Location Acquisition
          // -------------------------------------------------------------
          Step(
            title: const Text('Business & Coverage'),
            subtitle: const Text('Operating area & location setup'),
            isActive: _currentStep >= 1,
            state: _currentStep > 1 ? StepState.complete : StepState.indexed,
            content: Form(
              key: _step2Key,
              autovalidateMode: AutovalidateMode.onUserInteraction,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  TextFormField(
                    decoration: const InputDecoration(
                      labelText: 'Business Name',
                      hintText: 'e.g., QuickFix Plumbing Services',
                      prefixIcon: Icon(Icons.business_outlined),
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
                    onSaved: (val) => _businessName = val?.trim() ?? '',
                  ),
                  const SizedBox(height: 18),
                  const Text(
                    'Base Location Acquisition',
                    style: TextStyle(fontWeight: FontWeight.bold, fontSize: 14),
                  ),
                  const SizedBox(height: 6),
                  const Text(
                    'Provide your base address or use your current GPS location to receive dispatches in your area.',
                    style: TextStyle(fontSize: 12, color: Colors.black54),
                  ),
                  const SizedBox(height: 12),

                  // Option A: Free-text Address with Geocoding
                  TextFormField(
                    controller: _addressController,
                    decoration: InputDecoration(
                      labelText: 'Business / Workshop / Home Address',
                      hintText: 'e.g., No. 45, Temple Road, Battaramulla',
                      prefixIcon: const Icon(Icons.location_city_outlined),
                      suffixIcon: IconButton(
                        icon: _isResolvingLocation
                            ? const SizedBox(
                                width: 20,
                                height: 20,
                                child: CircularProgressIndicator(strokeWidth: 2),
                              )
                            : const Icon(Icons.search),
                        tooltip: 'Search Address Geocoding',
                        onPressed: _isResolvingLocation
                            ? null
                            : _searchAddressGeocoding,
                      ),
                    ),
                    onFieldSubmitted: (_) => _searchAddressGeocoding(),
                  ),
                  const SizedBox(height: 10),

                  // Option B: Hardware GPS
                  Row(
                    children: [
                      Expanded(
                        child: OutlinedButton.icon(
                          icon: const Icon(Icons.my_location),
                          label: const Text('Use Device GPS'),
                          onPressed: _isResolvingLocation
                              ? null
                              : _fetchGPSLocation,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 10),

                  // Coordinates & Boundary Guard feedback
                  if (_isLocationResolved)
                    Container(
                      margin: const EdgeInsets.symmetric(vertical: 6),
                      padding: const EdgeInsets.all(10),
                      decoration: BoxDecoration(
                        color: Colors.green.shade50,
                        borderRadius: BorderRadius.circular(8),
                        border: Border.all(color: Colors.green.shade400),
                      ),
                      child: Row(
                        children: [
                          Icon(Icons.check_circle,
                              color: Colors.green.shade700, size: 20),
                          const SizedBox(width: 8),
                          Expanded(
                            child: Text(
                              'Resolved Coordinates:\nLat: ${_latitude.toStringAsFixed(4)}, Lng: ${_longitude.toStringAsFixed(4)}',
                              style: TextStyle(
                                color: Colors.green.shade900,
                                fontWeight: FontWeight.bold,
                                fontSize: 13,
                              ),
                            ),
                          ),
                        ],
                      ),
                    )
                  else
                    Container(
                      margin: const EdgeInsets.symmetric(vertical: 6),
                      padding: const EdgeInsets.all(10),
                      decoration: BoxDecoration(
                        color: Colors.amber.shade50,
                        borderRadius: BorderRadius.circular(8),
                        border: Border.all(color: Colors.amber.shade400),
                      ),
                      child: Row(
                        children: [
                          Icon(Icons.info_outline,
                              color: Colors.amber.shade800, size: 20),
                          const SizedBox(width: 8),
                          const Expanded(
                            child: Text(
                              'Location unresolved. Please search your address or use Device GPS to set coordinates.',
                              style: TextStyle(
                                fontSize: 12,
                                color: Colors.black87,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),

                  const Divider(height: 28),

                  // Operating Radius Slider (1 to 50 km)
                  Text(
                    'Operating Radius: ${_radiusKm.toStringAsFixed(0)} km',
                    style: const TextStyle(fontWeight: FontWeight.bold),
                  ),
                  Slider(
                    value: _radiusKm,
                    min: 1.0,
                    max: 50.0,
                    divisions: 49,
                    label: '${_radiusKm.toStringAsFixed(0)} km',
                    onChanged: (val) => setState(() => _radiusKm = val),
                  ),
                ],
              ),
            ),
          ),

          // -------------------------------------------------------------
          // STEP 3: Skills & PDF Certificate Upload
          // -------------------------------------------------------------
          Step(
            title: const Text('Skills & Verification'),
            subtitle: const Text('Category & certificate credentials'),
            isActive: _currentStep >= 2,
            state: _currentStep == 2 ? StepState.editing : StepState.indexed,
            content: Form(
              key: _step3Key,
              autovalidateMode: AutovalidateMode.onUserInteraction,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  // Whitelisted Category Dropdown
                  DropdownButtonFormField<String>(
                    initialValue: _selectedCategory,
                    items: _categories
                        .map((c) => DropdownMenuItem(value: c, child: Text(c)))
                        .toList(),
                    onChanged: (val) => setState(
                        () => _selectedCategory = val ?? 'Plumbing'),
                    decoration: const InputDecoration(
                      labelText: 'Official Service Category',
                      prefixIcon: Icon(Icons.category_outlined),
                    ),
                  ),
                  const SizedBox(height: 14),

                  // Specific Skill Name (e.g., Leak Repair)
                  TextFormField(
                    decoration: const InputDecoration(
                      labelText: 'Skill Competency',
                      hintText: 'e.g., Pipe & Leak Repair / Circuit Installation',
                      prefixIcon: Icon(Icons.handyman_outlined),
                    ),
                    validator: (val) {
                      if (val == null || val.trim().isEmpty) {
                        return 'Skill Name is required';
                      }
                      if (val.trim().length < 3) {
                        return 'Skill Name must be at least 3 characters';
                      }
                      return null;
                    },
                    onSaved: (val) => _skillName = val?.trim() ?? '',
                  ),
                  const SizedBox(height: 18),

                  // PDF Certificate Upload & Cap
                  const Text(
                    'Professional Certification / License (PDF)',
                    style: TextStyle(fontWeight: FontWeight.bold, fontSize: 13),
                  ),
                  const SizedBox(height: 4),
                  const Text(
                    'Upload verification document (strictly PDF format, max 5 MB).',
                    style: TextStyle(fontSize: 12, color: Colors.black54),
                  ),
                  const SizedBox(height: 10),

                  ElevatedButton.icon(
                    icon: Icon(
                      hasFile ? Icons.check_circle : Icons.upload_file,
                    ),
                    label: Text(
                      _pdfFileName == null
                          ? 'Select PDF Certificate'
                          : 'Attached: $_pdfFileName',
                    ),
                    style: ElevatedButton.styleFrom(
                      backgroundColor:
                          hasFile ? Colors.green.shade700 : null,
                      foregroundColor: hasFile ? Colors.white : null,
                    ),
                    onPressed: _pickFile,
                  ),

                  if (_isUploading) ...[
                    const SizedBox(height: 16),
                    const Row(
                      children: [
                        SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        ),
                        SizedBox(width: 12),
                        Text(
                          'Uploading certificate & submitting application...',
                          style: TextStyle(fontSize: 13),
                        ),
                      ],
                    ),
                  ],
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}
