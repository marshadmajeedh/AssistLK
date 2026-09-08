# Component 1 Frontend Foundation Inspection

## 1. Executive Summary

This document provides the definitive architectural and design system inspection for the AssistLK Frontend prior to the implementation of the Component 1 Customer Frontend (**Smart Service Request & Problem Understanding Agent**). 

The inspection was conducted on branch `feature/component-1-customer-frontend` following the completion and merge of Component 1 Backend (Phases 7A–7G). In accordance with Phase F1 requirements, this phase involves **zero modifications to source code, dependencies, themes, or backend services**. 

### Key Findings
1. **Frontend Location & Framework**: The web application resides exclusively in `web/`. It is a modern Single Page Application (SPA) built with **React 19.2.8** and **Vite 8.2.0** utilizing standard JavaScript (ES Modules, JSX).
2. **Unified Styling & Design System**: The repository uses a token-driven JavaScript styling architecture located in `web/src/shared/theme/` rather than external CSS utility frameworks (no Tailwind, MUI, or Bootstrap). All colors, typography, spacing, and component styles align strictly with `docs/DESIGN_SYSTEM.md`.
3. **Existing Shared Components**: Reusable components currently implemented include `AppButton`, `AppCard`, `AppInput`, `ErrorMessage`, `LoadingSpinner`, and `PlaceholderPage`.
4. **Layout & Authentication Current State**: The existing frontend is currently configured primarily around an administrator shell (`AdminLayout`, `LoginPage` checking `user.role === 'Admin'`). Customer sessions, JWT interceptors, and Zustand authentication stores exist in `web/src/shared/auth/authStore.js` and `web/src/shared/api/apiClient.js` and are fully ready to support Customer roles once a Customer Layout and customer login path are established.
5. **GPS & Location Readiness**: No external mapping dependencies (Leaflet, Mapbox, Google Maps) are present in `package.json`. However, the HTML5 Geolocation API (`navigator.geolocation`) is natively supported in all target browsers, perfectly fulfilling the requirement for "Use Current Location" (latitude and longitude capture) alongside manual text location entry.
6. **Component 1 Feature Directory**: A dedicated feature folder `web/src/features/serviceRequests/` already exists in `src/features/` with a `.gitkeep` placeholder, perfectly aligned with the repository's modular feature-folder architecture.

---

## 2. Frontend Technology Stack

| Technology Layer | Actual Implementation in Repository | Version / Spec |
| :--- | :--- | :--- |
| **Application Root** | `web/` (Relative to workspace root) | Single Vite Web Project |
| **Framework** | React | `^19.2.8` |
| **DOM Renderer** | React DOM | `^19.2.8` |
| **Bundler & Dev Server** | Vite with `@vitejs/plugin-react` | `^8.2.0` (plugin: `^6.0.4`) |
| **Language** | JavaScript (JSX, ES Modules) | ECMAScript 2022+ |
| **Client-side Routing** | React Router DOM | `^7.18.2` |
| **Global State Management**| Zustand | `^5.0.15` |
| **HTTP / API Client** | Axios | `^1.19.0` |
| **Styling Solution** | Vanilla JS Theme Tokens + Inline Style Objects | Custom Design System (`src/shared/theme/`) |
| **CSS Preprocessors** | None | Raw CSS files (`index.css`, `App.css`) unused by `main.jsx` |
| **Component Libraries** | None (No MUI, Chakra, Bootstrap, AntD) | Pure in-house shared components |
| **Icon Libraries** | None installed (No Lucide, FontAwesome, React Icons)| Vite template SVG sprite in `public/icons.svg` |
| **Form Libraries** | None installed (No Formik, React Hook Form) | Controlled React components (`useState`) |
| **Validation Libraries** | None installed (No Zod, Yup) | Pure JS / HTML5 validation |
| **Animation Libraries** | None installed (No Framer Motion) | Native CSS transitions |
| **Map / GPS Packages** | None installed (No Leaflet, Google Maps) | Native Browser `navigator.geolocation` API |
| **Testing Libraries** | None in `web/package.json` | Backend has extensive xUnit/PostgreSql suites |
| **Linter** | ESLint + React Hooks + React Refresh | `^10.8.0` |
| **Build Scripts** | `"dev": "vite"`, `"build": "vite build"`, `"preview": "vite preview"`, `"lint": "eslint ."` | Standard Vite npm scripts |

---

## 3. Actual Frontend Directory Structure

