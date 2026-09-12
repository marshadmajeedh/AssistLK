# AssistLK Project Cleanup Audit Report

> **Historical / Superseded:** This document records an earlier implementation state. Its original conclusions and verification results are retained as historical evidence, not current instructions. Component 1 is now Python-only. See [current Agentic AI architecture](../agent-services/README.md). References repaired during documentation consolidation point to replacement explanations, not the original historical implementation.

**Date:** September 2026  
**Auditor:** AssistLK Engineering Audit Agent  
**Repository:** `AssistLK` (`SE3090_A1`)  
**Scope:** Static codebase cleanup audit across backend, frontend, generated artifacts, and documentation.  
**Mode:** **Audit Only** — No source code modifications, logic alterations, migrations, or file deletions were performed.

---

## Executive Summary

This cleanup audit was executed to reduce developer confusion, streamline repository navigation, and provide clear architectural clarity for the team across all four components. 

The audit identified several categories of friction:
1. **Leftover Template & Demo Files:** Initial boilerplate files (such as `Class1.cs`, Vite starter stylesheets, and mock demo agents) remain in the active codebase alongside production code.
2. **Architectural Layout Inconsistency:** An architectural collision exists between the project's primary **Layered Clean Architecture** (`Api/Controllers`, `Application/Services`, `Agents/Agents`, `Infrastructure/Repositories`) and an earlier proposed **Feature-Folder Architecture** (`Api/Features/*`), leaving orphaned, empty `.gitkeep` directories.
3. **Unused DTOs & Entity Leakage:** Several DTOs exist in `AssistLK.Api/DTOs` that are never referenced, while corresponding controllers either return anonymous types or leak raw EF Core database entities directly to the client.
4. **Untracked Structure Dumps:** Large directory listing text dumps (`backend_structure.txt`, `frontend_structure.txt`) reside untracked in the root directory.
5. **Documentation Fragmentation:** Over 10 early micro-stubs and duplicate design documents in `docs/` conflict with authoritative system architecture documents and use diverging phase schedules. Furthermore, leftover references to an internal Python agent service (`agent-service/`) conflict with the native .NET 8 C# agent implementation in `AssistLK.Agents`.

---

## 1. Backend Cleanup Findings (`backend/src/**`)

### Classification Criteria:
- **KEEP:** Required production classes, interfaces, entities, active migrations, operational services, active controllers, and validated agent tools.
- **REVIEW:** Demo classes, old prototype experiments, unused DTOs, architectural redundancies, or placeholder folders reserved for Components 2, 3, and 4.
- **REMOVE:** Default compiler/template files, build artifacts on disk, and completely redundant placeholders where active code already exists elsewhere.

---

### 1.1 `AssistLK.Agents` Deep Dive

