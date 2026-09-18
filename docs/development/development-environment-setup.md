# AssistLK Development Environment Setup Guide

## C1 Python service configuration

Follow the [canonical Python setup and configuration](../../agent-services/problem-understanding-agent/README.md#configuration). Provider keys belong to the Python service environment or its gitignored local `.env`; backend/root configuration is not the active C1 model-secret source. ASP.NET uses `AgentServices__ProblemUnderstandingUrl`, `AgentServices__InternalApiKey`, and `AgentServices__TimeoutSeconds`. Keep model keys out of React and Flutter.


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

#### Setting Your Connection Locally

You can configure your local connection using any of the three supported configuration methods:

##### Option A: Physical `.env` File (Recommended for Local Ergonomics)
1. Copy `.env.example` to `.env` in the repository root (or inside `backend/src/AssistLK.Api/`):
   ```bash
   cp .env.example .env
   ```
2. Set your Supabase connection string placeholder in `.env`:
   ```env
   ConnectionStrings__DefaultConnection=<YOUR_SUPABASE_CONNECTION_STRING>
   ```
3. Run the backend normally (`dotnet run --project src/AssistLK.Api` or via your IDE). The `.env` loader automatically detects and loads the file before the web host builds.
4. **Never commit `.env`** — verify that `.env` is ignored by Git (`.gitignore` rules already protect it).

##### Option B: .NET User Secrets
From `backend/`:
```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=<pooler-host>;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=<your-password>;SSL Mode=Require;Trust Server Certificate=true;" --project src/AssistLK.Api
```

##### Option C: Operating System / Process Environment Variables
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

## 10. Environment & Secret Configuration

> 🔒 **Security Requirement:** Environment files containing sensitive credentials (`.env`, `secrets.json`, etc.) must NEVER be committed to Git. They are explicitly excluded via `.gitignore`.

### 10.1 Backend Configuration (.NET 8)

The backend supports three complementary configuration methods for local development.

#### Supported Configuration Methods:
1. **Physical `.env` File (Local Ergonomics)**:
   - Copy `.env.example` to `.env` in the repository root:
     ```bash
     cp .env.example .env
     ```
   - Add your local values using placeholders:
     ```env
     ConnectionStrings__DefaultConnection=<YOUR_SUPABASE_CONNECTION_STRING>
     Jwt__Key=<YOUR_LOCAL_JWT_KEY>
     ASPNETCORE_ENVIRONMENT=Development
     ```
   - **Optional:** Having a `.env` file is completely optional. If absent, the backend boots normally without throwing errors.
   - **Never commit `.env`:** The file is ignored by `.gitignore`. Keep only `.env.example` tracked in Git with placeholders.
   - Run the backend normally (`dotnet run --project backend/src/AssistLK.Api` or via VS Code / Visual Studio). The backend loads `.env` before building the web application host.

2. **.NET User Secrets**:
   - Supported natively via `<UserSecretsId>15fcbe56-7908-4746-bfbe-0692dc0c8045</UserSecretsId>`:
     ```bash
     dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<YOUR_SUPABASE_CONNECTION_STRING>" --project backend/src/AssistLK.Api
     ```

3. **Operating System / Process Environment Variables**:
   - Standard OS environment variables can be exported in your shell or set in container/CI environments:
     ```bash
     export ConnectionStrings__DefaultConnection="<YOUR_SUPABASE_CONNECTION_STRING>"
     ```

#### Configuration Precedence:
When configuration is loaded, the backend follows this exact precedence order (highest to lowest):

```text
┌────────────────────────────────────────────────────────┐
│  1. Explicit OS / Process Environment Variables        │ (Highest - cannot be overwritten by .env)
├────────────────────────────────────────────────────────┤
│  2. Local Physical .env Values                         │ (Loaded into process env if not already set)
├────────────────────────────────────────────────────────┤
│  3. .NET User Secrets                                  │ (Development environment secrets store)
├────────────────────────────────────────────────────────┤
│  4. appsettings.{Environment}.json / appsettings.json  │ (Lowest - base defaults and schemas)
└────────────────────────────────────────────────────────┘
```

> **Note:** The `.env` loader is configured with `overwriteExistingVars: false`. This guarantees that an explicitly provided OS environment variable (such as one set in CI/CD or docker) will **never** be inadvertently overwritten by a `.env` file.

### 10.2 Frontend Configuration (React / Vite)
- **File:** `web/.env` (copy from `web/.env.example`)
  ```env
  VITE_API_BASE_URL=http://localhost:5012
  ```

### 10.3 Mobile Configuration (Flutter)
- **File:** `mobile/.env` (or run-time dart defines)
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
