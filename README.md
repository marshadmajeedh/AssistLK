# AssistLK

**Agentic AI-Assisted Emergency & Skilled Service Coordination Platform**

> **Course:** SE3090 – Software Engineering Frameworks  
> **Architecture:** Clean Architecture (.NET 8) + React (Vite) + Flutter + PostgreSQL + Google Gemini  
> **Components:** 4 Business Slices owned by 4 Team Members  

---

## Developer Quick-Start & Architectural Index

Welcome to AssistLK. This repository is designed to allow all team members to develop their respective backend services, agents, React views, and Flutter screens independently and safely.

Follow these 12 core sections to get started:

```text
 1. What AssistLK Is             → High-level emergency/skilled service workflow
 2. Repository Structure         → backend/, web/, mobile/, agent-services/, docs/
 3. How to Run Backend           → ASP.NET Core (.NET 8) Web API setup & launch
 4. How to Run React             → React 18 + Vite web portal setup & launch
 5. How to Run Flutter           → Flutter mobile client setup & launch
 6. How Agent Architecture Works → In-process .NET agents & external Python services
 7. How to Build a .NET Agent    → 10-step guide for C# native agents
 8. How to Build a Python Agent  → Out-of-process FastAPI/LangChain agents & contract
 9. Component Boundaries         → Responsibilities & data ownership across 4 members
10. Testing Strategy             → Real Postgres test DBs, Gemini fakes, Vitest, Flutter
11. Git Workflow                 → Trunk-based branching from develop, commit format, PRs
12. Where API Contracts Live     → Component handoff contracts (Existing vs Planned)
```

---

### 1. What AssistLK Is
AssistLK is an integrated platform connecting customers with emergency and skilled service needs (vehicle breakdown, plumbing, electrical faults, appliance repair) with verified, nearby service providers. The system coordinates problem understanding, intelligent provider matching, quotation/booking, and live service completion tracking with AI reasoning and human approval gates.

- 📖 **Full System Architecture:** [docs/architecture/system-architecture.md](docs/architecture/system-architecture.md)
- 📖 **End-to-End Workflow:** [docs/architecture/workflow-overview.md](docs/architecture/workflow-overview.md)

---

### 2. Repository Structure

```text
AssistLK/
├── backend/                  # ASP.NET Core (.NET 8) Clean Architecture solution
│   ├── AssistLK.sln
│   ├── src/
│   │   ├── AssistLK.Api             # HTTP Controllers, Auth, Middleware, DTOs
│   │   ├── AssistLK.Application     # Business workflows, Interfaces, DTOs
│   │   ├── AssistLK.Domain          # Pure entities, Enums, Value Objects
│   │   ├── AssistLK.Infrastructure  # PostgreSQL DbContext, Migrations, Repositories
│   │   └── AssistLK.Agents          # Agent adapters, HTTP clients, Tools, Safety
│   └── tests/
│       ├── AssistLK.Api.Tests       # API endpoint & security tests
│       └── AssistLK.IntegrationTests # Service, workflow, and PostgreSQL tests
├── web/                      # React 18 + Vite frontend for Customers & Staff
│   ├── src/app/              # Global routing (AppRouter.jsx)
│   ├── src/features/         # Vertical features (serviceRequests, providers, etc.)
│   └── src/shared/           # Shared design system, API client, auth
├── mobile/                   # Flutter cross-platform mobile application
│   └── lib/
│       ├── core/             # ApiClient, TokenStorage, AppConfig
│       ├── shared/           # Design system tokens and shared widgets
│       └── features/         # Vertical feature screens and providers
├── agent-services/           # Canonical root for Python agent microservices
└── docs/                     # Authoritative system documentation and guides
```

---

### 3. How to Run Backend

