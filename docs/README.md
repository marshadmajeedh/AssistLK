# AssistLK System Documentation

Welcome to the official technical documentation for **AssistLK** (`SE3090_A1`), an Agentic AI-assisted emergency and skilled service coordination platform built with **React**, **Flutter**, **ASP.NET Core (.NET 8)**, **PostgreSQL**, and **Google Gemini**.

---

## 1. Architecture Overview (Single Source of Truth)

AssistLK is implemented using **Clean Architecture** combined with a native .NET 8 Agentic AI foundation. All interactions from Web and Mobile clients are mediated through the ASP.NET Core API.

```text
AssistLK Backend (.NET 8)
├── AssistLK.Api             # HTTP Controllers, JWT Authentication, Global Middleware, API DTOs
├── AssistLK.Application     # Application Services, Workflow Orchestrators, Interfaces, DTOs
├── AssistLK.Agents          # AI Agents, Agent Tools, Gemini LLM Service, Safety Engine, Memory
├── AssistLK.Domain          # Domain Entities, Value Objects, Domain Enums
└── AssistLK.Infrastructure  # PostgreSQL DbContext, EF Core Migrations, Repositories
```

### Architecture Documents:
- **[System Architecture](file:///g:/SE3090_A1/AssistLK/docs/architecture/system-architecture.md):** Complete technical architecture, 5-layer Clean Architecture layout, data flows, security boundaries, and scalability considerations.
- **[Agent Foundation](file:///g:/SE3090_A1/AssistLK/docs/architecture/agent-foundation.md):** Native .NET 8 agent framework, `AgentOrchestrator`, `ToolExecutor`, `GeminiService` (Google Gemini 1.5 Flash), `AgentSafetyPolicyEngine`, and shared PostgreSQL workflow memory.
- **[External Python Agent Service](file:///g:/SE3090_A1/AssistLK/docs/architecture/external-python-agent-service.md):** Architectural design, communication contracts, safety rules, and .NET adapter integration for external Python agent services (FastAPI, LangChain, LangGraph).
- **[Workflow Overview](file:///g:/SE3090_A1/AssistLK/docs/architecture/workflow-overview.md):** End-to-end service request lifecycle across all 4 components (Problem Understanding → Provider Matching → Quotation & Booking → Service Tracking).

---

## 2. Component Specifications

The system is partitioned into 4 specialized vertical components with clear boundaries:

| Component | Technical Owner | Agent | Primary Specification |
|---|---|---|---|
| **Component 1** | Member 1 | Problem Understanding Agent | [Component 1 README](file:///g:/SE3090_A1/AssistLK/docs/components/component-1-problem-understanding/README.md) \| [Requirements](file:///g:/SE3090_A1/AssistLK/docs/components/component-1-problem-understanding/requirements.md) |
| **Component 2** | Member 2 | Provider Matching Agent | [Component 2 README](file:///g:/SE3090_A1/AssistLK/docs/components/component-2-provider-matching/README.md) \| [Requirements](file:///g:/SE3090_A1/AssistLK/docs/components/component-2-provider-matching/requirements.md) |
| **Component 3** | Member 3 | Service Coordination Agent | [Component 3 README](file:///g:/SE3090_A1/AssistLK/docs/components/component-3-quotation-booking/README.md) \| [Requirements](file:///g:/SE3090_A1/AssistLK/docs/components/component-3-quotation-booking/requirements.md) |
| **Component 4** | Member 4 | Validation & Safety Agent | [Component 4 README](file:///g:/SE3090_A1/AssistLK/docs/components/component-4-service-tracking/README.md) \| [Requirements](file:///g:/SE3090_A1/AssistLK/docs/components/component-4-service-tracking/requirements.md) |

---

## 3. Development Guidelines & Standards

- **[How to Create an Agent](file:///g:/SE3090_A1/AssistLK/docs/development/how-to-create-agent.md):** Step-by-step developer guide for engineering new LLM-powered agents with Google Gemini, ToolExecutor, and Clean Architecture boundaries.
- **[Component Development Rules](file:///g:/SE3090_A1/AssistLK/docs/development/component-development-rules.md):** The unified rule book for all component developers. Governs Clean Architecture layer placement, agent creation, tool contracts, memory rules, safety approval workflows, and PR requirements.
- **[Integration Roadmap](file:///g:/SE3090_A1/AssistLK/docs/development/integration-roadmap.md):** Authoritative component development order, handoff contracts, integration strategy, and delivery milestones.
- **[Database Ownership](file:///g:/SE3090_A1/AssistLK/docs/development/database-ownership.md):** Entity ownership boundaries, schema isolation, and EF Core migration policies.
- **[Environment Setup](file:///g:/SE3090_A1/AssistLK/docs/development/development-environment-setup.md):** Local prerequisites, connection strings, PostgreSQL configuration, and development startup guides.
- **[API Conventions](file:///g:/SE3090_A1/AssistLK/docs/API_CONVENTIONS.md):** Standardized route namespaces, HTTP verbs, status code mapping, and DTO usage.
- **[Git Workflow](file:///g:/SE3090_A1/AssistLK/docs/GIT_WORKFLOW.md):** Branching strategies, conventional commit formats (`type(component): description`), pull request validation, and merge policies.

---

## 4. UI Design System & AI Context

- **[Design System](file:///g:/SE3090_A1/AssistLK/docs/DESIGN_SYSTEM.md):** Curated color tokens, accessibility rules, typography, component styling, and responsive layout guidelines for Web (React) and Mobile (Flutter).
- **[Project Context for AI](file:///g:/SE3090_A1/AssistLK/docs/ai-context/project-context.md):** Standardized context file for LLM coding assistants and prompt engineering.

---

## 5. Governance & Templates

- **[Component Ownership Matrix](file:///g:/SE3090_A1/AssistLK/docs/COMPONENT_OWNERSHIP.md):** Responsibility matrix mapping team members to database entities, backend services, agents, frontend views, and tests.
- **[Architecture Decision Record (ADR) Template](file:///g:/SE3090_A1/AssistLK/docs/ADR_TEMPLATE.md):** Template for recording significant architectural choices.
- **[AI Usage Log Template](file:///g:/SE3090_A1/AssistLK/docs/AI_USAGE_LOG_TEMPLATE.md):** Template for tracking agentic AI tool prompts, outputs, and validation evidence.

---

## 6. Audit & Cleanup Reports

- **[Classification Logic Audit & Removal Report](file:///g:/SE3090_A1/AssistLK/docs/cleanup/classification-removal-report.md):** Report on the audit and elimination of obsolete deterministic/keyword-matching classification logic and duplicate agents.
- **[Classification Agent Removal Report](file:///g:/SE3090_A1/AssistLK/docs/cleanup/classification-agent-removal.md):** Specific execution log of `DemoProblemAgent` removal and Program.cs registration cleanup.
- **[Project Cleanup Audit Report](file:///g:/SE3090_A1/AssistLK/docs/project-cleanup-audit.md):** Comprehensive static audit identifying unused files, generated dumps, and documentation conflicts.
- **[Project Cleanup Change Log](file:///g:/SE3090_A1/AssistLK/docs/project-cleanup-change-log.md):** Phase 1 execution log recording 17 deleted files, .gitignore updates, and automated test validations.
- **[Documentation Cleanup Report](file:///g:/SE3090_A1/AssistLK/docs/documentation-cleanup-report.md):** Detailed report of the single-source-of-truth documentation realignment.

---

## 7. Archived Documentation

Early draft documents, obsolete phase calendars, and micro-stubs that have been superseded by the authoritative guides above have been relocated to the **[docs/archive/](file:///g:/SE3090_A1/AssistLK/docs/archive)** directory for historical reference:
- `DEVELOPMENT_PHASES.md` (Superseded by `integration-roadmap.md`)
- `ARCHITECTURE.md` (Superseded by `system-architecture.md`)
- `agent-overview.md` (Superseded by `agent-foundation.md`)
- `agent-workflow.md` & `AI_WORKFLOW.md` (Superseded by `workflow-overview.md`)
- `agent-development-guide.md` & `tool-development-guide.md` (Superseded by `component-development-rules.md`)