| File / Folder | Classification | Finding & Analysis | Recommendation |
|---|---|---|---|
| `Class1.cs` | **REMOVE** | Default empty class created by `dotnet new classlib`. Contains empty `public class Class1 {}` inside `AssistLK.Agents`. Not referenced anywhere. | Safely delete in cleanup phase. |
| `Agents/DemoProblemAgent.cs` | **REVIEW** | Early Phase 1 prototype agent returning hardcoded keyword-matched responses (`"Possible battery or ignition issue"`, `"General vehicle issue"`). Replaced in production by `ProblemUnderstandingAgent.cs`. It is still registered in `Program.cs` (lines 93 & 205) and into `AgentRegistry`. Has no unit or integration tests. | Deprecate and remove from DI once team confirms no manual demo scripts require it. |
| `Tools/DemoProviderSearchTool.cs` | **REVIEW** | Early prototype tool returning static hardcoded provider records (`"ABC Vehicle Service"`, `"City Auto Garage"`). It is registered in `Program.cs` (lines 103 & 224) into `ToolRegistry` as `"ProviderSearchTool"`. Not consumed by Component 1 workflow or any automated test. Represents an early placeholder for Component 2. | Retain under REVIEW until Component 2 (Provider Matching) implements the real provider repository search tool, then replace and deregister. |
| `Tools/LocationExtractionTool.cs` | **KEEP** | Production tool used by `ProblemUnderstandingAgent` to extract structured coordinates from request inputs. Verified with extensive integration tests (`Component1ToolTests.cs`). | Retain as core production tool. |
| `Tools/ProblemClassificationTool.cs` | **KEEP** | Production tool used by `ProblemUnderstandingAgent` for deterministic category classification. Verified with comprehensive integration tests (`Component1ToolTests.cs`). | Retain as core production tool. |
| `Tools/ServiceKnowledgeTool.cs` | **KEEP** | Production tool providing safety-aligned vehicle diagnosis possibilities without dangerous DIY advice. Fully tested (`Component1ToolTests.cs`, `Component1SafetyTests.cs`). | Retain as core production tool. |
| `Tools/ToolResult.cs` | **KEEP** | Standard return contract for all agent tools. | Retain as core abstraction. |
| `Agents/ProblemUnderstandingAgent.cs` | **KEEP** | Authoritative Component 1 AI agent coordinating LLM analysis with Google Gemini and local tools. | Retain as core agent. |
| `Services/GeminiService.cs` | **KEEP** | Production Gemini API client service handling prompt execution, JSON schema extraction, and fallback logic. | Retain as core infrastructure service. |
| `Abstractions/*` (`IAgent.cs`, `IAgentTool.cs`, `IGeminiService.cs`) | **KEEP** | Core agent contracts utilized throughout the agent framework. | Retain as core abstractions. |
| `Core/*` (`AgentContext`, `AgentOrchestrator`, `AgentRegistry`, `AgentSafetyPolicyEngine`, `ToolExecutor`, `ToolRegistry`) | **KEEP** | Foundational orchestration, registry, tool execution, and safety policy pipeline. | Retain as core agent foundation. |
| `Models/*` (`AgentResult`, `AgentSafetyRule`, `ProblemUnderstandingInput`, `ProblemUnderstandingOutput`) | **KEEP** | All models are actively used by the orchestrator, safety engine, and Component 1 agent. | Retain all models. |
| `bin/`, `obj/` | **REMOVE** | Compiled local build outputs and NuGet asset caches present on disk. | Clean via `dotnet clean` (ignored by `.gitignore`). |

---

### 1.2 `AssistLK.Api` Deep Dive

