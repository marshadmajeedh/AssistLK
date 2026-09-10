# AssistLK Development Environment Setup Guide

> **Developer Onboarding Guide**  
> *Follow this guide before starting Component Development to get the entire AssistLK ecosystem up and running locally.*

---

## 1. Purpose

This document explains how to set up the **AssistLK** development environment.

It covers:
- Required software installation
- Backend setup (.NET 8)
- Database setup (PostgreSQL 16+)
- Frontend setup (React)
- Mobile setup (Flutter)
- Environment configuration
- Entity Framework database migration commands
- Running the complete system

> **Note:** This guide must be followed by all developers before starting component development. A new teammate should be able to clone the repository and run AssistLK end-to-end without blockers.

---

## 2. System Requirements

### Recommended Operating System
- **Windows 10 / Windows 11** (Linux & macOS supported with corresponding CLI tools)

### Required Tools & SDKs

| Tool | Version | Purpose |
|---|---|---|
| **.NET SDK** | `8.x` | Backend API & Application core |
| **PostgreSQL** | `16+` | Primary relational database |
| **Node.js** | `LTS (v20+)` | Frontend tooling & runtime |
| **Flutter** | `Latest Stable` | Cross-platform mobile client |
| **Git** | `Latest` | Version control |
| **Visual Studio Code** | `Latest` | Recommended IDE / Editor |

---

## 3. Clone Repository

1. Clone the repository:
   ```bash
   git clone <repository-url>
   ```

2. Move into the project root directory:
   ```bash
   cd AssistLK
   ```

3. Update and switch to the latest development branch:
   ```bash
   git checkout develop
   git pull origin develop
   ```

---

## 4. Backend Setup (.NET 8)

### 4.1 Install .NET 8 SDK
- Download and install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
- Verify installation:
  ```bash
  dotnet --version
  ```
  *Expected output:* `8.x.x`

### 4.2 Backend Project Structure
```text
backend/
├── src/
│   ├── AssistLK.Api
│   ├── AssistLK.Application
│   ├── AssistLK.Domain
│   └── AssistLK.Infrastructure
└── tests/
```

### 4.3 Restore Backend Dependencies
Navigate to the backend folder and restore packages:
```bash
cd backend
dotnet restore
```

### 4.4 Build Backend
Run:
```bash
dotnet build
```
*Expected output:*
```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## 5. PostgreSQL Database Setup

### 5.1 Install PostgreSQL
- Install **PostgreSQL Server 16+** and optionally **pgAdmin 4**.
- Verify PostgreSQL CLI is in your PATH:
  ```bash
  psql --version
  ```

### 5.2 Create Database
1. Open the PostgreSQL prompt:
   ```bash
   psql -U postgres
   ```
2. Create the `assistlk` database:
   ```sql
   CREATE DATABASE assistlk;
   ```
3. Exit the interactive shell:
   ```text
   \q
   ```

### 5.3 Shared Database Connection Configuration (Supabase)
The backend uses **Entity Framework Core** with **PostgreSQL**.
In development, the team connects to a shared **Supabase PostgreSQL** database using the **Session Pooler** (`port 5432`).

> ⚠️ **Security Requirement:** Never commit database credentials, passwords, or connection strings to Git. Do not place real credentials in `appsettings.json`, `appsettings.Development.json`, or tracked files.

#### Setting Your Connection Locally (.NET User Secrets)
From `backend/`:
```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=<pooler-host>;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=<your-password>;SSL Mode=Require;Trust Server Certificate=true;" --project src/AssistLK.Api
```

Alternatively, you can export the environment variable:
```bash
# Windows PowerShell
$env:ConnectionStrings__DefaultConnection="Host=<pooler-host>;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=<your-password>;SSL Mode=Require;Trust Server Certificate=true;"

