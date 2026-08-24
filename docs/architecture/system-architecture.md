# AssistLK System Architecture

## 1. Introduction

AssistLK is an Agentic AI powered multi-service platform designed to connect customers with service providers through an intelligent automated workflow.

The system uses multiple specialized AI agents that collaborate through a shared Agent Foundation. It supports vehicle, home, technical, and maintenance services while reducing manual searching, communication delays, and unstructured service coordination.

## 2. Architecture Overview

AssistLK follows a layered architecture combined with an Agentic AI framework.

```text
Users
    -> Web Application and Mobile Application
    -> Backend API
    -> Agent Orchestration Layer
    -> AI Agents, Agent Tools, and Memory System
    -> Safety Layer, Monitoring, and Audit System
    -> Application Layer
    -> Infrastructure Layer
    -> PostgreSQL Database
```

React and Flutter use the same ASP.NET Core API, identity model, permissions, and business rules. Clients never access PostgreSQL or the agent service directly.

## 3. Technology Architecture

### Frontend layer

**Technology:** React Web Application

**Responsibilities:** User interface, customer interaction, provider interaction, and admin dashboard.

### Mobile layer

**Technology:** Flutter

**Responsibilities:** Customer mobile access, provider mobile access, and notifications.

### Backend layer

**Technology:** .NET 8 Web API and C#

**Responsibilities:** Business logic, authentication, API management, authorization, validation, and agent execution coordination.

### Database layer

**Technology:** PostgreSQL and Entity Framework Core

**Responsibilities:** Data storage, relationships, transaction management, workflow state, and audit records.

## 4. Clean Architecture Structure

The backend follows Clean Architecture:

```text
AssistLK Backend
    -> API Layer
    -> Application Layer
    -> Domain Layer
    -> Infrastructure Layer
```

Dependencies must point inward toward the domain and application abstractions. Controllers and external integrations must not contain core business rules.

## 5. API Layer

**Location:** `AssistLK.Api`

**Responsibilities:**

- Receive HTTP requests.
- Validate input and authorization context.
- Map requests to application use cases.
- Return consistent responses.
- Connect frontend clients with backend services.

Examples:

```http
POST /api/service-requests
GET /api/providers
POST /api/bookings
```

## 6. Application Layer

**Location:** `AssistLK.Application`

**Responsibilities:**

- Coordinate application use cases.
- Apply business rules and validation.
- Coordinate agent workflows.
- Define ports for infrastructure services.

Examples include provider matching, quotation, booking, and service tracking services.

## 7. Domain Layer

**Location:** `AssistLK.Domain`

**Responsibilities:**

- Define entities.
- Define enums and value objects.
- Hold core business models and invariants.

Examples include `User`, `Provider`, `Booking`, `ServiceRequest`, and `Quotation`.

## 8. Infrastructure Layer

**Location:** `AssistLK.Infrastructure`

**Responsibilities:**

- Implement database access.
- Implement repositories and external service adapters.
- Manage PostgreSQL and Entity Framework Core integrations.
- Integrate notification, maps, and other external APIs.

External failures must produce an explicit recoverable workflow state rather than a partially applied business operation.

## 9. Agentic AI Architecture

AssistLK uses multiple specialized agents coordinated by the shared Agent Foundation.

```text
User Request
    -> Agent Orchestrator
    -> Problem Understanding Agent
    -> Provider Matching Agent
    -> Quotation and Booking Agent
    -> Service Tracking Agent
```

Agents do not directly call one another. The orchestrator controls execution order and shared workflow memory carries structured context between them.

## 10. Agent Foundation Components

### Agent Orchestrator

Manages workflows, selects agents, and controls execution order.

### Agent Memory System

Allows agents to share structured information. For example, Component 1 may store a battery issue and Colombo location, and Component 2 may read that context to find nearby mechanics.

### Agent Tool Framework

Allows agents to perform approved actions through tools such as `ProviderSearchTool`, `QuotationComparisonTool`, and `NotificationTool`. Agents must not directly access external systems.

### Safety Policy Engine

Controls risky AI actions. `CREATE_BOOKING`, `MAKE_PAYMENT`, and `CANCEL_SERVICE` require the appropriate approval workflow.

### Monitoring System

Tracks agent execution, success or failure, execution time, tool usage, and errors.

### Audit System

Records important state transitions, approvals, actor information, correlation identifiers, and execution evidence without storing hidden chain-of-thought.

## 11. Component Architecture

AssistLK contains four intelligent components:

```text
Component 1: Problem Understanding Agent
    -> Component 2: Provider Matching Agent
    -> Component 3: Quotation and Booking Agent
    -> Component 4: Service Tracking Agent
```

Each component remains independently testable and communicates through shared contracts, workflow memory, and orchestrated transitions.

## 12. Component Ownership

### Component 1: Smart Service Request and Problem Understanding

Understands customer problems, extracts information, and creates service requests.

### Component 2: Provider Matching and Recommendation

Searches providers, ranks eligible options, and generates recommendations.

### Component 3: Quotation and Booking

Manages quotations, customer approval, and booking creation.

### Component 4: Service Tracking

Manages status tracking, notifications, service history, and completion confirmation.

Component ownership means primary technical responsibility, not exclusive access. Cross-component changes require coordination with the owning member.

## 13. Security Architecture

Security must include:

- Authentication.
- Authorization.
- Data protection.
- Role-based access control.
- Approval workflows for sensitive actions.
- Input and AI output validation.
- Audit logging for important actions.

Domain operations are authorized and validated by the API. AI suggestions are untrusted input until validated.

## 14. Scalability Considerations

The architecture supports adding new service categories, agents, tools, and provider networks. New agents should be registered with the orchestrator and shared contracts without changing unrelated components.

Use versioned API contracts and correlation IDs for cross-service requests. Keep agent responses structured and bounded, and design external integrations behind replaceable infrastructure adapters.

## 15. Design Principles

AssistLK follows:

- Separation of concerns.
- Modular architecture.
- Reusable components.
- Secure AI execution.
- Human-controlled sensitive actions.
- Explicit and auditable state transitions.
- Maintainable code structure.
- One shared API boundary for web and mobile clients.