| File / Folder | Classification | Finding & Analysis | Recommendation |
|---|---|---|---|
| `Features/ServiceRequests/.gitkeep` | **REMOVE** | Empty feature folder placeholder. The actual production controller and DTOs were implemented in `Controllers/ServiceRequestsController.cs` and `DTOs/ServiceRequests/`. Having an empty feature folder creates confusion. | Delete the empty folder and `.gitkeep`. |
| `Features/Bookings/.gitkeep` | **REVIEW** | Empty folder placeholder for Component 3. | Retain if the team decides to transition to feature folders; otherwise remove if maintaining layered architecture. |
| `Features/Providers/.gitkeep` | **REVIEW** | Empty folder placeholder for Component 2. | Retain until Component 2 decides on folder organization. |
| `Features/Quotations/.gitkeep` | **REVIEW** | Empty folder placeholder for Component 3. | Retain until Component 3 decides on folder organization. |
| `Features/ServiceTracking/.gitkeep` | **REVIEW** | Empty folder placeholder for Component 4. | Retain until Component 4 decides on folder organization. |
| `Controllers/.gitkeep` | **REMOVE** | Redundant `.gitkeep`. Directory contains 5 active controllers (`AgentMonitoringController`, `AgentWorkflowController`, `AuthController`, `ServiceRequestsController`, `SystemController`). | Delete redundant `.gitkeep`. |
| `DTOs/.gitkeep` | **REMOVE** | Redundant `.gitkeep`. Directory contains multiple active DTO classes and subdirectories. | Delete redundant `.gitkeep`. |
| `Middleware/.gitkeep` | **REMOVE** | Redundant `.gitkeep`. Directory contains `ExceptionHandlingMiddleware.cs`. | Delete redundant `.gitkeep`. |
| `DTOs/AgentMetricResponseDto.cs` | **REVIEW** | **Unused DTO.** Defined with `AgentName`, `Status`, `ExecutionTimeMs`, `ToolCalls`, but never used. In `AgentMonitoringController.GetMetrics()`, the raw EF Core entity `AgentExecutionMetric` is returned directly, bypassing this DTO. | Update `AgentMonitoringController` to map to `AgentMetricResponseDto` (prevents entity model leakage), or remove if raw entity exposure was intended. |
| `DTOs/AgentWorkflowResponseDto.cs` | **REVIEW** | **Unused DTO.** Defined with `WorkflowId`, `Status`, `Result`, but never used. In `AgentWorkflowController.Execute()`, an untyped anonymous object `{ WorkflowId = workflow.Id, Result = result }` is returned instead. | Update `AgentWorkflowController` to return typed `AgentWorkflowResponseDto`, or remove if unneeded. |
| `DTOs/AgentWorkflowRequestDto.cs` | **KEEP** | Actively consumed as the request body in `AgentWorkflowController.Execute()`. | Retain. |
| `DTOs/ErrorResponse.cs` | **KEEP** | Standard error payload model consumed by `ExceptionHandlingMiddleware`. | Retain. |
| `DTOs/ServiceRequests/ProblemUnderstandingResponseDto.cs` | **KEEP** | Production API response DTO for Component 1 analysis endpoint. Verified by API security and PostgreSQL integration tests. | Retain. |
| `Controllers/AgentWorkflowController.cs` | **REVIEW** | Generic endpoint (`POST /api/agents/execute`) built for initial prototype testing. Calls `AgentExecutionService`. Does not enforce authentication or role validation. | Keep for developmental agent testing, or mark for administrative authorization hardening. |
| `Controllers/AgentMonitoringController.cs` | **REVIEW** | Directly injects and queries `AssistLKDbContext` instead of using `AgentMonitoringService` or `IAgentWorkflowDbContext`. Violates Clean Architecture layer boundaries. | Refactor to query through application service or repository in future refactoring sprint. |
| `Controllers/ServiceRequestsController.cs` | **KEEP** | Authoritative production controller for Component 1 with full role authorization and ownership enforcement. | Retain. |
| `Controllers/AuthController.cs` | **KEEP** | Authentication controller handling registration, login, and token generation. | Retain. |
| `Controllers/SystemController.cs` | **KEEP** | Health check / system status endpoint (`/api/system/info`). Tested by `SystemTests.cs`. | Retain. |
| `Authentication/JwtTokenService.cs` | **KEEP** | Production JWT token generation service implementing `IJwtTokenService`. | Retain. |
| `Middleware/ExceptionHandlingMiddleware.cs` | **KEEP** | Global exception filter converting domain/application exceptions into standardized JSON error responses. | Retain. |
| `Seed/DevelopmentDataSeeder.cs` | **KEEP** | Secure development database seeder for initial administrator bootstrap. | Retain. |

---

### 1.3 `AssistLK.Application` Deep Dive