```text
web/
├── .env
├── .env.example
├── .gitignore
├── eslint.config.js
├── index.html
├── package.json
├── package-lock.json
├── README.md
├── vite.config.js
├── public/
│   ├── favicon.svg
│   └── icons.svg
└── src/
    ├── App.css                    (Vite template CSS - not imported in main)
    ├── App.jsx                    (Root component mounting AppRouter)
    ├── index.css                  (Vite template CSS - not imported in main)
    ├── main.jsx                   (Vite entry point rendering <App /> in StrictMode)
    ├── app/
    │   └── router/
    │       └── AppRouter.jsx      (React Router BrowserRouter, Route definitions)
    ├── assets/
    │   ├── hero.png
    │   ├── react.svg
    │   └── vite.svg
    ├── features/
    │   ├── aiWorkflows/
    │   │   └── .gitkeep
    │   ├── auth/
    │   │   └── pages/
    │   │       └── LoginPage.jsx  (Admin login form & authentication controller)
    │   ├── providers/
    │   │   └── .gitkeep
    │   ├── quotations/
    │   │   └── .gitkeep
    │   ├── serviceRequests/
    │   │   └── .gitkeep           (Pre-allocated home for Component 1)
    │   └── tracking/
    │       └── .gitkeep
    └── shared/
        ├── .gitkeep
        ├── api/
        │   └── apiClient.js       (Configured Axios instance with JWT interceptors)
        ├── auth/
        │   ├── authStore.js       (Zustand authentication store with sessionStorage)
        │   └── ProtectedRoute.jsx (Role-based route guard component)
        ├── components/
        │   ├── AppButton.jsx      (Shared button with primary, secondary, outline, danger)
        │   ├── AppCard.jsx        (Shared card container with borders and elevation)
        │   ├── AppInput.jsx       (Shared labelled text input with error display)
        │   ├── ErrorMessage.jsx   (Shared error alert banner component)
        │   ├── LoadingSpinner.jsx (Shared centered text/loading indicator)
        │   └── PlaceholderPage.jsx(Generic placeholder for unbuilt modules)
        ├── layouts/
        │   └── AdminLayout.jsx    (Two-column layout with sidebar for Admin role)
        └── theme/
            ├── colors.js          (Hex color definitions matching docs/DESIGN_SYSTEM.md)
            ├── components.js      (Predefined style objects for buttons, cards, inputs)
            ├── index.js           (Barrel export for theme tokens and style objects)
            ├── radius.js          (Standard border-radius tokens)
            ├── spacing.js         (Standard 4px-grid spacing tokens)
            ├── theme.js           (Consolidated theme object)
            └── typography.js      (Font stacks, weights, sizes, and line-heights)
```

---

## 4. Shared Theme / Design System

The repository strictly enforces a centralized design token system located in `web/src/shared/theme/` which codifies the platform guidelines documented in `docs/DESIGN_SYSTEM.md`.

### Colors (`web/src/shared/theme/colors.js`)
* **Brand**:
  * `primary`: `#1F4E78` (AssistLK Deep Navy / Slate Blue)
  * `primaryDark`: `#173B5E` (Admin sidebar and darker contrast elements)
  * `secondary`: `#0F6B66` (Teal Accent)
* **Status**:
  * `success`: `#2E7D32` (Forest Green - verified, analyzed, ready)
  * `warning`: `#C45A11` (Amber/Orange - awaiting information, pending)
  * `error`: `#B42318` (Crimson Red - failures, cancellation, errors)
* **Backgrounds**:
  * `background`: `#F6F8FA` (Soft Cool Grey/White page backdrop)
  * `surface`: `#FFFFFF` (Pure White cards, inputs, dropdowns)
* **Text**:
  * `textPrimary`: `#1F2937` (Charcoal / Slate 800)
  * `textSecondary`: `#6B7280` (Muted Grey / Slate 500)
* **UI**:
  * `border`: `#D1D5DB` (Subtle grey border)
  * `disabled`: `#9CA3AF` (Muted disabled element background)

### Typography (`web/src/shared/theme/typography.js`)
* **Font Family**: `"Inter", "Segoe UI", Roboto, Helvetica, Arial, sans-serif`
* **pageTitle**: `fontSize: 28px`, `fontWeight: 700`, `lineHeight: 1.2`
* **sectionHeading**: `fontSize: 20px`, `fontWeight: 600`, `lineHeight: 1.3`
* **cardHeading**: `fontSize: 16px`, `fontWeight: 600`, `lineHeight: 1.4`
* **body**: `fontSize: 14px`, `fontWeight: 400`, `lineHeight: 1.5`
* **small**: `fontSize: 12px`, `fontWeight: 400`, `lineHeight: 1.4`
* **button**: `fontSize: 14px`, `fontWeight: 600`

### Spacing (`web/src/shared/theme/spacing.js`)
Standard 4px/8px modular scale:
* `xs`: `4px`
* `sm`: `8px`
* `md`: `16px`
* `lg`: `24px`
* `xl`: `32px`

