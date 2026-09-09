# AssistLK Mobile Application (`mobile/`)

Flutter cross-platform mobile application for AssistLK customers and service providers.

---

## Quick Start

### 1. Prerequisites
- Flutter SDK (3.19+ or compatible)
- Android Studio / Xcode / Chrome for testing

### 2. Install Dependencies
```bash
cd mobile
flutter pub get
```

### 3. Run Development App
- **For Chrome (Web):**
  ```bash
  flutter run -d chrome
  ```
- **For Android Emulator (uses http://10.0.2.2:5012/api automatically):**
  ```bash
  flutter run
  ```
- **For Physical Device (Specify backend IP):**
  ```bash
  flutter run --dart-define=API_BASE_URL=http://<YOUR_LOCAL_IP>:5012/api
  ```

### 4. Run Automated Tests
```bash
flutter test
```

---

## Directory Architecture

```text
mobile/lib/
├── app/                  # App initialization and routing
├── core/                 # ApiClient, TokenStorage, AppConfig
├── shared/               # Design system (theme tokens & shared widgets)
└── features/             # Vertical business features (1 per component)
    ├── auth/             # Login & Registration
    ├── service_requests/ # Component 1
    ├── providers/        # Component 2
    ├── quotations/       # Component 3
    └── tracking/         # Component 4
```

For complete mobile team standards, see:
👉 **[Flutter Development Guide](../docs/development/flutter-development-guide.md)**
