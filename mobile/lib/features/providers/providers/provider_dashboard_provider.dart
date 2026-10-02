import 'dart:convert';
import 'dart:async';
import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:geolocator/geolocator.dart';
import 'package:flutter_tts/flutter_tts.dart';
import 'package:latlong2/latlong.dart' as latlong;
import 'package:dio/dio.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../services/provider_service.dart';
import 'web_notifications.dart';
import '../../../../core/services/notification_service.dart';

class ProviderDashboardProvider extends ChangeNotifier {
  static const String _prefIsOnlineKey = 'provider_is_online';
  static const String _prefOperatingRadiusKey = 'provider_operating_radius_km';
  static const String _prefVoiceAlertKey = 'provider_voice_alert_enabled';

  final ProviderService providerService;
  final FlutterTts flutterTts = FlutterTts();
  final Dio _dio = Dio();

  ProviderDashboardProvider({required this.providerService});

  Map<String, dynamic>? profile;
  bool _isProfileLoading = true;
  bool _isOnline = false;
  double _operatingRadiusKm = 10.0;
  double _latitude = 6.9271; // Default fallback to Colombo, Sri Lanka
  double _longitude = 79.8612;
  bool _isLoading = false;
  String? _error;
  bool _isVoiceAlertEnabled = true;
  double? _liveDistanceKm;

  // Stores the real job dispatched from your Python/C# backend
  Map<String, dynamic>? activeJobMatch;
  String? _lastAnnouncedJobId;
  List<latlong.LatLng> routePoints = [];

  List<Map<String, dynamic>> _inAppNotifications = [];
  int _unreadNotificationCount = 0;

  List<Map<String, dynamic>> get inAppNotifications => List.unmodifiable(_inAppNotifications);
  int get unreadNotificationCount => _unreadNotificationCount;

  int? _remainingAcceptSeconds;
  int _totalTimeoutSeconds = 60;
  Timer? _countdownTimer;
  String? _countdownJobId;

  int? get remainingAcceptSeconds => _remainingAcceptSeconds;
  int get totalTimeoutSeconds => _totalTimeoutSeconds;

  bool get isProfileLoading => _isProfileLoading;
  bool get isOnline => _isOnline;
  double get operatingRadiusKm => _operatingRadiusKm;
  double get latitude => _latitude;
  double get longitude => _longitude;
  bool get isLoading => _isLoading;
  String? get error => _error;
  bool get isVoiceAlertEnabled => _isVoiceAlertEnabled;
  double? get liveDistanceKm => _liveDistanceKm;

  String get verificationStatus =>
      profile?['verificationStatus']?.toString() ?? 'Pending';
  bool get isVerified => verificationStatus.toLowerCase() == 'verified';
  bool get isPending => verificationStatus.toLowerCase() == 'pending';
  bool get isRejected => verificationStatus.toLowerCase() == 'rejected';

  String get fullName => profile?['fullName']?.toString() ?? '';
  String get businessName => profile?['businessName']?.toString() ?? '';
  String get category => profile?['category']?.toString() ?? 'Plumbing';
  List<dynamic> get skills => (profile?['skills'] as List<dynamic>?) ?? [];
  List<String> get categories {
    final list = skills
        .map((s) => (s is Map ? s['category']?.toString() : null) ?? '')
        .where((s) => s.isNotEmpty)
        .toSet()
        .toList();
    if (list.isEmpty && category.isNotEmpty) {
      return [category];
    }
    return list;
  }
  double get rating => (profile?['rating'] as num?)?.toDouble() ?? 0.0;
  int get totalCompletedJobs => (profile?['totalCompletedJobs'] as num?)?.toInt() ?? 0;

  String? _profileImagePath;
  String? get profileImagePath => _profileImagePath;

  String _getProfileImageKey() {
    final id = profile?['providerId']?.toString() ?? profile?['id']?.toString() ?? profile?['userId']?.toString();
    if (id != null && id.isNotEmpty) {
      return 'provider_profile_image_$id';
    }
    return 'provider_profile_image_default';
  }

  String get defaultAvatarUrl {
    final name = Uri.encodeComponent(fullName.isNotEmpty ? fullName : 'Provider');
    return 'https://ui-avatars.com/api/?name=$name&background=1F4E78&color=ffffff&size=256&bold=true';
  }