### Shape / Border Radius (`web/src/shared/theme/radius.js`)
* `small`: `6px` (tooltips, small badges)
* `medium`: `10px` (inputs, buttons, notification banners)
* `large`: `16px` (cards, modal surfaces, containers)
* `pill`: `999px` (status badges, chips, pills)

### Component Tokens (`web/src/shared/theme/components.js`)
* **`buttonStyles`**:
  * `base`: `minHeight: 48px`, `padding: "0 16px"`, `borderRadius: 10px`, `border: "none"`, `cursor: "pointer"`, `fontFamily: typography.fontFamily`, `fontSize: 14px`, `fontWeight: 600`
  * Variants: `primary` (Navy `#1F4E78`), `secondary` (Teal `#0F6B66`), `danger` (Red `#B42318`), `outline` (White bg, Navy border & text), `disabled` (`#9CA3AF`, `cursor: "not-allowed"`)
* **`inputStyles`**:
  * `width: "100%"`, `minHeight: 48px`, `padding: "0 16px"`, `backgroundColor: "#FFFFFF"`, `color: "#1F2937"`, `border: "1px solid #D1D5DB"`, `borderRadius: 10px`, `outline: "none"`, `boxSizing: "border-box"`
* **`cardStyles`**:
  * `backgroundColor: "#FFFFFF"`, `border: "1px solid #D1D5DB"`, `borderRadius: 16px`, `padding: 16px`, `color: "#1F2937"`, `boxShadow: "0 2px 8px rgba(0, 0, 0, 0.06)"`
* **`pageStyles`**:
  * `backgroundColor: "#F6F8FA"`, `color: "#1F2937"`, `minHeight: "100vh"`, `padding: 24px`, `fontFamily: typography.fontFamily`

---

## 5. Shared Component Inventory

| Component File | Name | Purpose | Main Props | Styling Approach | Component 1 Reuse | Limitations / Observations |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `shared/components/AppButton.jsx` | `AppButton` | Standard platform button | `children`, `variant`, `type`, `disabled`, `onClick`, `style` | Inlines `buttonStyles.base` + variant + disabled overrides | **YES (High)**: Submit, Analyze, Edit, Cancel, and Confirm buttons. | Fixed `minHeight: 48px`; does not accept an inline icon prop or loading indicator spinner within button. |
| `shared/components/AppCard.jsx` | `AppCard` | Standard content surface | `children`, `style` | Inlines `cardStyles` + custom `style` | **YES (High)**: Request forms, analysis result displays, list items. | Pure `div` container; no subcomponents (`AppCard.Header`, `AppCard.Body`). |
| `shared/components/AppInput.jsx` | `AppInput` | Labelled text input | `label`, `error`, `...inputProps` (`value`, `onChange`, etc.) | Label (`typography.body`) + native `<input>` (`inputStyles`) + error text | **YES (Medium)**: Manual location, short inputs. | Strictly single-line `<input>`. Cannot be used for multi-line request description (up to 4000 chars). |
| `shared/components/ErrorMessage.jsx` | `ErrorMessage` | In-page error alert banner | `message` | Container with `colors.surface`, red border & text (`colors.error`), radius `10px` | **YES (High)**: API errors, validation errors, analysis failures. | Returns `null` if empty; single severity only (error); no dismiss button. |
| `shared/components/LoadingSpinner.jsx`| `LoadingSpinner`| Loading state placeholder | `message` (default: "Loading...") | Centered `div` with `colors.textSecondary` and `typography.body` | **YES (Medium)**: Page loads, initial request fetching. | Renders plain text string; does not render an animated CSS SVG spinner. |
| `shared/components/PlaceholderPage.jsx`| `PlaceholderPage`| Unimplemented route stub | `title` | Page heading and generic stub message | **No for production**: Only useful during scaffolding. | Static placeholder content. |

---

## 6. Existing Page Layout Patterns

The repository currently exhibits two primary layout paradigms:
1. **Centered Card Auth Layout** (`LoginPage.jsx`):
   * Outer container: Full-viewport flex container (`minHeight: "100vh"`, `alignItems: "center"`, `justifyContent: "center"`).
   * Background: `colors.background` (`#F6F8FA`).
   * Card container: `AppCard` constrained to `maxWidth: "420px"`, full width on mobile (`width: "100%"`).
   * Form layout: Vertical stack using `spacing.lg` (24px) for section breaks and `spacing.md` (16px) between inputs, full-width `AppButton` at bottom.
2. **Dashboard Two-Column Layout** (`AdminLayout.jsx`):
   * Outer container: Flex container (`minHeight: "100vh"`), horizontal layout.
   * Left aside: Fixed `width: "240px"`, background `colors.primaryDark` (`#173B5E`), white text, vertical navigation links with `spacing.md` gaps.
   * Main content: `flex: 1`, padding `spacing.lg` (24px), white background or page background with `<Outlet />`.