# Linux / macOS / Bash
export ConnectionStrings__DefaultConnection="Host=<pooler-host>;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=<your-password>;SSL Mode=Require;Trust Server Certificate=true;"
```

### 5.4 Team Database & Migration Policy
1. **Shared Development Database:** All team members' local ASP.NET Core backends connect to the same shared Supabase PostgreSQL instance.
2. **Single Migrator Rule:** **ONLY ONE DESIGNATED DEVELOPER** runs database migrations (`dotnet ef database update`) against the shared Supabase database. Teammates must NOT independently run database updates against the shared database to prevent migration lock contention and schema drift.
3. **Isolated Integration Tests:** Automated integration tests continue to run against an isolated local or ephemeral database (`assistlk_test_integration`) and will NEVER execute against the shared Supabase database.

---

## 6. Entity Framework Migration Setup

Install the EF Core CLI global tool (if not already installed):
```bash
dotnet tool install --global dotnet-ef
```
Verify the installation:
```bash
dotnet ef --version
```

### 6.1 Create Migrations
From the `backend/` directory:
```bash
cd backend
dotnet ef migrations add InitialCreate --project src/AssistLK.Infrastructure --startup-project src/AssistLK.Api
```

**Migration naming conventions & examples:**
- `AddProblemAnalysisTables`
- `AddProviderTables`
- `AddQuotationTables`
- `AddTrackingTables`

### 6.2 Apply Migrations
Update the database schema:
```bash
dotnet ef database update --project src/AssistLK.Infrastructure --startup-project src/AssistLK.Api
```

---

## 7. Running Backend

1. Navigate to the API project directory:
   ```bash
   cd backend/src/AssistLK.Api
   ```
2. Run the application:
   ```bash
   dotnet run
   ```
3. The backend API will start on:
   ```text
   https://localhost:xxxx (e.g., https://localhost:5001)
   ```

---

## 8. Frontend Setup (React)

### 8.1 Install Node.js
- Install Node.js LTS from [nodejs.org](https://nodejs.org/).
- Verify installation:
  ```bash
  node --version
  npm --version
  ```

### 8.2 Frontend Location & Dependencies
Frontend directory: `frontend/`

```bash
cd frontend
npm install
```

### 8.3 Run Frontend
```bash
npm run dev
```
Frontend will be accessible at:
```text
http://localhost:5173
```

---

## 9. Mobile Setup (Flutter)

### 9.1 Install Flutter
- Install the latest stable Flutter SDK from [flutter.dev](https://flutter.dev/).
- Verify installation:
  ```bash
  flutter --version
  ```

### 9.2 Check Environment & Tooling
Run the doctor tool and resolve all required warnings/issues:
```bash
flutter doctor
```

### 9.3 Install Dependencies
Mobile directory: `mobile/`

```bash
cd mobile
flutter pub get
```

### 9.4 Run Mobile Application
Ensure an emulator or physical device is connected:
```bash
flutter devices
```
Launch on an active device/emulator:
```bash
flutter run
```

---

## 10. Environment Variables

> 🔒 Ensure environment configuration files containing sensitive secrets are listed in `.gitignore`.

- **Backend** (`backend/src/AssistLK.Api/appsettings.Development.json`):
  ```json
  {
    "JwtSettings": {
      "Secret": "development-secret-key-change-in-production"
    }
  }
  ```

- **Frontend** (`frontend/.env`):
  ```env
  VITE_API_URL=https://localhost:5001
  ```

- **Mobile** (`mobile/.env`):
  ```env
  API_BASE_URL=http://10.0.2.2:5001
  ```
  *(Use `http://10.0.2.2:5001` for Android Emulator localhost routing or `http://localhost:5001` for iOS Simulator)*

---

## 11. Complete Development Workflow

### Starting a New Task:
1. Always synchronize the latest changes:
   ```bash
   git checkout develop
   git pull origin develop
   ```
2. Create your dedicated feature branch:
   ```bash
   git checkout -b feature/component-1-problem-agent
   ```

---

## 12. Running the Complete System

### System Architecture Flow:
```text
 Mobile (Flutter) / Web Client (React)
                  │
                  ▼
         .NET 8 Backend API
                  │
                  ▼
           Agent Framework
                  │
                  ▼
        PostgreSQL Database
```

### Startup Order:
1. **Database:** Ensure PostgreSQL service is active.
2. **Backend:** Run `dotnet run` inside `backend/src/AssistLK.Api`.
3. **Frontend:** Run `npm run dev` inside `frontend/`.
4. **Mobile:** Run `flutter run` inside `mobile/`.

---

## 13. Common Commands Cheatsheet

### Backend (.NET)
```bash
dotnet restore      # Restore NuGet packages
dotnet build        # Build entire solution
dotnet test         # Run unit & integration tests
dotnet run          # Launch the API service
```

### Frontend (React / Vite)
```bash
npm install         # Install npm packages
npm run dev         # Start local Vite development server
npm run build       # Build production bundle
npm run preview     # Preview production build locally
```

### Mobile (Flutter)
```bash
flutter pub get     # Fetch dependencies
flutter analyze     # Run static code analysis
flutter test        # Execute Flutter tests
flutter run         # Run on device/emulator
flutter clean       # Wipe build cache
```

---

## 14. Troubleshooting

| Issue | Potential Cause | Solution |
|---|---|---|
| **Database Connection Failed** | Service inactive or bad credentials | Check if PostgreSQL service is running, database `assistlk` exists, and credentials match `appsettings.json`. |
| **EF Core Migration Error** | Out-of-sync builds or broken model | Run `dotnet restore` followed by `dotnet build`, then retry `dotnet ef database update`. |
| **Flutter Package Issues** | Stale build cache or dependency conflict | Run `flutter clean` then `flutter pub get`. |
| **Node Module / Vite Errors** | Corrupt `node_modules` | Run `rm -rf node_modules package-lock.json && npm install`. |

---

## 15. Development Rules & Best Practices

- **Always:**
  - Pull latest changes (`git checkout develop && git pull origin develop`) before branching.
  - Test your changes locally across unit tests and UI builds.
  - Follow modular architecture and clean code principles.
- **Never:**
  - Commit hardcoded secrets, connection strings, or API keys.
  - Modify shared architectural contracts without team discussion.
  - Bypass or skip database migrations.
  - Push directly to `main` or `develop` branches (use Pull Requests).

---

## 16. Support & Escalation

If you encounter blocking setup issues:
1. Consult this setup document and troubleshooting table.
2. Check the project root `README.md` and repository Wiki.
3. Search or open an issue on **GitHub Issues**.
4. Reach out to the team on our designated communication channel.