| File / Folder | Classification | Finding & Analysis | Recommendation |
|---|---|---|---|
| `Interfaces/.gitkeep` | **REMOVE** | Redundant `.gitkeep`. Directory contains 7 active interface files. | Delete redundant `.gitkeep`. |
| **Interface Completeness Check** | **KEEP** | **All 7 interfaces are actively implemented and consumed:**<br>• `IAgentWorkflowDbContext` (Implemented by `AssistLKDbContext`, consumed by agent services)<br>• `IAuthService` (Implemented by `AuthService`, consumed by `AuthController`)<br>• `IJwtTokenService` (Implemented by `JwtTokenService`, consumed by `AuthService`)<br>• `IProblemAnalysisRepository` (Implemented by `ProblemAnalysisRepository`, consumed by `ServiceRequestService`)<br>• `IServiceRequestRepository` (Implemented by `ServiceRequestRepository`, consumed by `ServiceRequestService`)<br>• `IServiceRequestService` (Implemented by `ServiceRequestService`, consumed by `ServiceRequestsController` & `ProblemUnderstandingWorkflowService`)<br>• `IUserRepository` (Implemented by `UserRepository`, consumed by `AuthService`) | Retain all 7 interfaces. Zero unused interfaces found. |
| `Services/ServiceRequests/.gitkeep` | **REMOVE** | Empty service subfolder placeholder. `ServiceRequestService.cs` was placed directly in `AssistLK.Application/Services/`, leaving this subfolder orphaned. | Delete empty placeholder subfolder. |
| `Services/Providers/.gitkeep` | **REVIEW** | Empty folder placeholder for Component 2 provider application services. | Retain for Component 2 implementation. |
| `Services/Quotations/.gitkeep` | **REVIEW** | Empty folder placeholder for Component 3 quotation/booking application services. | Retain for Component 3 implementation. |
| `Services/ServiceTracking/.gitkeep` | **REVIEW** | Empty folder placeholder for Component 4 tracking application services. | Retain for Component 4 implementation. |
| `Validators/.gitkeep` | **REVIEW** | Empty folder placeholder for FluentValidation or custom validators. Request validation is currently handled inside domain entity factories and service private methods (`ValidateAnalysisResult`). | Retain if the team plans to adopt FluentValidation; otherwise remove if domain validation is preferred. |
| `Services/AgentExecutionService.cs` | **REVIEW** | Prototype dynamic workflow coordinator used exclusively by `AgentWorkflowController`. Runs untyped agents by string name and records timing. In contrast, `ProblemUnderstandingWorkflowService` handles structured, production-grade agent execution with strict customer ownership and state validation. | Retain for generic agent testing, but document that Component workflows should use dedicated strongly-typed workflow services. |
| `Services/ProblemUnderstandingWorkflowService.cs` | **KEEP** | Production workflow coordinator for Component 1 connecting agent execution, state management, audit logging, and domain persistence. | Retain as core workflow service. |
| `Services/ServiceRequestService.cs` | **KEEP** | Core domain application service managing service request lifecycles, analysis application, and matching handoff contract. | Retain as core domain service. |
| `Services/Auth/AuthService.cs` | **KEEP** | Authentication business logic handling credential verification, hashing, and token issuance. | Retain. |
| `Services/Agent*Service.cs` (`Context`, `Memory`, `Monitoring`, `Safety`, `Workflow`) | **KEEP** | Foundational workflow, memory persistence, safety policy, and audit services used by `ProblemUnderstandingWorkflowService`. | Retain all foundation services. |
| `ServiceRequests/DTOs/*` (7 DTO files) | **KEEP** | Complete set of request/response DTOs for Component 1, including handoff DTO `ServiceRequestForMatchingResponse.cs` for Component 2. | Retain all DTOs. |
| `Auth/DTOs/*` (5 DTO files) | **KEEP** | All authentication request and response DTOs are actively used. | Retain all DTOs. |
| `Common/Exceptions/ConflictException.cs` | **KEEP** | Custom domain exception mapped to HTTP 409 Conflict. | Retain. |

---

### 1.4 `AssistLK.Infrastructure` Deep Dive

| File / Folder | Classification | Finding & Analysis | Recommendation |
|---|---|---|---|
| `ExternalServices/.gitkeep` | **REVIEW** | Empty placeholder folder. No external service clients (e.g. Google Maps Geocoding API, SMS gateway, Cloud Storage) have been implemented yet. | Retain for Component 2/3/4 integrations (e.g., Maps, Payment gateways). |
| `Data/.gitkeep` | **REMOVE** | Redundant `.gitkeep`. Directory is actively populated with `AssistLKDbContext.cs` and `Migrations/`. | Delete redundant `.gitkeep`. |
| `Repositories/.gitkeep` | **REMOVE** | Redundant `.gitkeep`. Directory contains 3 active repositories. | Delete redundant `.gitkeep`. |
| `Data/AssistLKDbContext.cs` | **KEEP** | Central EF Core DbContext configuring domain entities, indexes, relationships, and agent foundation tables. | Retain. |
| `Data/Migrations/*` (6 migrations + ModelSnapshot) | **KEEP** | All 6 EF Core migrations are active and required to reproduce the PostgreSQL schema: `InitialDatabaseFoundation`, `AddAgentWorkflowFoundation`, `AddAgentMemory`, `AddAgentSafetyActions`, `AddAgentExecutionMetrics`, `AddServiceRequestAndProblemAnalysis`. | Retain all migrations. Never delete active migrations. |
| `Repositories/*` (`ProblemAnalysisRepository`, `ServiceRequestRepository`, `UserRepository`) | **KEEP** | Production data access implementations. | Retain. |
| `DependencyInjection.cs` | **KEEP** | Extension method registering DbContext, repositories, and interfaces into ASP.NET Core DI. | Retain. |

---

### 1.5 `AssistLK.Domain` Deep Dive