1. **Prerequisites:** .NET 8 SDK and PostgreSQL 15+ installed.
2. **Database Setup:** Configure local connection string in `backend/src/AssistLK.Api/appsettings.Development.json` or User Secrets:
   ```bash
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=assistlk_db;Username=postgres;Password=yourpassword" --project backend/src/AssistLK.Api
   ```
3. **Run Migrations & Start API:**
   ```bash
   dotnet run --project backend/src/AssistLK.Api
   ```
   API runs at: `http://localhost:5012` (Swagger UI at `/swagger`).

- 📖 **Detailed Environment Guide:** [docs/development/development-environment-setup.md](docs/development/development-environment-setup.md)

---

### 4. How to Run React Frontend

1. **Install Dependencies:**
   ```bash
   cd web && npm install
   ```
2. **Configure Environment:**
   Copy `.env.example` to `.env` (default `VITE_API_BASE_URL=http://localhost:5012`).
3. **Start Development Server:**
   ```bash
   npm run dev
   ```
   Web portal opens at: `http://localhost:5173`.

- 📖 **React Development Guide:** [docs/development/react-development-guide.md](docs/development/react-development-guide.md)

---

### 5. How to Run Flutter Mobile

1. **Install Dependencies:**
   ```bash
   cd mobile && flutter pub get
   ```
2. **Start App:**
   - Chrome: `flutter run -d chrome`
   - Android Emulator: `flutter run`
   - Physical Device: `flutter run --dart-define=API_BASE_URL=http://<YOUR_IP>:5012/api`

- 📖 **Flutter Development Guide:** [docs/development/flutter-development-guide.md](docs/development/flutter-development-guide.md)

---

### 6. How Agent Architecture Works

AssistLK supports clean agent integration:
1. **Agent Foundation (.NET):** Core abstractions in `AssistLK.Agents` (`IAgent`, `AgentRegistry`, `AgentOrchestrator`, `ToolExecutor`, safety policies, and client adapters).
2. **External Python Agent Services (Out-of-Process):** Standalone microservices in `agent-services/<agent-name>` using FastAPI and LangGraph (such as Component 1 Problem Understanding).

> **CRITICAL ARCHITECTURAL RULE:** Agents **never** directly access `AssistLKDbContext`, repositories, or mutate database entities. All persistence and lifecycle states are managed by Application workflow services.

- 📖 **Agent Foundation:** [docs/architecture/agent-foundation.md](docs/architecture/agent-foundation.md)

---

### 7. How to Build a .NET Agent

Follow the 10-step developer guide:
1. Define single responsibility
2. Define structured input/output DTOs
3. Implement `IAgent`
4. Inject `IGeminiService`
5. Add deterministic tools via `ToolExecutor`
6. Apply safety sanitization
7. Use memory strictly for explicit useful facts
8. Integrate through Application workflow service
9. Register in DI and `AgentRegistry`
10. Add automated tests with mocked LLM (`FakeGeminiService`)

- 📖 **.NET Agent Developer Guide:** [docs/development/how-to-create-dotnet-agent.md](docs/development/how-to-create-dotnet-agent.md)

---

### 8. How to Build & Connect a Python Agent

Python agents reside in `agent-services/<name>/`. They operate strictly as out-of-process inference workers and communicate with .NET via provider-neutral JSON:
- .NET handles Authentication, Authorization, Domain Rules, and PostgreSQL persistence.
- Python handles ML reasoning, LangGraph state machines, and structured outputs.
- 📖 **Python Agent Architecture:** [docs/architecture/external-python-agent-service.md](docs/architecture/external-python-agent-service.md)
- 📖 **External Agent Contract & JSON Schema:** [docs/architecture/external-agent-contract.md](docs/architecture/external-agent-contract.md)

#### Component 1 One-Command Development Launcher

##### First-Time Setup
```powershell
cd agent-services/problem-understanding-agent
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
```
Configure local gitignored Python `.env` (`agent-services/problem-understanding-agent/.env`).