  Future<void> updateProfileImage(String path) async {
    _profileImagePath = path;
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.setString(_getProfileImageKey(), path);
    } catch (e) {
      debugPrint('Failed to save profile image: $e');
    }
    notifyListeners();
  }

  Future<void> removeProfileImage() async {
    _profileImagePath = null;
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.remove(_getProfileImageKey());
    } catch (e) {
      debugPrint('Failed to remove profile image: $e');
    }
    notifyListeners();
  }

  String _getNotificationKey() {
    final id = profile?['providerId']?.toString() ?? profile?['id']?.toString() ?? profile?['userId']?.toString();
    if (id != null && id.isNotEmpty) {
      return 'provider_in_app_notifications_$id';
    }
    return 'provider_in_app_notifications_default';
  }

  Future<void> _saveNotifications() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      if (_inAppNotifications.length > 50) {
        _inAppNotifications = _inAppNotifications.sublist(0, 50);
      }
      await prefs.setString(_getNotificationKey(), jsonEncode(_inAppNotifications));
    } catch (e) {
      debugPrint('Failed to save in-app notifications: $e');
    }
  }

  void addInAppNotification({
    required String title,
    required String message,
    required String type,
    String? jobId,
  }) {
    final newNotif = {
      'id': DateTime.now().millisecondsSinceEpoch.toString(),
      'title': title,
      'message': message,
      'type': type,
      'jobId': jobId,
      'timestamp': DateTime.now().toIso8601String(),
      'isRead': false,
    };
    _inAppNotifications.insert(0, newNotif);
    _unreadNotificationCount = _inAppNotifications.where((n) => n['isRead'] == false).length;
    _saveNotifications();
    notifyListeners();
  }

  void markAllNotificationsAsRead() {
    bool hasUnread = false;
    for (var n in _inAppNotifications) {
      if (n['isRead'] == false) {
        n['isRead'] = true;
        hasUnread = true;
      }
    }
    if (hasUnread) {
      _unreadNotificationCount = 0;
      _saveNotifications();
      notifyListeners();
    }
  }

  void deleteInAppNotification(String id) {
    _inAppNotifications.removeWhere((n) => n['id']?.toString() == id);
    _unreadNotificationCount = _inAppNotifications.where((n) => n['isRead'] == false).length;
    _saveNotifications();
    notifyListeners();
  }

  void clearAllInAppNotifications() {
    _inAppNotifications.clear();
    _unreadNotificationCount = 0;
    _saveNotifications();
    notifyListeners();
  }

  StreamSubscription<Position>? _positionStreamSubscription;
  Timer? _pollingTimer;

  Future<void> _initTts() async {
    try {
      await flutterTts.setLanguage('en-US');
      await flutterTts.setSpeechRate(0.5);
      await flutterTts.setVolume(1.0);
      await flutterTts.setPitch(1.0);
      await flutterTts.awaitSynthCompletion(true);
    } catch (e) {
      debugPrint('TTS initialization warning: $e');
    }
  }

  Future<void> init() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      _isOnline = prefs.getBool(_prefIsOnlineKey) ?? false;
      _operatingRadiusKm = prefs.getDouble(_prefOperatingRadiusKey) ?? 10.0;
      _isVoiceAlertEnabled = prefs.getBool(_prefVoiceAlertKey) ?? true;
      notifyListeners();
    } catch (e) {
      debugPrint('Failed to load persisted provider preferences: $e');
    }

    unawaited(_initTts());
    unawaited(NotificationService().init());

    await fetchProfile();
  }

  Future<void> fetchProfile() async {
    _isProfileLoading = true;
    _error = null;
    notifyListeners();

    try {
      final data = await providerService.getProfile();
      profile = data;

      try {
        final prefs = await SharedPreferences.getInstance();
        final savedImage = prefs.getString(_getProfileImageKey());
        if (savedImage != null && savedImage.isNotEmpty && File(savedImage).existsSync()) {
          _profileImagePath = savedImage;
        } else {
          _profileImagePath = null;
        }
      } catch (e) {
        debugPrint('Failed to load profile image for provider: $e');
      }

      try {
        final prefs = await SharedPreferences.getInstance();
        final rawNotifs = prefs.getString(_getNotificationKey());
        if (rawNotifs != null && rawNotifs.isNotEmpty) {
          final decoded = jsonDecode(rawNotifs) as List<dynamic>;
          _inAppNotifications = decoded.map((e) => Map<String, dynamic>.from(e as Map)).toList();
          _unreadNotificationCount = _inAppNotifications.where((n) => n['isRead'] == false).length;
        } else {
          _inAppNotifications = [];
          _unreadNotificationCount = 0;
        }
      } catch (e) {
        debugPrint('Failed to load notifications for provider: $e');
        _inAppNotifications = [];
        _unreadNotificationCount = 0;
      }

      final num? rad = data['operatingRadiusKm'] as num?;
      if (rad != null && rad > 0) {
        _operatingRadiusKm = rad.toDouble();
      }
      final num? lat = data['latitude'] as num?;
      final num? lng = data['longitude'] as num?;
      if (lat != null && lng != null && lat != 0 && lng != 0) {
        _latitude = lat.toDouble();
        _longitude = lng.toDouble();
      }
      if (data['isOnline'] is bool) {
        _isOnline = data['isOnline'] as bool;
      }

      _isProfileLoading = false;
      notifyListeners();

      if (isVerified) {
        _isOnline = true;
        _saveIsOnline(true);
        unawaited(_checkPermissionsAndFetchLocation());
        fetchLatestDispatchedJob();
        _startPolling();
      } else {
        _stopPolling();
        _clearJobState();
      }
    } catch (e) {
      debugPrint('Failed to load provider profile: $e');
      _error = 'Failed to load profile: $e';
      _isProfileLoading = false;
      notifyListeners();
    }
  }

  Future<void> updateProfile({
    required String businessName,
    required double operatingRadiusKm,
    double? latitude,
    double? longitude,
  }) async {
    _isLoading = true;
    notifyListeners();
    try {
      final updated = await providerService.updateProfile(
        businessName: businessName,
        operatingRadiusKm: operatingRadiusKm,
        latitude: latitude,
        longitude: longitude,
      );
      profile = {...?profile, ...updated};
      _operatingRadiusKm = operatingRadiusKm;
      if (latitude != null) _latitude = latitude;
      if (longitude != null) _longitude = longitude;
      _isLoading = false;
      notifyListeners();
      await _saveOperatingRadius(operatingRadiusKm);
    } catch (e) {
      _isLoading = false;
      _error = 'Failed to update profile: $e';
      notifyListeners();
      rethrow;
    }
  }

  Future<void> _checkPermissionsAndFetchLocation() async {
    _error = null;
    try {
      bool serviceEnabled = await Geolocator.isLocationServiceEnabled();
      if (!serviceEnabled) {
        _error = 'Location services are disabled.';
        notifyListeners();
        return;
      }

      LocationPermission permission = await Geolocator.checkPermission();
      if (permission == LocationPermission.denied) {
        permission = await Geolocator.requestPermission();
        if (permission == LocationPermission.denied) {
          _error = 'Location permissions are denied.';
          notifyListeners();
          return;
        }
      }

      if (permission == LocationPermission.deniedForever) {
        _error = 'Location permissions are permanently denied.';
        notifyListeners();
        return;
      }

      // Safe location acquisition
      Position position;
      if (kIsWeb) {
        position = await Geolocator.getCurrentPosition(
          locationSettings: const LocationSettings(
            accuracy: LocationAccuracy.high,
          ),
        );
      } else {
        Position? lastKnown = await Geolocator.getLastKnownPosition();
        position =
            lastKnown ??
            await Geolocator.getCurrentPosition(
              locationSettings: const LocationSettings(
                accuracy: LocationAccuracy.high,
              ),
            );
      }

      _latitude = position.latitude;
      _longitude = position.longitude;

      // Listen for dynamic positional movement
      _positionStreamSubscription?.cancel();
      _positionStreamSubscription =
          Geolocator.getPositionStream(
            locationSettings: const LocationSettings(
              accuracy: LocationAccuracy.high,
              distanceFilter: 50,
            ),
          ).listen((Position pos) {
            _latitude = pos.latitude;
            _longitude = pos.longitude;
            _updateLiveDistance();
            notifyListeners();
            if (_isOnline && isVerified) {
              _syncAvailability();
            }
          });

      _error = null;
      _updateLiveDistance();
      notifyListeners();

      if (isVerified) {
        _syncAvailability();
      }
    } catch (e) {
      _error = 'Error fetching location: $e';
      notifyListeners();
    }
  }

  void _updateLiveDistance() {
    if (activeJobMatch != null) {
      final num? cLat = activeJobMatch!['customerLatitude'] as num?;
      final num? cLng = activeJobMatch!['customerLongitude'] as num?;
      if (cLat != null && cLng != null) {
        final distanceInMeters = Geolocator.distanceBetween(
          _latitude,
          _longitude,
          cLat.toDouble(),
          cLng.toDouble(),
        );
        _liveDistanceKm = distanceInMeters / 1000.0;
      } else {
        final num? staticDist = activeJobMatch!['distanceKm'] as num?;
        _liveDistanceKm = staticDist?.toDouble();
      }
    } else {
      _liveDistanceKm = null;
    }
  }

  void toggleVoiceAlert(bool value) async {
    _isVoiceAlertEnabled = value;
    notifyListeners();
    if (value && kIsWeb) {
      requestNotificationPermissions();
    }
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.setBool(_prefVoiceAlertKey, value);
    } catch (e) {
      debugPrint('Failed to save voice alert preference: $e');
    }
  }

  void toggleOnlineStatus(bool value) async {
    if (!isVerified) return;

    if (value) {
      if (kIsWeb) {
        requestNotificationPermissions();
      }
      // Force an immediate GPS sync before going online
      await _checkPermissionsAndFetchLocation();
    }

    _isOnline = value;
    notifyListeners();
    _saveIsOnline(value);
    _syncAvailability();

    if (_isOnline) {
      fetchLatestDispatchedJob();
      _startPolling();
    } else {
      _stopPolling();
      _clearJobState();
    }
  }

  Future<void> _saveIsOnline(bool value) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.setBool(_prefIsOnlineKey, value);
    } catch (e) {
      debugPrint('Failed to save isOnline preference: $e');
    }
  }

  void _startPolling() {
    if (!isVerified) return;
    _pollingTimer?.cancel();
    _pollingTimer = Timer.periodic(const Duration(seconds: 5), (_) {
      if (_isOnline && isVerified) {
        fetchLatestDispatchedJob();
      }
    });
  }

  void _stopPolling() {
    _pollingTimer?.cancel();
    _pollingTimer = null;
  }

  void setOperatingRadius(double value) {
    _operatingRadiusKm = value;
    notifyListeners();
    _saveOperatingRadius(value);
  }

  void onRadiusChangeEnd(double value) {
    _saveOperatingRadius(value);
    if (isVerified) {
      _syncAvailability();
    }
  }

  Future<void> _saveOperatingRadius(double value) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.setDouble(_prefOperatingRadiusKey, value);
    } catch (e) {
      debugPrint('Failed to save operatingRadius preference: $e');
    }
  }

  Future<void> _syncAvailability() async {
    if (!isVerified) return;
    try {
      await providerService.updateAvailability(
        isOnline: _isOnline,
        latitude: _latitude,
        longitude: _longitude,
        operatingRadiusKm: _operatingRadiusKm,
      );
    } catch (e) {
      debugPrint('Failed to sync availability: $e');
    }
  }

  // Fetches approved matches from PostgreSQL via ASP.NET Core
  Future<void> fetchLatestDispatchedJob() async {
    if (!isVerified) return;
    try {
      final response = await providerService.apiClient.client.get(
        '/providers/active-dispatch',
      );
      if (response.statusCode == 200 && response.data != null) {
        final data = Map<String, dynamic>.from(response.data);
        activeJobMatch = data;
        _updateLiveDistance();

        final jobId = data['jobId']?.toString();
        final status = data['status']?.toString();
        if (jobId != null && jobId != _lastAnnouncedJobId && status != 'Accepted') {
          _lastAnnouncedJobId = jobId;
          unawaited(_announceJob(data));

          final shortId = jobId.length > 8 ? jobId.substring(0, 8) : jobId;
          final category = data['category']?.toString() ?? 'Service';
          final dist = _liveDistanceKm?.toStringAsFixed(1) ?? data['distanceKm']?.toString() ?? 'nearby';
          addInAppNotification(
            title: 'New Service Dispatch',
            message: 'Incoming $category request #$shortId ($dist km away). Review details and accept or decline.',
            type: 'alert',
            jobId: jobId,
          );
        }

        // Manage acceptance countdown timer for pending recommended dispatch
        if (status != 'Accepted') {
          final int timeoutSec = (data['timeoutSeconds'] as num?)?.toInt() ?? 60;
          _totalTimeoutSeconds = timeoutSec;
          final int remaining = (data['remainingSeconds'] as num?)?.toInt() ?? timeoutSec;
          _startAcceptCountdown(remaining, jobId);
        } else {
          _stopAcceptCountdown();
        }

        final dynamic rawLat = data['customerLatitude'];
        final dynamic rawLng = data['customerLongitude'];
        if (rawLat != null && rawLng != null && routePoints.isEmpty) {
          final double custLat = (rawLat as num).toDouble();
          final double custLng = (rawLng as num).toDouble();
          unawaited(_fetchRoute(custLat, custLng));
        }

        notifyListeners();
        return;
      } else if (response.statusCode == 204) {
        // If a recommended match was pending and server reports 204, it timed out on server!
        if (activeJobMatch != null && activeJobMatch!['status'] != 'Accepted') {
          await _onDispatchTimedOut();
          return;
        }
        _clearJobState();
        return;
      }
    } catch (e) {
      debugPrint('Network fetch for active dispatch notice: $e');
      // Do not clear the active job on transient network glitches
    }
  }

  Future<void> _fetchRoute(double custLat, double custLng) async {
    try {
      final url =
          'https://router.project-osrm.org/route/v1/driving/$_longitude,$_latitude;$custLng,$custLat?geometries=geojson';
      final response = await _dio.get(url);
      if (response.statusCode == 200 && response.data != null) {
        final routes = response.data['routes'] as List;
        if (routes.isNotEmpty) {
          final geometry = routes[0]['geometry'];
          final coordinates = geometry['coordinates'] as List;
          routePoints = coordinates.map((coord) {
            return latlong.LatLng(
              (coord[1] as num).toDouble(),
              (coord[0] as num).toDouble(),
            );
          }).toList();
          notifyListeners();
        }
      }
    } catch (e) {
      debugPrint('OSRM route fetch skipped or unavailable: $e');
    }
  }

  Future<void> _announceJob(Map<String, dynamic> data) async {
    try {
      final category = data['category']?.toString() ?? 'Service Request';
      final distance = _liveDistanceKm?.toStringAsFixed(1) ??
          data['distanceKm']?.toString() ??
          'unknown';
      final urgency = data['urgency']?.toString() ?? 'Standard';
      final problemDesc = (data['detectedProblem'] ??
              data['description'] ??
              'New service dispatch received.')
          .toString();

      if (kIsWeb) {
        showWebNotification(
          'New $category Job ($distance km)',
          problemDesc,
        );
      } else {
        unawaited(NotificationService().showJobAlertNotification(
          id: (data['jobId']?.hashCode ?? DateTime.now().millisecondsSinceEpoch) & 0x7FFFFFFF,
          title: '🚨 New $category Job ($distance km)',
          body: '$problemDesc (Urgency: $urgency)',
          payload: data['jobId']?.toString(),
        ));
      }

      if (_isVoiceAlertEnabled) {
        // Basic voice message only: never read the long AI review text
        await flutterTts.speak(
          'New $category job alert, $distance kilometers away. $urgency urgency.',
        );
      }
    } catch (e) {
      debugPrint('Job announcement warning: $e');
    }
  }

  void _startAcceptCountdown(int initialSeconds, String? jobId) {
    if (_countdownTimer != null && _countdownJobId == jobId) {
      // Timer already active for this exact job; don't reset countdown
      return;
    }

    _stopAcceptCountdown();
    _countdownJobId = jobId;
    _remainingAcceptSeconds = initialSeconds;

    _countdownTimer = Timer.periodic(const Duration(seconds: 1), (timer) {
      if (_remainingAcceptSeconds != null && _remainingAcceptSeconds! > 0) {
        _remainingAcceptSeconds = _remainingAcceptSeconds! - 1;
        notifyListeners();
      } else {
        _stopAcceptCountdown();
        _onDispatchTimedOut();
      }
    });
  }

  void _stopAcceptCountdown() {
    _countdownTimer?.cancel();
    _countdownTimer = null;
    _countdownJobId = null;
    _remainingAcceptSeconds = null;
  }

  Future<void> _onDispatchTimedOut() async {
    try {
      debugPrint('Job acceptance timer expired. Auto-declining and releasing to next provider.');
      final currentJobId = activeJobMatch?['jobId']?.toString();
      final shortId = currentJobId != null && currentJobId.length > 8 ? currentJobId.substring(0, 8) : (currentJobId ?? '');

      _stopAcceptCountdown();
      activeJobMatch = null;
      routePoints = [];
      _lastAnnouncedJobId = null;
      notifyListeners();

      addInAppNotification(
        title: 'Dispatch Expired',
        message: 'You missed service request #$shortId. Acceptance timer expired.',
        type: 'timeout',
        jobId: currentJobId,
      );

      if (_isVoiceAlertEnabled) {
        try {
          await flutterTts.speak('Dispatch request expired. Re-dispatching to next available provider.');
        } catch (e) {
          debugPrint('TTS speak timeout alert error: $e');
        }
      }

      try {
        await providerService.declineActiveDispatch();
      } catch (_) {}
    } catch (e) {
      debugPrint('Error triggering timeout decline: $e');
      _clearJobState();
    }
  }

  void _clearJobState() {
    _stopAcceptCountdown();
    activeJobMatch = null;
    routePoints = [];
    notifyListeners();
  }

  void clearActiveJobMatch() {
    _clearJobState();
  }

  Future<void> acceptJob() async {
    _stopAcceptCountdown();
    final currentJobId = activeJobMatch?['jobId']?.toString();
    final shortId = currentJobId != null && currentJobId.length > 8 ? currentJobId.substring(0, 8) : (currentJobId ?? '');
    try {
      await providerService.acceptActiveDispatch('/api/providers/active-dispatch/accept');
      if (activeJobMatch != null) {
        activeJobMatch!['status'] = 'Accepted';
      }
      addInAppNotification(
        title: 'Job Accepted',
        message: 'You accepted request #$shortId. Customer location and driving route are locked.',
        type: 'accepted',
        jobId: currentJobId,
      );
      notifyListeners();
    } catch (e) {
      debugPrint('Failed to accept job: $e');
      rethrow;
    }
  }

  Future<void> completeJob() async {
    _stopAcceptCountdown();
    final currentJobId = activeJobMatch?['jobId']?.toString();
    final shortId = currentJobId != null && currentJobId.length > 8 ? currentJobId.substring(0, 8) : (currentJobId ?? '');
    try {
      await providerService.apiClient.client.post('/providers/active-dispatch/complete');
    } catch (e) {
      debugPrint('Error marking dispatch completed on backend: $e');
    }
    addInAppNotification(
      title: 'Job Completed',
      message: 'You completed service request #$shortId. Great job!',
      type: 'completed',
      jobId: currentJobId,
    );
    _clearJobState();
    _lastAnnouncedJobId = null;
  }

  Future<void> declineJob({bool logNotification = true}) async {
    _stopAcceptCountdown();
    final currentJobId = activeJobMatch?['jobId']?.toString();
    final shortId = currentJobId != null && currentJobId.length > 8 ? currentJobId.substring(0, 8) : (currentJobId ?? '');
    try {
      await providerService.declineActiveDispatch();
    } catch (e) {
      debugPrint('Failed to decline match: $e');
    }
    if (logNotification) {
      addInAppNotification(
        title: 'Dispatch Declined',
        message: 'You declined service request #$shortId. Request returned to dispatch.',
        type: 'declined',
        jobId: currentJobId,
      );
    }
    _clearJobState();
    _lastAnnouncedJobId = null;
  }

  Future<void> onLogout() async {
    _stopAcceptCountdown();
    _stopPolling();
    _positionStreamSubscription?.cancel();
    _positionStreamSubscription = null;
    _isOnline = false;
    await _saveIsOnline(false);
    try {
      await providerService.updateAvailability(
        isOnline: false,
        latitude: _latitude,
        longitude: _longitude,
        operatingRadiusKm: _operatingRadiusKm,
      );
    } catch (e) {
      debugPrint('Failed to sync offline status on logout: $e');
    }
    _clearJobState();
  }

  @override
  void dispose() {
    _stopAcceptCountdown();
    _stopPolling();
    _positionStreamSubscription?.cancel();
    super.dispose();
  }
}
