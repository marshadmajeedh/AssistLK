# AssistLK Web Frontend (`web/`)

React 18 + Vite frontend for AssistLK emergency and skilled service coordination.

---

## Quick Start

### 1. Install Dependencies
```bash
cd web
npm install
```

### 2. Environment Configuration
Copy `.env.example` to `.env`:
```bash
cp .env.example .env
```
Default configuration:
```env
VITE_API_BASE_URL=http://localhost:5012
```

### 3. Run Development Server
```bash
npm run dev
```

### 4. Run Tests & Linter
```bash
npm test          # Run Vitest test suite
npm run lint      # Run ESLint checks
npm run build     # Validate production build
```

---

## Directory Conventions for Teammates

```text
web/src/
├── app/                  # Application routing (AppRouter.jsx)
├── features/             # Vertical component feature modules
│   ├── serviceRequests/  # Component 1 (Problem Understanding)
│   ├── providers/        # Component 2 (Provider Management & Matching)
│   ├── quotations/       # Component 3 (Quotations & Bookings)
│   ├── tracking/         # Component 4 (Service Tracking & Feedback)
│   └── auth/             # Login & Authentication
└── shared/               # Shared Design System, API client & Layouts
```

For complete team conventions, theme tokens, and component patterns, read:
👉 **[React Development Guide](../docs/development/react-development-guide.md)**