##### Normal Daily Start
To start both the Python Agent and ASP.NET Core in a single command:
```powershell
.\scripts\start-c1-dev.ps1
```
This launcher:
1. Starts the Python Agent on `http://127.0.0.1:8001` using `.venv/Scripts/python.exe`.
2. Validates health check (`/health` returns HTTP 200) before starting ASP.NET.
3. Automatically sets ASP.NET process environment variables (`AgentServices__ProblemUnderstandingUrl=http://127.0.0.1:8001`).
4. Starts `dotnet run --project backend/src/AssistLK.Api`.
5. Cleanly stops both processes upon `Ctrl+C` with no orphaned processes.

Optional cleanup helper if a terminal was closed without `Ctrl+C`:
```powershell
.\scripts\stop-c1-dev.ps1
```

---

### 9. Component Boundaries & Ownership

The system is strictly partitioned across 4 team members:

| Member | Component | Domain Scope | Primary Agent |
|---|---|---|---|
| **Member 1** | **Component 1** | Service Request & Problem Understanding | `ProblemUnderstandingAgent` |
| **Member 2** | **Component 2** | Provider Management & Intelligent Matching | `ProviderMatchingAgent` |
| **Member 3** | **Component 3** | Quotation, Booking & Service Coordination | `ServiceCoordinationAgent` |
| **Member 4** | **Component 4** | Service Tracking, Completion & Feedback | `ValidationSafetyAgent` |

Components communicate via public Application service contracts or documented HTTP endpoints. Direct cross-component table manipulation is prohibited.

- 📖 **Component Boundaries:** [docs/architecture/component-boundaries.md](docs/architecture/component-boundaries.md)
- 📖 **Ownership Matrix:** [docs/COMPONENT_OWNERSHIP.md](docs/COMPONENT_OWNERSHIP.md)

---

### 10. Testing Strategy & Quality Gates

Automated tests run on every commit:
- **Backend (.NET):** Uses real PostgreSQL test databases (`assistlk_test_integration` and `assistlk_test_api`) with destructive safety guards protecting `assistlk_db`. Offline Gemini simulation ensures tests never require live API keys.
  ```bash
  dotnet test backend/AssistLK.sln
  ```
- **Web (React):** Vitest + React Testing Library + ESLint.
  ```bash
  cd web && npm test && npm run lint && npm run build
  ```
- **Mobile (Flutter):**
  ```bash
  cd mobile && flutter test
  ```

- 📖 **Testing & QA Guide:** [docs/development/testing-guide.md](docs/development/testing-guide.md)

---

### 11. Git Workflow & Branching

- **Trunk Branch:** `develop`
- **Feature Branch Pattern:** `feature/component-X-short-description`
- **Conventional Commits:** `feat(c2): provider distance scoring`, `test(c3): quotation validation`
- Always pull latest `develop` and run local test suites before opening a Pull Request into `develop`.

- 📖 **Team Git Workflow:** [docs/development/git-workflow.md](docs/development/git-workflow.md)

---

### 12. Where API Contracts Live

Major component handoffs are documented and versioned:
1. **Component 1 → Component 2:** `ReadyForMatching` snapshot (`GET /api/service-requests/{id}`) `[EXISTING]`
2. **Component 2 → Component 3:** Provider match notification `[PLANNED]`
3. **Component 3 → Component 4:** Booking confirmed handoff `[PLANNED]`

- 📖 **Component Integration Contracts:** [docs/api/component-contracts.md](docs/api/component-contracts.md)
- 📖 **API Conventions:** [docs/API_CONVENTIONS.md](docs/API_CONVENTIONS.md)

---

## Security & Secrets Policy

Never commit secrets to this repository:
- ❌ Do not commit `GOOGLE_API_KEY` or Gemini credentials.
- ❌ Do not commit database passwords or production connection strings.
- ❌ Do not commit `.env` files (use `.env.example`).
- ❌ Use .NET User Secrets in development.