| File / Folder | Classification | Finding & Analysis | Recommendation |
|---|---|---|---|
| `Entities/.gitkeep` | **REMOVE** | Redundant `.gitkeep` in a fully populated directory (11 entity classes). | Delete redundant `.gitkeep`. |
| `Enums/.gitkeep` | **REMOVE** | Redundant `.gitkeep` in a fully populated directory (5 enum classes). | Delete redundant `.gitkeep`. |
| `Entities/*` (11 entities) | **KEEP** | Core domain entities (`ServiceRequest`, `ProblemAnalysis`, `User`, `AgentWorkflow`, `AgentExecution`, `AgentExecutionMetric`, `AgentApproval`, `AgentAuditLog`, `AgentMemory`, `AgentAction`, `BaseEntity`). All mapped in EF Core. | Retain all entities. |
| `Enums/*` (5 enums) | **KEEP** | Core domain enums (`ServiceRequestStatus`, `ServiceRequestUrgency`, `UserRole`, `AgentWorkflowStatus`, `AgentApprovalStatus`). | Retain all enums. |

---

## 2. Frontend Cleanup Findings (`web/src/**`)

### Classification Criteria:
- **Keep:** Component 1 service request implementation, shared components, authentication, routing, theme system, and tests.
- **Review:** Empty feature folders, unused assets, and placeholder pages maintained for navigation stability.
- **Remove:** Unused Vite template files, unused starter assets, and redundant `.gitkeep` files in populated directories.

---

### 2.1 Detailed Findings

| File / Folder | Classification | Finding & Analysis | Recommendation |
|---|---|---|---|
| `web/src/App.css` | **REMOVE** | **Leftover Vite starter CSS** (185 lines). Defines `.counter`, `.hero`, `.ticks`, `#next-steps`, `#docs`, and media queries from Vite default template. It is **never imported** anywhere in `web/` (`App.jsx` only imports `AppRouter`). | Safely delete. |
| `web/src/assets/react.svg` | **REMOVE** | Default Vite React starter logo. Unused in any JSX or CSS file. | Safely delete. |
| `web/src/assets/vite.svg` | **REMOVE** | Default Vite starter logo. Unused in any JSX or CSS file. | Safely delete. |
| `web/src/assets/hero.png` | **REVIEW** | Unused image asset located in `web/src/assets/`. Not referenced in any component or style. Might have been intended for a landing page. | Review with frontend owner; remove if unneeded. |
| `web/public/icons.svg` | **REVIEW** | SVG sprite from Vite starter containing symbols for Bluesky, Discord, GitHub, and documentation icons. 0 references across the entire frontend. | Review and remove during asset cleanup. |
| `web/src/features/serviceRequests/.gitkeep` | **REMOVE** | Redundant `.gitkeep`. The folder contains 4 components, 4 pages, 1 hook, 1 service, 2 utilities, and 9 test suites. | Safely delete. |
| `web/src/shared/.gitkeep` | **REMOVE** | Redundant `.gitkeep`. Directory contains `api/`, `auth/`, `components/`, `layouts/`, and `theme/`. | Safely delete. |
| `web/src/features/aiWorkflows/.gitkeep` | **REVIEW** | Empty feature folder for AI Workflow monitoring UI. | Retain placeholder for admin workflow visualizer. |
| `web/src/features/providers/.gitkeep` | **REVIEW** | Empty feature folder for Component 2 (Provider Matching). | Retain placeholder for Component 2. |
| `web/src/features/quotations/.gitkeep` | **REVIEW** | Empty feature folder for Component 3 (Quotation & Booking). | Retain placeholder for Component 3. |
| `web/src/features/tracking/.gitkeep` | **REVIEW** | Empty feature folder for Component 4 (Service Tracking). | Retain placeholder for Component 4. |
| `web/src/shared/components/PlaceholderPage.jsx` | **REVIEW / KEEP** | Generic placeholder page component used across 7 routes in `AppRouter.jsx` (`/dashboard`, `/admin/service-requests`, `/providers`, `/quotations`, `/service-tracking`, `/ai-workflows`, `/unauthorized`, `*`). Essential to prevent route crashes in `AdminLayout`. | Keep until replaced by real component feature pages. |
| `web/src/features/serviceRequests/**` | **KEEP** | Full Component 1 implementation (components, pages, services, hooks, utilities). | Retain. |
| `web/src/shared/components/**` | **KEEP** | Production design system components (`AppButton`, `AppCard`, `AppInput`, `AppTextArea`, `ErrorMessage`, `LoadingSpinner`, `StatusBadge`). | Retain. |
| `web/src/shared/theme/**` | **KEEP** | Curated theme tokens (`colors.js`, `components.js`, `radius.js`, `spacing.js`, `typography.js`, `theme.js`). | Retain. |
| `web/src/shared/auth/**` & `api/**` | **KEEP** | Zustand auth store, `ProtectedRoute`, and Axios client with token interceptors. | Retain. |
| `web/src/shared/layouts/**` | **KEEP** | `AdminLayout.jsx` and `CustomerLayout.jsx`. | Retain. |
| `web/src/app/router/**` | **KEEP** | `AppRouter.jsx` and its associated router test suite. | Retain. |
| `web/src/**/__tests__/**` (16 test suites) | **KEEP** | Vitest and React Testing Library tests for all components, pages, utils, layouts, and router. | Retain all tests without modification. |