### Recommended Pattern for Component 1 Customer Pages
Customer-facing pages in Component 1 (Create Request, Request Details, Request List) should follow a **Centered Content Max-Width Shell** (`maxWidth: "960px"` or `1120px"`, centered with auto margins, padded with `spacing.lg`), featuring:
* Page Header: Title with `typography.pageTitle` and optional subtitle with `typography.body` (`colors.textSecondary`).
* Structured Sections: Grouped into `AppCard` containers.
* Form Layout: Vertical form controls with clear visual hierarchy, labels, and helper texts.
* Responsive Margin: `padding: spacing.md` on mobile, `padding: spacing.lg` on desktop.

---

## 7. Navigation / App Shell

1. **Current Navigation System**:
   * Located at `web/src/shared/layouts/AdminLayout.jsx`.
   * Only accommodates administrator routes:
     * `/dashboard`
     * `/service-requests` (currently renders `<PlaceholderPage title="Service Requests" />`)
     * `/providers`
     * `/quotations`
     * `/service-tracking`
     * `/ai-workflows`
   * Displays the logged-in administrator's name (`user?.fullName`) and a Logout button calling `authStore.logout()`.
2. **Missing Customer Navigation Shell**:
   * There is **no public layout** or **customer authenticated layout** currently present in the repository.
   * Component 1 requires a dedicated **Customer Shell** (e.g., `CustomerLayout.jsx` or a top navigation bar) that provides customer-specific navigation:
     * **My Requests** (`/service-requests`)
     * **Create Request** (`/service-requests/new`)
     * User profile display (`user.fullName`, `Role: Customer`)
     * Logout action

---

## 8. Routing Architecture

Routing is configured in `web/src/app/router/AppRouter.jsx` utilizing `react-router-dom` v7.

### Current Route Map
| Route Path | Element | Route Protection | Allowed Roles |
| :--- | :--- | :--- | :--- |
| `/login` | `<LoginPage />` | Public | All |
| `/unauthorized` | `<PlaceholderPage title="Unauthorized" />` | Public | All |
| `/` | `<Navigate to="/dashboard" replace />` | Public (Redirect) | N/A |
| `/dashboard` | `<PlaceholderPage title="Dashboard" />` | Protected | `["Admin"]` |
| `/service-requests`| `<PlaceholderPage title="Service Requests" />` | Protected | `["Admin"]` |
| `/providers` | `<PlaceholderPage title="Providers" />` | Protected | `["Admin"]` |
| `/quotations` | `<PlaceholderPage title="Quotations" />` | Protected | `["Admin"]` |
| `/service-tracking`| `<PlaceholderPage title="Service Tracking" />` | Protected | `["Admin"]` |
| `/ai-workflows` | `<PlaceholderPage title="AI Workflows" />` | Protected | `["Admin"]` |
| `*` | `<PlaceholderPage title="Page Not Found" />` | Public | All |

### Guard Mechanism
* Implemented via `web/src/shared/auth/ProtectedRoute.jsx`.
* Verifies presence of `token` and `user` in `useAuthStore`. If missing, redirects to `/login`.
* If `allowedRoles` array is passed, checks `allowedRoles.includes(user.role)`. If unauthorized, redirects to `/unauthorized`.

### Required Routing Architecture for Component 1
Component 1 must introduce customer-accessible routes under `ProtectedRoute allowedRoles={["Customer"]}`:
* `/service-requests` -> Customer Request List (`ServiceRequestListPage`)
* `/service-requests/new` -> Create Request Form (`CreateServiceRequestPage`)
* `/service-requests/:id` -> Request Details, Problem Analysis, & AI Workflow (`ServiceRequestDetailPage`)
* `/service-requests/:id/edit` -> Edit Request Form (`EditServiceRequestPage`)

---

## 9. Authentication Architecture

The existing authentication infrastructure is solid, fully functional, and ready for customer integration.

### Core Components
1. **Zustand Store** (`web/src/shared/auth/authStore.js`):
   * State: `user` (`{ userId, fullName, email, role }`), `token`, `loading`, `error`.
   * Persistence: Both `assistlk_token` and `assistlk_user` are persisted in browser `sessionStorage`.
   * Actions:
     * `login(email, password)`: Dispatches `POST /api/auth/login`, saves token and serialized user to `sessionStorage`, updates reactive store.
     * `logout()`: Clears `sessionStorage` and resets store state to `null`.
     * `setError(message)`: Updates error notification state.
