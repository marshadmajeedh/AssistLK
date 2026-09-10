# AssistLK Technical Documentation Hub

Welcome to the official technical documentation for **AssistLK** (`SE3090_A1`), an Agentic AI-assisted emergency and skilled service coordination platform built with **ASP.NET Core (.NET 8)**, **React (Vite)**, **Flutter**, **PostgreSQL**, and **Google Gemini**.

---

## Developer Onboarding Sitemap (The 12 Critical Areas)

For any new teammate joining the team (Members 1, 2, 3, or 4), here is the fast-track guide to understanding and building features in AssistLK:

### 1. What AssistLK Is
- Multi-component platform connecting customers facing urgent home/vehicle problems (breakdowns, plumbing, electrical, appliance) with verified skilled service providers.
- Coordinates problem understanding, provider matching, quotation/booking, and live service tracking.
- See: **[System Architecture](architecture/system-architecture.md)** \| **[Workflow Overview](architecture/workflow-overview.md)**

### 2. Repository Structure
- Clean, segregated repository layout:
  - `backend/`: ASP.NET Core Clean Architecture (.NET 8 Web API, Application, Domain, Infrastructure, Agents)
  - `web/`: React 18 + Vite customer and staff portal
  - `mobile/`: Flutter cross-platform mobile app (Customer & Provider)
  - `agent-services/`: Canonical home for optional out-of-process Python agent microservices
  - `docs/`: Technical specifications, architecture docs, and development guides

### 3. How to Run Backend
- Prerequisites: .NET 8 SDK, PostgreSQL 15+
- Start API:
  ```bash
  dotnet run --project backend/src/AssistLK.Api
  ```
- See: **[Development Environment Setup](development/development-environment-setup.md)**

### 4. How to Run React
- Prerequisites: Node.js 18+
- Start Web Dev Server:
  ```bash
  cd web && npm install && npm run dev
  ```
- See: **[React Development Guide](development/react-development-guide.md)**

### 5. How to Run Flutter
- Prerequisites: Flutter SDK 3.19+
- Start Mobile App:
  ```bash
  cd mobile && flutter pub get && flutter run
  ```
- See: **[Flutter Development Guide](development/flutter-development-guide.md)**

### 6. How Agent Architecture Works
- Dual-model support: Native .NET agents (in-process) and optional Python agent microservices (out-of-process).
- Shared agent foundation: Orchestration, memory, safety policies, tool execution, and execution monitoring.
- Agents **never** directly access `AssistLKDbContext` or EF repositories.
- See: **[Agent Foundation](architecture/agent-foundation.md)**

### 7. How to Build a .NET Agent
- 10-step step-by-step guide for implementing native C# agents (`IAgent`, `IGeminiService`, `ToolExecutor`).
- See: **[How to Create a .NET Agent](development/how-to-create-dotnet-agent.md)**

### 8. How to Build & Connect a Python Agent
- Out-of-process FastAPI agent service using LangChain, LangGraph, or ML libraries.
- Standardized provider-neutral JSON wire contract, timeouts, retries, and fallback behaviors.
- See: **[External Python Agent Service](architecture/external-python-agent-service.md)** \| **[External Agent Contract](architecture/external-agent-contract.md)**

### 9. Component Boundaries & Ownership
- Comprehensive ownership matrix mapping Members 1–4 to database entities, backend services, agents, React views, and Flutter screens.
- Strict rules for cross-component interactions and handoff boundaries.
- See: **[Component Boundaries](architecture/component-boundaries.md)** \| **[Component Ownership Matrix](COMPONENT_OWNERSHIP.md)**

### 10. Testing Strategy & Quality Gates
- Real PostgreSQL test databases (`assistlk_test_integration`, `assistlk_test_api`) with destructive safety guards.
- Offline Gemini simulation & LLM fakes (automated tests never call live Gemini API).
- Vitest for React, Flutter test suite, and Pytest for Python agents.
- See: **[Testing & Quality Assurance Guide](development/testing-guide.md)**

### 11. Git Workflow & Branching
- Trunk-based branching from `develop`: `feature/component-X-...`
- Conventional commit conventions, branch synchronization, and PR checklists.
- See: **[Team Git Workflow](development/git-workflow.md)**

### 12. Where API Contracts Live
- Complete specifications for cross-component handoffs (C1 → C2 ReadyForMatching, C2 → C3 Provider Matching, C3 → C4 Booking Confirmed).
- Clear distinction between `[EXISTING]` and `[PLANNED / FUTURE CONTRACT]`.
- See: **[Component Integration Contracts](api/component-contracts.md)** \| **[API Conventions](API_CONVENTIONS.md)**

---

## Detailed Directory Index

| Category | Authoritative Documents |
|---|---|
| **Architecture** | • [System Architecture](architecture/system-architecture.md)<br>• [Component Boundaries](architecture/component-boundaries.md)<br>• [Agent Foundation](architecture/agent-foundation.md)<br>• [External Python Agent Service](architecture/external-python-agent-service.md)<br>• [External Agent Contract](architecture/external-agent-contract.md)<br>• [Workflow Overview](architecture/workflow-overview.md) |
| **Development Guides** | • [How to Create a .NET Agent](development/how-to-create-dotnet-agent.md)<br>• [React Development Guide](development/react-development-guide.md)<br>• [Flutter Development Guide](development/flutter-development-guide.md)<br>• [Component Development Rules](development/component-development-rules.md)<br>• [Database Ownership](development/database-ownership.md)<br>• [Testing Guide](development/testing-guide.md)<br>• [Git Workflow](development/git-workflow.md)<br>• [Environment Setup](development/development-environment-setup.md) |
| **Contracts & APIs** | • [Component Integration Contracts](api/component-contracts.md)<br>• [External Agent Contract](architecture/external-agent-contract.md)<br>• [API Conventions](API_CONVENTIONS.md) |
| **UI & Design** | • [Design System & UI Tokens](DESIGN_SYSTEM.md) |
| **Audit & Governance** | • [Team Architecture Audit](cleanup/team-architecture-audit.md)<br>• [Component Ownership](COMPONENT_OWNERSHIP.md)<br>• [ADR Template](ADR_TEMPLATE.md)<br>• [AI Usage Log Template](AI_USAGE_LOG_TEMPLATE.md) |
