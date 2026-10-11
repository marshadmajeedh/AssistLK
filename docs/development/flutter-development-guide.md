# AssistLK Flutter Development Guide

**Status:** Authoritative Mobile Standard  
**Applies To:** All mobile developers working in `mobile/` (Flutter)  
**Related Documents:** [Design System](../DESIGN_SYSTEM.md), [Component Boundaries](../architecture/component-boundaries.md), [Testing Guide](testing-guide.md)

---

## 1. Directory Structure & Architecture

AssistLK mobile uses a feature-first architectural pattern with centralized core infrastructure:

```text
mobile/lib/
├── main.dart             # Application bootstrap & Provider setup
├── app/                  # Top-level MaterialApp and routing
│   └── app.dart
├── core/                 # Cross-cutting foundational infrastructure
│   ├── api/              # ApiClient (Dio with auth interceptors)
│   ├── auth/             # Secure TokenStorage (flutter_secure_storage)
│   └── config/           # AppConfig (base URL resolution & env vars)
├── shared/               # Shared Design System tokens and reusable widgets
│   ├── theme/            # AppColors, AppTextStyles, AppSpacing, AppRadius, AppTheme
│   └── widgets/          # AppButton, AppCard, AppTextField
└── features/             # Vertical business slices (1 per component)
    ├── auth/             # Login, Registration, and Auth Gate
    ├── service_requests/ # Component 1 (Service Request & Problem Understanding)
    ├── providers/        # Component 2 (Provider Profiles & Matching)
    ├── quotations/       # Component 3 (Quotations & Bookings)
    └── tracking/         # Component 4 (Live Tracking & Feedback)
```

---

## 2. Standard Feature Directory Convention

Inside each feature directory (`mobile/lib/features/<feature_name>/`), maintain this pragmatic, consistent structure:

```text
features/<feature_name>/
├── models/               # Data classes with factory fromJson and toJson
├── services/             # HTTP service calling ApiClient
├── providers/            # State management (ChangeNotifier / Provider)
├── screens/              # Scaffolded page screens for routes
└── widgets/              # Feature-specific reusable UI sub-widgets
```

---

## 3. Core Developer Rules for Mobile

### 1. Theme & Design System Reuse
- **Do not hardcode colors, padding values, or text sizes.** Always use the tokens defined in `shared/theme/`:
  - `AppColors.primaryBlue`, `AppColors.surfaceBackground`, `AppColors.errorRed`, etc.
  - `AppSpacing.sm`, `AppSpacing.md`, `AppSpacing.lg`
  - `AppRadius.md`, `AppRadius.lg`
  - `AppTextStyles.heading1`, `AppTextStyles.bodyMedium`
- Use the shared widgets in `shared/widgets/`:
  - `AppButton(label: 'Submit', onPressed: () {}, isLoading: false)`
  - `AppTextField(label: 'Problem Description', controller: _controller)`
  - `AppCard(child: ...)`

### 2. Centralized `ApiClient` & Token Storage
- Use `ApiClient` located in `core/api/api_client.dart`.
- The `ApiClient` is powered by `Dio` and automatically attaches the JWT Bearer token from `TokenStorage` via request interceptors.
- Handle API exceptions gracefully using user-friendly error dialogs or snackbars.

### 3. Environment & Backend URL Configuration
- `AppConfig` (`core/config/app_config.dart`) automatically handles platform differences:
  - Web: `http://localhost:5012/api`
  - Android Emulator: `http://10.0.2.2:5012/api`
  - Physical Devices / Custom Hosts: Pass `--dart-define=API_BASE_URL=http://<YOUR_IP>:5012/api` at runtime:
    ```bash
    flutter run --dart-define=API_BASE_URL=http://192.168.1.100:5012/api
    ```

### 4. Auth & Role-Based Navigation
- `AuthGate` (`features/auth/screens/auth_gate.dart`) inspects the user's role from the stored JWT:
  - `Customer` → Routed to `CustomerHomeScreen` (`features/service_requests/`)
  - `Provider` → Routed to `ProviderHomeScreen` (`features/providers/`)
  - Unauthenticated → Routed to `LoginScreen`

### 5. Testing Conventions
- Store unit tests in `mobile/test/` matching the source structure:
  - Model serialization: `mobile/test/models/..._test.dart`
  - Services (mocking Dio): `mobile/test/services/..._test.dart`
  - Widget tests: `mobile/test/widgets/..._test.dart`
- Run tests before creating a pull request:
  ```bash
  flutter test
  ```