2. **Token Attachment & Expiry** (`web/src/shared/api/apiClient.js`):
   * Request Interceptor: Automatically retrieves `assistlk_token` from `sessionStorage` and injects `Authorization: Bearer <token>`.
   * Response Interceptor: On HTTP `401 Unauthorized`, automatically clears `sessionStorage` tokens.
3. **Backend Auth Support**:
   * The backend `AuthController` (`backend/src/AssistLK.Api/Controllers/AuthController.cs`) provides:
     * `POST /api/auth/login` (Returns `{ token, userId, fullName, email, role, expiresAtUtc }`)
     * `POST /api/auth/register` (Supports registering a user with `UserRole.Customer = 1`)
     * `GET /api/auth/me` (Authenticated profile fetch)
4. **Current Frontend Restriction to Address in F2**:
   * In `web/src/features/auth/pages/LoginPage.jsx` (lines 34–40), there is an explicit guard:
     ```javascript
     if (user.role !== "Admin") {
       useAuthStore.getState().logout();
       setError("This web portal is available to administrators only.");
       return;
     }
     ```
   * **Direct Reuse Strategy**: The underlying `authStore.js` and `apiClient.js` can be reused directly by Component 1. In Phase F2, `LoginPage.jsx` should be updated to route users according to role (e.g., `Customer` -> `/service-requests`, `Admin` -> `/dashboard`).

---

## 10. API Client Architecture

### Axios Instance (`web/src/shared/api/apiClient.js`)
* Base URL: Configured via `import.meta.env.VITE_API_BASE_URL`. Defaults to `http://localhost:5012` via `.env`.
* Headers: Default `Content-Type: application/json`.
* Request Interceptor: Injects Bearer token from `sessionStorage.getItem("assistlk_token")`.
* Response Interceptor: Catches 401 and clears authentication session.

### Backend Endpoints Ready for Component 1 Integration
The backend `ServiceRequestsController` (`backend/src/AssistLK.Api/Controllers/ServiceRequestsController.cs`) requires `[Authorize(Roles = "Customer")]` and provides the exact REST contract:

| HTTP Method | Endpoint Path | Request Body | Response Body | Description |
| :--- | :--- | :--- | :--- | :--- |
| `POST` | `/api/service-requests` | `CreateServiceRequestRequest` (`description`, `locationText`, `latitude?`, `longitude?`) | `ServiceRequestResponse` | Creates a new service request in `Created` state. |
| `GET` | `/api/service-requests/my` | None | `IReadOnlyList<ServiceRequestResponse>` | Retrieves all service requests belonging to current customer. |
| `GET` | `/api/service-requests/{id}` | None | `ServiceRequestResponse` | Retrieves service request details by GUID. |
| `PUT` | `/api/service-requests/{id}` | `UpdateServiceRequestRequest` (`description`, `locationText`, `latitude?`, `longitude?`) | `ServiceRequestResponse` | Updates description and location (allowed in `Created`, `Analyzing`, `AwaitingInformation`). |
| `POST` | `/api/service-requests/{id}/cancel` | None | `ServiceRequestResponse` | Cancels the service request (transitions to `Cancelled`). |
| `POST` | `/api/service-requests/{id}/analyze` | None | `ProblemUnderstandingResponseDto` | Executes the Problem Understanding AI Agent workflow. |
| `POST` | `/api/service-requests/{id}/ready-for-matching` | None | `ServiceRequestResponse` | Transitions request to `ReadyForMatching` for Component 2 handoff. |

---

## 11. Form & Validation Conventions

### Existing Conventions
* Native Controlled Inputs: `value={state}`, `onChange={(e) => setState(e.target.value)}`.
* Label Placement: Rendered above input using `typography.body` and `colors.textPrimary` with `marginBottom: spacing.sm`.
* Error Display: Rendered below input using `typography.small` and `colors.error` with `marginTop: spacing.xs`.
* Required Indicator: Handled via native HTML `required` and component props.
* Submission Button: `AppButton` with `type="submit"`, `disabled={loading}`.

### Component 1 Requirements & Missing Pieces
1. **Description Field (Up to 4000 characters)**:
   * The existing `AppInput` component is restricted to an `<input>` tag.
   * A multi-line textarea component (`AppTextArea`) must be introduced into `web/src/shared/components/` following identical styling tokens (`inputStyles`, `minHeight: 120px`, `resize: vertical`).
2. **Location Field**:
   * Uses `AppInput` for text location (`locationText`).
   * An action button ("Use Current Location") placed beside or below the input to trigger GPS coordinates.

---

## 12. Status / Badge Conventions

The backend manages the complete Component 1 lifecycle through the `ServiceRequestStatus` enum:
1. `Created` (Initial state upon submission)
2. `Analyzing` (AI Problem Understanding workflow in progress)
3. `AwaitingInformation` (Agent determined information is missing / follow-up questions active)
4. `Analyzed` (Agent completed classification and problem summary)
5. `ReadyForMatching` (Customer confirmed analysis; ready for Component 2 provider matching)
6. `Cancelled` (Customer cancelled request)