---

## 3. Generated Files & Build Artifacts

### 3.1 Files That Should Never Be Committed

| Path / Pattern | Current Status | Description | Action Required |
|---|---|---|---|
| `backend_structure.txt` | **Untracked** (73 KB) in repo root | Temporary directory tree text dump generated via PowerShell `tree /f`. Not needed in version control. | Add to `.gitignore` and delete file. |
| `frontend_structure.txt` | **Untracked** (6.5 KB) in repo root | Temporary directory tree text dump generated via PowerShell `tree /f`. Not needed in version control. | Add to `.gitignore` and delete file. |
| `backend/**/bin/` | Ignored by `.gitignore`, present on disk | Compiled .NET binaries (`.dll`, `.pdb`, `.deps.json`) across all 7 backend projects. | Run `dotnet clean` to purge local disk. |
| `backend/**/obj/` | Ignored by `.gitignore`, present on disk | Intermediate compiler build artifacts and NuGet package cache files. | Delete obj folders during build cleanup. |
| `web/dist/` | Ignored by `.gitignore`, present on disk | Vite compiled frontend production bundle. | Clean before distribution. |
| `web/node_modules/` | Ignored by `.gitignore`, present on disk | Installed npm dependencies. | Keep on disk for development; correctly ignored. |
| `mobile/build/`, `mobile/.dart_tool/` | Ignored by `.gitignore`, present on disk | Flutter build output and package tool caches. | Clean via `flutter clean`. |
| `agent-service/` | **Tracked** in git (only `.gitkeep` files) | Scaffold folder for an unused Python agent service (`agents/`, `orchestration/`, `schemas/`, `tests/`, `tools/`). The actual agents are built in C# (.NET 8). | Review with team: remove if Python is not used to eliminate architectural ambiguity. |

---

### 3.2 `.gitignore` Health Audit & Recommended Updates

The existing `.gitignore` covers standard .NET, Node, Flutter, and Python ignores. However, the audit revealed a few gaps:

```diff
  # --- Existing ignores working correctly ---
  **/bin/
  **/obj/
  web/node_modules/
  web/dist/
  .env
  appsettings.Development.json

+ # --- Recommended additions to .gitignore ---
+ # Temporary directory and structure dumps
+ *_structure.txt
+ *structure.txt
+ *.txt.tmp
+ 
+ # Test result artifacts
+ *.trx
+ TestResults/
+ 
+ # Local IDE / Editor caches
+ .csharp/
+ .dotnet/
+ *.DotSettings.user
```

---

## 4. Documentation Cleanup (`docs/**`)

The `docs/` folder contains **35 markdown files**. While the technical content is high in specific documents, there is significant fragmentation, duplication, and conflicting architectural guidance.

### 4.1 Outdated Phase Documents

| Document | Issues Identified | Resolution |
|---|---|---|
| `docs/DEVELOPMENT_PHASES.md` | Contains an obsolete 8-phase calendar schedule (Phase 0 to Phase 7 spanning August 10 to September 30, 2026). It directly contradicts the authoritative **Integration Roadmap** in `docs/development/integration-roadmap.md` which uses a Component-based phase structure (Phase 6 Foundation → Phase 7 Component Slices [7A–7D] → Phase 8 Integration → Phase 9 System Testing). | **Deprecate / Archive.** Replace any links with `docs/development/integration-roadmap.md`. |

