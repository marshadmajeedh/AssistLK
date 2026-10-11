# AssistLK React Development Guide

**Status:** Authoritative Frontend Standard  
**Applies To:** All web developers working in `web/` (React + Vite)  
**Related Documents:** [Design System](../DESIGN_SYSTEM.md), [Component Boundaries](../architecture/component-boundaries.md), [Testing Guide](testing-guide.md)

---

## 1. Architecture Overview

The web application is structured around a modular, feature-based architecture that enables 4 developers to build distinct business components without stepping on each other's code:

```text
web/src/
├── app/                  # Application-wide shell, routing, and providers
│   └── router/           # AppRouter.jsx with route definitions
├── features/             # Vertical business features (1 per component)
│   ├── serviceRequests/  # Component 1 (Problem Understanding)
│   ├── providers/        # Component 2 (Provider Management & Matching)
│   ├── quotations/       # Component 3 (Quotations & Bookings)
│   ├── tracking/         # Component 4 (Service Tracking & Feedback)
│   └── auth/             # Authentication feature (Login)
└── shared/               # Cross-cutting foundational modules
    ├── api/              # Centralized Axios/fetch client (apiClient.js)
    ├── auth/             # Auth context, token store, and ProtectedRoute
    ├── components/       # Shared design system components (AppButton, AppInput, etc.)
    ├── layouts/          # Page layouts (CustomerLayout, AdminLayout)
    └── theme/            # Shared design tokens (colors, typography, spacing, radius)
```

---

## 2. Feature Directory Conventions

Each component teammate develops within their dedicated `web/src/features/<featureName>/` folder. Inside every feature, maintain this standard subfolder convention:

```text
features/<featureName>/
├── components/           # Feature-specific presentation components
│   └── __tests__/        # Component unit & rendering tests
├── pages/                # Route-level page components
│   └── __tests__/        # Page-level integration tests
├── services/             # API client integration functions
│   └── __tests__/        # Service tests (mocking apiClient)
├── hooks/                # Custom React hooks specific to this feature
├── utils/                # Formatting, calculations, status helpers
│   └── __tests__/        # Pure utility unit tests
└── models or types/      # (Optional) JSDoc or schema contracts
```

---

## 3. Core Rules for Teammates

### 1. Reusable Shared Design System
- **Never hardcode hex colors or ad-hoc styles.** Always import design tokens from `shared/theme/`:
  ```javascript
  import { colors, spacing, radius, typography } from '@/shared/theme';
  ```
- Use the standard shared components in `shared/components/`:
  - `AppButton`: Primary, secondary, danger, and outline variants with loading states.
  - `AppInput`, `AppTextArea`: Accessible form controls with validation states.
  - `AppCard`: Standard elevation, padding, and border radius.
  - `StatusBadge`: Unified status pill with semantic color mappings.
  - `LoadingSpinner`, `ErrorMessage`: Standard state feedbacks.

### 2. Centralized API Client
- **Never use raw `fetch` or instantiate new Axios instances.** All HTTP calls must use `apiClient` from `shared/api/apiClient`:
  ```javascript
  import apiClient from '@/shared/api/apiClient';

  export const fetchProviders = async (categoryId) => {
    const response = await apiClient.get(`/api/providers?category=${categoryId}`);
    return response.data;
  };
  ```
- `apiClient` automatically attaches the Bearer JWT token from `authStore` and handles 401 Unauthorized redirects.

### 3. Centralized Authentication & Protection
- Protect feature routes using `ProtectedRoute`:
  ```jsx
  <Route
    path="/providers"
    element={
      <ProtectedRoute allowedRoles={['Provider', 'Admin']}>
        <ProviderManagementPage />
      </ProtectedRoute>
    }
  />
  ```

### 4. Component Isolation
- Developing Component 2 (`features/providers/`) must **never** require modifying Component 1 (`features/serviceRequests/`).
- If data from Component 1 is needed (e.g. `ReadyForMatching` details), fetch it through the public API contract or route parameters, not by importing internal Component 1 components.