### Current Status Badge Inventory
* **Status**: NO reusable `StatusBadge` or chip component currently exists in `web/src/shared/components/`.
* **Centralized Semantic Colors**:
  * `colors.success`: `#2E7D32`
  * `colors.warning`: `#C45A11`
  * `colors.error`: `#B42318`
  * `colors.primary`: `#1F4E78`
  * `colors.secondary`: `#0F6B66`
  * `colors.disabled`: `#9CA3AF`

### Status Badge Recommendation for Component 1
In Phase F2, a shared `StatusBadge.jsx` component should be created in `web/src/shared/components/` using:
* Pill radius (`radius.pill`: `999px`).
* Small font (`typography.small`: `12px`, `fontWeight: 600`).
* Consistent status-to-color mapping:
  * `Created` -> Neutral / Slate (`backgroundColor: "#E5E7EB"`, `color: "#374151"`)
  * `Analyzing` -> Secondary Teal / Indigo (`backgroundColor: "#E0F2F1"`, `color: "#0F6B66"`)
  * `AwaitingInformation` -> Warning Amber (`backgroundColor: "#FEF3C7"`, `color: "#C45A11"`)
  * `Analyzed` -> Success Green (`backgroundColor: "#DCFCE7"`, `color: "#2E7D32"`)
  * `ReadyForMatching` -> Brand Primary Blue (`backgroundColor: "#DBEAFE"`, `color: "#1F4E78"`)
  * `Cancelled` -> Error Red (`backgroundColor: "#FEE2E2"`, `color: "#B42318"`)

---

## 13. GPS / Location Readiness

### Inspection Findings
* Geolocation Packages: None installed in `web/package.json` (no Leaflet, Google Maps, Mapbox, OpenStreetMap).
* Geolocation Hooks / Services: None exist in `web/src/`.
* Geocoding APIs: No map API keys or reverse-geocoding endpoints configured in `web/.env`.

### Technical Feasibility & Solution Strategy
1. **Browser Native Support**:
   * Modern web browsers implement the W3C Geolocation API (`navigator.geolocation.getCurrentPosition`).
   * This native API provides direct, high-accuracy access to `coords.latitude` and `coords.longitude` without requiring any third-party npm packages or external API keys.
2. **Backend Contract Alignment**:
   * The backend `CreateServiceRequestRequest` and `UpdateServiceRequestRequest` accept:
     * `LocationText` (`string`, required, max 255 chars)
     * `Latitude` (`decimal?`, optional, range -90 to 90)
     * `Longitude` (`decimal?`, optional, range -180 to 180)
3. **User Experience Protocol**:
   * **GPS is strictly optional**: Users are never blocked if location permission is denied or unsupported.
   * **Two-mode input**:
     * **Mode A (Current Location)**: User clicks "Use Current Location" button. Browser prompts for permission. When granted, latitude and longitude are stored in component state, and a descriptive label (e.g., "Current GPS Location (6.9271° N, 79.8612° E)") is populated into `locationText` if empty.
     * **Mode B (Manual Entry)**: User types location description (e.g., "High Level Road, Maharagama") directly into `AppInput`.
4. **Reverse Geocoding Assessment**:
   * Reverse geocoding (converting lat/long into street address) is **not required for Component 1 core operation** and would add external API dependency risks. The customer can provide or refine the manual location text directly.

---

## 14. Component 1 Future Page Mapping

| Future Page / View | Purpose | Core Content / Actions | Existing Shared Components to Reuse |
| :--- | :--- | :--- | :--- |
| **1. Create Service Request** (`CreateServiceRequestPage`) | Customer submits a new problem | Description input, manual location input, GPS location button, Submit button | `AppCard`, `AppInput`, `AppButton`, `ErrorMessage` |
| **2. My Service Requests** (`ServiceRequestListPage`) | Customer reviews all submitted requests | List of requests, status badges, timestamps, view details link, "New Request" button | `AppCard`, `AppButton`, `LoadingSpinner`, `ErrorMessage` |
| **3. Service Request Details** (`ServiceRequestDetailPage`) | Primary hub for single request | Category, Urgency, Status, Problem Summary, Confidence, Actions (Analyze, Edit, Cancel, Confirm) | `AppCard`, `AppButton`, `LoadingSpinner`, `ErrorMessage` |
| **4. Edit Service Request** (`EditServiceRequestPage`) | Modify description or location | Pre-populated description and location fields, save updates button | `AppCard`, `AppInput`, `AppButton`, `ErrorMessage` |
| **5. AI Analysis State** (`AnalysisResultCard`) | Displays Problem Understanding Agent classification results | Detected problem, service category, urgency badge, confidence score | `AppCard`, `StatusBadge`, `AppButton` |
| **6. Clarification State** (`ClarificationSection`) | Handles `AwaitingInformation` state | Displays agent follow-up questions, prompts customer to update description, re-analyze action | `AppCard`, `AppButton`, `ErrorMessage` |
| **7. Review & Confirm** (`ConfirmMatchingSection`) | Customer confirms structured analysis | Summary of categorized request, "Confirm & Find Providers" button calling `/ready-for-matching` | `AppCard`, `AppButton` |
| **8. Component 2 Boundary** (`ReadyForMatchingBanner`) | Handoff state indicator | Banner stating request is ready for Provider Matching (Component 2) | `AppCard`, `StatusBadge` |