---

### 4.2 Duplicate Documents & Micro-Stubs

| Redundant / Stub Document | Authoritative Document | Analysis |
|---|---|---|
| `docs/ARCHITECTURE.md` (23 lines) | `docs/architecture/system-architecture.md` (231 lines) | `ARCHITECTURE.md` is an initial 23-line stub that still references a hypothetical "Python agent service". `docs/architecture/system-architecture.md` is the comprehensive, authoritative design document. | Consolidate into `docs/architecture/system-architecture.md` and remove root stub. |
| `docs/agent-overview.md` (74 lines) | `docs/architecture/agent-foundation.md` (31 lines) & `docs/architecture/system-architecture.md` | Duplicate high-level summary of the Agent Foundation. | Merge unique details into `agent-foundation.md` and remove stub. |
| `docs/agent-workflow.md` (40 lines)<br>`docs/AI_WORKFLOW.md` (48 lines) | `docs/architecture/workflow-overview.md` (199 lines) | Both files provide minimal text diagrams of workflow transitions that are fully articulated with complete sequence steps in `docs/architecture/workflow-overview.md`. | Archive or delete both stubs; maintain `workflow-overview.md` as single source of truth. |
| `docs/agent-development-guide.md` (44 lines)<br>`docs/tool-development-guide.md` (25 lines)<br>`docs/component-agent-integration.md` (32 lines) | `docs/development/component-development-rules.md` (299 lines) | These three micro-stubs in the `docs/` root provide fragments of instructions that are already comprehensively specified in `docs/development/component-development-rules.md`. | Remove micro-stubs; link developers directly to `docs/development/component-development-rules.md`. |
| `docs/memory-context.md` (41 lines)<br>`docs/monitoring-evaluation.md` (29 lines)<br>`docs/safety-policy.md` (37 lines) | `docs/architecture/agent-foundation.md`<br>`docs/development/component-development-rules.md`<br>`docs/ai-context/project-context.md` | Extremely brief stubs (under 40 lines each) duplicating subsections of the primary architecture and rule documents. | Consolidate into `docs/architecture/agent-foundation.md` or move to a dedicated `docs/architecture/agents/` folder. |

---

### 4.3 Conflicting Architecture Explanations

1. **Clean Architecture vs Feature-Folder Layout:**
   - In `docs/development/component-development-rules.md` (Section 3), developers are told to place Controllers, DTOs, Agents, Services, Tools, and Validators together inside `AssistLK.Api/Features/<ComponentName>/`.
   - In contrast, `docs/architecture/system-architecture.md` specifies a **Clean Architecture** organized by layer projects: `AssistLK.Api` (Controllers), `AssistLK.Application` (Services, Interfaces, DTOs), `AssistLK.Agents` (Agents, Tools), `AssistLK.Domain` (Entities), and `AssistLK.Infrastructure` (DbContext, Repositories).
   - **The Code Reality:** The codebase (and Component 1 implementation) follows Clean Architecture. However, developers created empty folders in `AssistLK.Api/Features/` to satisfy the conflicting rule document.
   - **Recommendation:** Update `docs/development/component-development-rules.md` to match the active Clean Architecture project structure and remove `AssistLK.Api/Features/`.

2. **Python LangGraph vs .NET 8 C# Agents:**
   - `docs/ARCHITECTURE.md`, `agent-service/README.md`, and sections of the root `README.md` describe an internal Python/LangGraph service.
   - The entire agent engine, Google Gemini integration, tool execution, safety engine, and Component 1 agent are implemented in C# (.NET 8) in `AssistLK.Agents`.
   - **Recommendation:** Clarify in documentation that the Agent Foundation is implemented natively in .NET 8 C# (`AssistLK.Agents`). If Python is not used for future components, mark `agent-service/` as deprecated or remove it.

3. **Inconsistent File Naming Conventions:**
   - 9 uppercase screaming-snake files (`ADR_TEMPLATE.md`, `AI_USAGE_LOG_TEMPLATE.md`, `AI_WORKFLOW.md`, `API_CONVENTIONS.md`, `ARCHITECTURE.md`, `COMPONENT_OWNERSHIP.md`, `DESIGN_SYSTEM.md`, `DEVELOPMENT_PHASES.md`, `GIT_WORKFLOW.md`) reside alongside 10 kebab-case markdown files (`agent-development-guide.md`, `agent-overview.md`, etc.).
   - **Recommendation:** Standardize all documentation file names to `kebab-case.md`.

