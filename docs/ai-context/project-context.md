# AssistLK Project Context

> **Current C1 runtime:** Python-only FastAPI/LangGraph via the ASP.NET external adapter. .NET owns authorization, lifecycle, persistence, monitoring, and recovery. Python owns tools, reasoning, and provider secrets. C# interface and service lists below apply to the ASP.NET integration, not Python functions. See [canonical architecture](../../agent-services/README.md). Future-component examples are conceptual.

## 1. Project Overview

AssistLK is an Agentic AI powered multi-service platform designed to connect customers with verified service providers.

The system solves the problem of finding, communicating with, and managing service providers through an intelligent workflow.

The platform supports multiple service categories:

- Vehicle services
- Home services
- Technical services
- Maintenance services

Examples include mechanic, battery replacement, towing, plumbing, electrical, appliance repair, device repair, and maintenance services.

## 2. Main Project Goal

AssistLK provides an intelligent service coordination platform where AI agents can:

- Understand customer problems
- Find suitable providers
- Manage quotations
- Handle booking workflows
- Track service progress
- Maintain context between different agents

The system should reduce manual communication and provide a structured service experience.

## 3. System Type

- **Project type:** Agentic AI Multi-Service Platform
- **Architecture style:** Clean Architecture plus AI Agent Framework
- **Main concept:** Specialized AI agents collaborate through shared workflow memory, tools, safety controls, and monitoring.

## 4. Technology Stack

### Backend

- Framework: .NET 8 Web API
- Language: C#
- Architecture: Clean Architecture
- ORM: Entity Framework Core
- Database: PostgreSQL

### Frontend

React

### Mobile Application

Flutter

## 5. High-Level Architecture

Each component must respect this architecture:

```text
Controller Layer
	-> Application Layer
	-> Agent Layer
	-> Tool Layer
	-> Infrastructure Layer
	-> Database
```

## 6. Agentic AI Architecture

AssistLK uses multiple AI agents supported by shared platform services.

### Agent Orchestrator

- Starts workflows
- Selects agents
- Manages execution order

### Agent Memory System

- Shares information between agents
- Maintains workflow context

### Tool Framework

- Performs database operations through approved tools
- Communicates with external services
- Executes system actions

### Safety Layer

- Evaluates risk
- Enforces approvals
- Prevents unsafe actions

### Monitoring System

- Records execution metrics
- Tracks performance
- Monitors errors

## 7. Agent Workflow

```text
Customer Request
	-> Create Workflow
	-> Select Agent
	-> Read Memory
	-> Agent Reasoning
	-> Use Tools
	-> Safety Check
	-> Approval if required
	-> Execute Action
	-> Save Memory
	-> Record Metrics
```

## 8. Component Structure

AssistLK contains four main intelligent components.

### Component 1: Smart Service Request and Problem Understanding Agent

**Owner:** Member 1

**Purpose:** Understand customer problems and convert natural language requests into structured service information.

**Responsibilities:** Receive requests, identify categories, extract details, detect urgency, ask follow-up questions, and store information in memory.

**Does not:** Search providers, create quotations, or track services.

### Component 2: Provider Matching and Recommendation Agent

**Owner:** Member 2

**Purpose:** Find and recommend suitable service providers.

**Responsibilities:** Search and filter providers, check availability, rank providers, and recommend providers.

**Does not:** Analyse customer problems, create bookings, or track services.

### Component 3: Quotation and Booking Management Agent

**Owner:** Member 3

**Purpose:** Manage quotation and booking workflows.

**Responsibilities:** Request quotations, receive provider quotations, compare quotations, request customer approval, and create bookings.

**Sensitive actions:** Booking creation and payment-related actions must use `AgentSafetyService`.

**Does not:** Find providers or track service progress.

### Component 4: Service Tracking and Customer Communication Agent

**Owner:** Member 4

**Purpose:** Track service progress after booking.

**Responsibilities:** Manage service status, track progress, notify customers, maintain service history, and confirm completion.

**Does not:** Analyse problems, find providers, or create quotations.

## 9. Shared Agent Rules

The .NET orchestration side uses:

- `IAgent`
- `AgentContext`
- `AgentMemoryService`
- `AgentSafetyService`
- `AgentMonitoringService`

Agents must not directly call another agent, directly access the database, bypass safety checks, or create separate memory systems.

Conceptual shared C# tool interaction (not the Python C1 persistence path):

```text
Agent
	-> ToolExecutor
	-> Tool
	-> Database or API
```

## 10. Memory Rules

Persisted workflow information is managed by ASP.NET through `AgentMemoryService`; transient Python state is separate. For example, Component 1 may store a battery issue and Colombo location, and Component 2 may read that information to find providers. Never create separate memory solutions.

## 11. Tool Rules

Tools perform external actions. Examples include `ProviderSearchTool`, `QuotationComparisonTool`, and `NotificationTool`.

Tools have a clear responsibility and handle errors. Only shared C# tools implement `IAgentTool`; Python tools follow the Python service architecture.

## 12. Safety Rules

Sensitive operations require approval. Examples include `CREATE_BOOKING`, `CANCEL_BOOKING`, and `MAKE_PAYMENT`.

Agents must never automatically confirm bookings, make financial decisions, or perform destructive actions without approval.

## 13. Monitoring Rules

Every agent execution should record the agent name, execution status, execution time, tool usage, and errors through `AgentMonitoringService`.

## 14. Database Rules

Database ownership follows component boundaries:

| Component | Entities |
|---|---|
| Component 1 | `ServiceRequest`, `ProblemAnalysis` |
| Component 2 | `Provider`, `ProviderSkill`, `ProviderAvailability`, `ProviderRating` |
| Component 3 | `Quotation`, `QuotationItem`, `Booking`, `BookingStatusHistory` |
| Component 4 | `ServiceTracking`, `ServiceStatusHistory`, `Notification`, `ServiceIssue` |

Do not modify another component's tables without discussion.

## 15. Development Rules

Follow this order:

```text
Database
	-> Application Services
	-> Agent
	-> Tools
	-> API
	-> Testing
	-> Documentation
```

## 16. Git Rules

Use the branch format `feature/component-name-description`.

Examples:

- `feature/component-1-problem-agent`
- `feature/component-2-provider-agent`
- `feature/component-3-booking-agent`
- `feature/component-4-tracking-agent`

Use the commit format `type(component): description`.

Examples:

- `feat(component2): add provider search`
- `fix(component3): fix quotation workflow`
- `docs(component4): update tracking guide`

## 17. Important Files

Before development, read:

- Project context: `/docs/ai-context/project-context.md`
- Copilot instructions: `/.github/copilot-instructions.md`
- Development rules: `/docs/development/component-development-rules.md`
- Database rules: `/docs/development/database-ownership.md`
- Component requirements: `/docs/components/`

## 18. Do Not Modify

Without team approval, do not modify the Agent Orchestrator, Agent Foundation, Memory System, Safety System, Monitoring System, or shared database configuration.

## 19. Code Quality Expectations

Generated code must follow the existing architecture, use meaningful names, avoid duplicate logic, include validation and error handling, and remain maintainable. Do not create temporary solutions that break the architecture.

## 20. Final AI Assistant Instruction

When generating code for AssistLK:

1. Understand the component requirement first.
2. Follow the existing architecture.
3. Reuse shared Agent Foundation features.
4. Do not create duplicate systems.
5. Keep components independent.
6. Respect ownership boundaries.
7. Prioritize maintainable, production-quality code.

The goal is to build a scalable Agentic AI service platform.
