# AssistLK GitHub Copilot Instructions

## 1. Project Overview

AssistLK is an Agentic AI powered multi-service platform designed to connect customers with service providers.

The platform supports multiple service domains:

- Vehicle services
- Home services
- Technical services
- Maintenance services

The system follows an AI agent based architecture where agents understand requests, use tools, maintain memory, follow safety rules, and monitor their own execution.

## 2. Technology Stack

### Backend

- Framework: .NET 8 Web API
- Architecture: Clean Architecture
- Database: PostgreSQL
- ORM: Entity Framework Core

### Frontend

React

### Mobile

Flutter

## 3. Important Documentation

Before generating code, always read:

- Project context: `/docs/ai-context/project-context.md`
- Architecture: `/docs/architecture/`
- Development rules: `/docs/development/component-development-rules.md`
- Database rules: `/docs/development/database-ownership.md`

Read the relevant component documentation before implementing:

- Component 1: `/docs/components/component-1-problem-understanding/`
- Component 2: `/docs/components/component-2-provider-matching/`
- Component 3: `/docs/components/component-3-quotation-booking/`
- Component 4: `/docs/components/component-4-service-tracking/`

## 4. Core Architecture Rules

AssistLK follows:

```text
Controller
	-> Application Service
	-> Agent
	-> Tool
	-> Infrastructure
	-> Database
```

Do not skip architectural layers.

## 5. Agent Development Rules

Every AI agent must:

- Implement `IAgent`
- Use `AgentContext`
- Use `AgentMemoryService`
- Use `AgentSafetyService`
- Use `AgentMonitoringService`

Example structure:

```text
FeatureName
|
|- Agents
|- Services
|- Tools
|- DTOs
|- Controllers
|- Validators
```

## 6. Agent Communication Rules

Agents must not directly call other agents.

Incorrect:

```text
ProblemAgent
	-> ProviderAgent
```

Correct:

```text
ProblemAgent
	-> Workflow Memory
	-> ProviderAgent
```

The Agent Orchestrator controls agent execution order.

## 7. Memory Rules

All agents must use `AgentMemoryService`. Do not create separate memory systems.

Example workflow memory:

```text
Problem: Battery issue
Location: Colombo
```

Another agent should read from workflow memory.

## 8. Tool Development Rules

Agents must not directly access:

- Database
- External APIs
- External services

Correct flow:

```text
Agent
	-> ToolExecutor
	-> Tool
	-> Database or API
```

All tools must implement `IAgentTool`.

## 9. Safety Rules

Sensitive actions require `AgentSafetyService`.

Examples requiring approval:

- `CREATE_BOOKING`
- `CANCEL_BOOKING`
- `MAKE_PAYMENT`

Never bypass approval workflows. Agents must not make financial decisions, perform destructive actions, or modify important records without validation.

## 10. Monitoring Rules

Every agent execution must support:

- Execution time tracking
- Success and failure tracking
- Tool usage tracking
- Error recording

Use `AgentMonitoringService`.

## 11. Database Rules

Each component owns its database entities.

### Component 1

- `ServiceRequest`
- `ProblemAnalysis`

### Component 2

- `Provider`
- `ProviderSkill`
- `ProviderAvailability`
- `ProviderRating`

### Component 3

- `Quotation`
- `QuotationItem`
- `Booking`
- `BookingStatusHistory`

### Component 4

- `ServiceTracking`
- `ServiceStatusHistory`
- `Notification`
- `ServiceIssue`

Do not modify another component's entities without discussion.

## 12. Entity Rules

Use PascalCase for entity names:

```text
ServiceRequest
ProviderRating
BookingStatusHistory
```

Always use primary keys, foreign keys, and proper relationships.

## 13. API Rules

Use REST conventions:

- `GET`: Retrieve data
- `POST`: Create data
- `PUT`: Update data
- `DELETE`: Remove data

Examples:

```text
GET /api/providers
POST /api/bookings
```

## 14. Code Quality Rules

Generated code must:

- Follow existing project style
- Use meaningful names
- Avoid duplicate logic
- Include validation
- Include error handling
- Be maintainable

Do not create unnecessary classes.

## 15. Git Rules

Use feature branches named `feature/component-name-description`.

Examples:

- `feature/component-2-provider-agent`
- `feature/component-3-booking-flow`

Use commit messages in the format `type(component): description`.

Examples:

- `feat(component2): add provider search tool`
- `fix(component3): fix quotation calculation`
- `docs(component4): update tracking documentation`

## 16. Before Creating Code

Always:

1. Understand the requirement document.
2. Check the existing architecture.
3. Check database ownership.
4. Check existing services.
5. Avoid duplicate implementations.

## 17. Do Not Modify

Without team approval, do not modify:

- Agent Foundation
- Agent Orchestrator
- AgentMemoryService
- AgentSafetyService
- AgentMonitoringService
- Shared database context
- Core architecture

## 18. Testing Requirements

Every feature should include unit tests for business logic, agent behaviour, and tool execution.

Integration tests should cover API workflows, database operations, and agent communication.

## 19. Development Workflow

Follow this sequence:

```text
Requirement
	-> Database Design
	-> Application Logic
	-> Agent Implementation
	-> Tools
	-> API
	-> Testing
	-> Documentation
	-> Pull Request
```

## 20. Final Rule

When generating code, prioritize:

1. Existing AssistLK architecture
2. Component requirements
3. Shared Agent Foundation
4. Maintainability
5. Security
6. Clean code

Do not create quick solutions that break the architecture. The goal is to build a production-quality Agentic AI platform.