---

## 15. Shared Components Component 1 Can Reuse

1. **`AppButton`** (`web/src/shared/components/AppButton.jsx`):
   * Reusable for: Primary actions ("Submit Request", "Analyze Problem", "Confirm & Match Providers"), secondary actions ("Edit Details"), danger actions ("Cancel Request"), and outline actions ("Back to Requests").
2. **`AppCard`** (`web/src/shared/components/AppCard.jsx`):
   * Reusable for: Page section cards, form containers, request detail cards, AI analysis summary boxes, clarification cards.
3. **`AppInput`** (`web/src/shared/components/AppInput.jsx`):
   * Reusable for: Location text input, follow-up clarification single-line inputs.
4. **`ErrorMessage`** (`web/src/shared/components/ErrorMessage.jsx`):
   * Reusable for: API submission errors, network failures, cancellation warnings, analysis error feedback.
5. **`LoadingSpinner`** (`web/src/shared/components/LoadingSpinner.jsx`):
   * Reusable for: Page initial data fetch, background status polling.

---

## 16. Missing Shared Components We May Need

To ensure strict adherence to the AssistLK Design System across all components, the following reusable components should be added to `web/src/shared/components/` during Phase F2:

1. **`AppTextArea.jsx`**:
   * Multi-line textarea variant sharing identical design tokens (`colors.border`, `colors.surface`, `radius.medium`, `typography.body`, `spacing.md` padding). Essential for the 4000-character request description.
2. **`StatusBadge.jsx`**:
   * Semantic badge/chip component displaying status (`Created`, `Analyzing`, `AwaitingInformation`, `Analyzed`, `ReadyForMatching`, `Cancelled`) with centralized background and foreground colors.
3. **`CustomerLayout.jsx`**:
   * Dedicated authenticated application shell for Customers, featuring a top navbar, AssistLK brand logo, "My Requests" link, "Create Request" link, user info, and Logout button.

---

## 17. Proposed Component 1 Frontend Folder Structure

Following the existing pattern in `web/src/features/` where `serviceRequests/` is already established with `.gitkeep`:

```text
web/src/features/serviceRequests/
├── components/
│   ├── AnalysisResultCard.jsx      (Displays AI category, urgency, problem summary, confidence)
│   ├── ClarificationPrompt.jsx     (Displays agent follow-up questions & update prompt)
│   ├── LocationPicker.jsx          (Coordinates manual location text & GPS trigger)
│   └── ServiceRequestCard.jsx      (Summary card for list view with status & dates)
├── hooks/
│   ├── useGeolocation.js           (Custom hook encapsulating navigator.geolocation)
│   └── useServiceRequest.js        (Hook managing request fetch, polling, & workflow state)
├── pages/
│   ├── CreateServiceRequestPage.jsx(Page: Customer creates a new request)
│   ├── EditServiceRequestPage.jsx  (Page: Customer edits existing request)
│   ├── ServiceRequestDetailPage.jsx(Page: Details, analysis trigger, clarification, confirmation)
│   └── ServiceRequestListPage.jsx  (Page: Customer views list of their requests)
└── services/
    └── serviceRequestService.js    (Axios API service methods calling backend endpoints)
```

---

## 18. Proposed Component 1 Routes

To be registered in `web/src/app/router/AppRouter.jsx` within a Customer-guarded route block:

```jsx
<Route element={<ProtectedRoute allowedRoles={["Customer"]} />}>
  <Route element={<CustomerLayout />}>
    <Route path="/service-requests" element={<ServiceRequestListPage />} />
    <Route path="/service-requests/new" element={<CreateServiceRequestPage />} />
    <Route path="/service-requests/:id" element={<ServiceRequestDetailPage />} />
    <Route path="/service-requests/:id/edit" element={<EditServiceRequestPage />} />
  </Route>
</Route>
```

---

## 19. Proposed Component 1 API Service Structure