---

### 4.4 Recommended Final Documentation Structure

To provide an intuitive, single-source-of-truth documentation layout, the recommended target structure is:

```text
docs/
├── README.md                                  # Documentation entrypoint & index
├── architecture/
│   ├── system-architecture.md                 # Complete system architecture
│   ├── agent-foundation.md                    # Agent engine, memory, safety & tools
│   └── workflow-overview.md                   # End-to-end service request workflows
├── development/
│   ├── integration-roadmap.md                 # Project timeline & component dependencies
│   ├── component-development-rules.md         # Rules for Component 1, 2, 3, 4 developers
│   ├── api-conventions.md                     # REST API conventions & route standards
│   ├── database-ownership.md                  # Entity ownership & EF Core migration rules
│   ├── environment-setup.md                   # Local development setup (.NET, React, Postgres)
│   └── git-workflow.md                        # Branching, commits, PR & CI policies
├── components/
│   ├── component-1-problem-understanding/     # Requirements, inspection & verification logs
│   ├── component-2-provider-matching/         # Requirements & specification
│   ├── component-3-quotation-booking/         # Requirements & specification
│   └── component-4-service-tracking/          # Requirements & specification
├── design/
│   └── design-system.md                       # UI design system, color palette & typography
├── ai-context/
│   └── project-context.md                     # AI agent prompt context & guidelines
└── templates/
    ├── adr-template.md                        # Architecture Decision Record template
    └── ai-usage-log-template.md               # AI tool assistance logging template
```

---

## 5. Prioritized Cleanup Action Plan (For Subsequent Execution)

When the team is ready to execute cleanup changes, the following phased sequence is recommended:

### Phase 1: Safe Removals (Zero Logic or Risk Impact)
1. Delete untracked structure dumps: `backend_structure.txt`, `frontend_structure.txt`.
2. Delete default template file: `backend/src/AssistLK.Agents/Class1.cs`.
3. Delete unused Vite starter styles: `web/src/App.css`.
4. Delete unused starter assets: `web/src/assets/react.svg`, `web/src/assets/vite.svg`.
5. Remove redundant `.gitkeep` files in populated directories:
   - `backend/src/AssistLK.Api/Controllers/.gitkeep`
   - `backend/src/AssistLK.Api/DTOs/.gitkeep`
   - `backend/src/AssistLK.Api/Middleware/.gitkeep`
   - `backend/src/AssistLK.Api/Features/ServiceRequests/.gitkeep`
   - `backend/src/AssistLK.Application/Interfaces/.gitkeep`
   - `backend/src/AssistLK.Application/Services/ServiceRequests/.gitkeep`
   - `backend/src/AssistLK.Domain/Entities/.gitkeep`
   - `backend/src/AssistLK.Domain/Enums/.gitkeep`
   - `backend/src/AssistLK.Infrastructure/Data/.gitkeep`
   - `backend/src/AssistLK.Infrastructure/Repositories/.gitkeep`
   - `web/src/features/serviceRequests/.gitkeep`
   - `web/src/shared/.gitkeep`

### Phase 2: DTO & Controller Alignment (Low Risk)
1. In `AgentMonitoringController.cs`, map results to `AgentMetricResponseDto` instead of exposing raw `AgentExecutionMetric` entities.
2. In `AgentWorkflowController.cs`, return `AgentWorkflowResponseDto` rather than an untyped anonymous object.
3. Decide whether to retain `DemoProblemAgent.cs` and `DemoProviderSearchTool.cs` as demo mocks or deregister them from `Program.cs`.

### Phase 3: Documentation Reorganization (Medium Effort)
1. Reorganize `docs/` into the proposed folder hierarchy (`architecture/`, `development/`, `design/`, `components/`, `templates/`).
2. Remove the 8 redundant micro-stubs in `docs/` root.
3. Update `docs/development/component-development-rules.md` to reflect Clean Architecture rather than feature folders.
4. Archive `docs/DEVELOPMENT_PHASES.md` in favor of `docs/development/integration-roadmap.md`.
5. Add `*_structure.txt` to `.gitignore`.