File location: `web/src/features/serviceRequests/services/serviceRequestService.js`

```javascript
import apiClient from "../../../shared/api/apiClient";

export const serviceRequestService = {
  // POST /api/service-requests
  create: async (data) => {
    const response = await apiClient.post("/api/service-requests", data);
    return response.data;
  },

  // GET /api/service-requests/my
  getMyRequests: async () => {
    const response = await apiClient.get("/api/service-requests/my");
    return response.data;
  },

  // GET /api/service-requests/{id}
  getById: async (id) => {
    const response = await apiClient.get(`/api/service-requests/${id}`);
    return response.data;
  },

  // PUT /api/service-requests/{id}
  update: async (id, data) => {
    const response = await apiClient.put(`/api/service-requests/${id}`, data);
    return response.data;
  },

  // POST /api/service-requests/{id}/cancel
  cancel: async (id) => {
    const response = await apiClient.post(`/api/service-requests/${id}/cancel`);
    return response.data;
  },

  // POST /api/service-requests/{id}/analyze
  analyze: async (id) => {
    const response = await apiClient.post(`/api/service-requests/${id}/analyze`);
    return response.data;
  },

  // POST /api/service-requests/{id}/ready-for-matching
  markReadyForMatching: async (id) => {
    const response = await apiClient.post(`/api/service-requests/${id}/ready-for-matching`);
    return response.data;
  },
};

export default serviceRequestService;
```

---

## 20. Risks / Integration Concerns

1. **Admin-Locked Login Page**:
   * `LoginPage.jsx` explicitly rejects users if `user.role !== "Admin"`. A customer attempting to log in will be logged out immediately with an error message.
   * *Mitigation*: Update `LoginPage.jsx` in Phase F2 to support role-based redirects (`Customer` -> `/service-requests`).
2. **Missing Customer Layout**:
   * The only layout in the repository is `AdminLayout.jsx` with an Admin sidebar. Placing customer routes inside `AdminLayout` would cause role rejection in `ProtectedRoute`.
   * *Mitigation*: Introduce `CustomerLayout.jsx` in `web/src/shared/layouts/` tailored for customer navigation.
3. **No Multi-Line Text Input**:
   * The backend requires `Description` (up to 4000 characters). `AppInput` only renders single-line inputs.
   * *Mitigation*: Create `AppTextArea.jsx` in `web/src/shared/components/` using the exact design system tokens.
4. **Geolocation Browser Permissions & HTTPS Context**:
   * `navigator.geolocation` requires user permission and in production environments requires HTTPS or localhost. If denied or in an unsecure context, geolocation will fail.
   * *Mitigation*: The UI must gracefully catch permission errors, notify the user with a friendly hint, and keep manual location entry fully operational at all times.

---

## 21. Recommendations Before Implementation

1. **Keep Component 1 Focused**: Strictly maintain boundaries; do not build provider matching screens, quotation forms, or tracking workflows inside Component 1.
2. **Follow Existing Code Style**: Maintain the existing clean JavaScript (ES modules, React hooks, inline token styling using `web/src/shared/theme/`) without introducing heavy external UI dependencies.
3. **Reuse Existing Tokens**: Never hardcode colors like `#1F4E78` or `#2E7D32` directly in feature components; import them from `web/src/shared/theme`.
4. **Centralize Service Request State**: Use a dedicated service file (`serviceRequestService.js`) to insulate UI components from Axios details.
5. **Support Graceful Polling / Loading**: The AI problem understanding workflow (`/analyze`) may take several seconds to process LLM and classification tool chains. The UI should display a clear analyzing status and disable double-submissions.

---

## 22. Readiness Decision for Frontend Phase F2

### **READY FOR F2**

The inspection confirms that the AssistLK frontend foundation is clean, modular, and fully prepared for Component 1 customer frontend development. The backend REST endpoints and security policies are 100% complete and tested.

### Exact Scope for Frontend Phase F2:
1. **Shared UI Extensions**: Implement `AppTextArea.jsx` and `StatusBadge.jsx` in `web/src/shared/components/`.
2. **Customer App Shell & Auth Flow**: Implement `CustomerLayout.jsx` in `web/src/shared/layouts/` and adapt `LoginPage.jsx` to route `Customer` users to `/service-requests`.
3. **Component 1 Service & Geolocation Hook**: Create `serviceRequestService.js` and `useGeolocation.js` in `web/src/features/serviceRequests/`.
4. **Component 1 Customer Pages**: Implement `CreateServiceRequestPage`, `ServiceRequestListPage`, `ServiceRequestDetailPage`, and `EditServiceRequestPage` in `web/src/features/serviceRequests/pages/`.
5. **Route Registration**: Mount Component 1 customer routes in `web/src/app/router/AppRouter.jsx`.
