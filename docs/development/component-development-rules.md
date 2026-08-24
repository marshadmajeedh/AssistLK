# AssistLK Component Development Rules and Integration Guide

This document is the shared rule book for all four component developers. Every component must follow the same architecture, coding style, Git workflow, and AI integration pattern.

## 1. Purpose

These rules define development standards for all AssistLK component agents and their supporting backend, database, API, web, mobile, and test code.

## 2. Overall Architecture Rule

All components must follow:

```text
Controller Layer
	-> Application Service Layer
	-> Agent Layer
	-> Tool Layer
	-> Infrastructure Layer
	-> Database
```

Example:

```text
Provider Matching API Controller
	-> ProviderMatchingService
	-> ProviderMatchingAgent
	-> ProviderSearchTool
	-> Database
```

Do not skip architectural layers. Keep business rules in the backend application and domain layers, and keep controllers thin.

## 3. Folder Structure Rules

The backend feature areas should be organized as:

```text
AssistLK.Api
	Features
		-> ProblemUnderstanding
		-> ProviderMatching
		-> QuotationBooking
		-> ServiceTracking
```

Each feature should use the following structure where applicable:

```text
ProviderMatching
	-> Controllers
	-> DTOs
	-> Agents
	-> Services
	-> Tools
	-> Validators
```

## 4. Agent Development Rules

Every agent must implement `IAgent`, expose a meaningful name, and provide an asynchronous execution method that accepts `AgentContext` and returns `AgentResult`.

Example:

```csharp
public class ProviderMatchingAgent : IAgent
{
		public string Name => "ProviderMatchingAgent";

		public Task<AgentResult> ExecuteAsync(AgentContext context)
		{
				// Implementation follows the shared agent contract.
		}
}
```

Agents must use the shared context, memory, safety, monitoring, and tool-execution services.

## 5. Agent Communication Rules

Agents must not directly call other agents.

Incorrect:

```text
ProviderAgent
	-> QuotationAgent
```

Correct:

```text
ProviderAgent
	-> Workflow Memory
	-> QuotationAgent reads memory
```

The Agent Orchestrator controls workflow order and agent execution.

## 6. Memory Rules

Every component must use `AgentMemoryService`. Never create separate memory tables or manually managed workflow memory systems.

For example, Component 1 stores:

```json
{
	"problem": "Battery issue"
}
```

Component 2 reads that shared workflow information. Memory should contain structured, minimal, auditable context.

## 7. Tool Development Rules

Agents must not directly access databases, external APIs, or external services.

Correct flow:

```text
Agent
	-> ToolExecutor
	-> Tool
	-> Database or API
```

Every tool must implement `IAgentTool`, have a clear responsibility, be reusable, handle errors properly, and be authorized and observable.

## 8. Safety Rules

Every sensitive action must go through `AgentSafetyService` and the applicable safety policy. Examples include:

```text
CREATE_BOOKING
CANCEL_BOOKING
MAKE_PAYMENT
```

No developer or agent may bypass approval workflows. AI output must be validated before it reaches a domain operation.

## 9. Monitoring Rules

Every component must record:

- Execution time
- Success or failure
- Tool usage
- Errors
- Correlation identifiers where work is asynchronous

Use `AgentMonitoringService` for agent execution and tool metrics.

## 10. Database Rules

Each component owns its entities. For example:

- Component 2: `Provider`, `ProviderSkill`, `ProviderAvailability`
- Component 3: `Quotation`, `Booking`
- Component 4: `ServiceTracking`, `Notification`

The owning component defines invariants and use cases. Other components must use published identifiers and contracts rather than changing another component's entities directly.

### Migration rule

Before creating a migration, synchronize with the team's agreed integration branch:

```text
git pull origin develop
```

Then create and test the migration to reduce conflicts. Do not commit secrets, generated build output, or personal data in fixtures.

## 11. Git Workflow Rules

When starting work:

```text
git checkout develop
git pull origin develop
```

Create a feature branch such as:

```text
git checkout -b feature/component-2-provider-agent
```

Follow the repository's agreed integration branch if it differs from `develop`.

## 12. Commit Naming Convention

Use:

```text
type(component): description
```

Examples:

```text
feat(component2): add provider search tool
fix(component3): fix quotation calculation
docs(component4): update tracking guide
```

## 13. Pull Request Rules

Before creating a pull request, confirm:

- Code builds.
- Relevant tests pass.
- No unnecessary files are included.
- README or component documentation is updated when needed.
- Database migrations are included when required.

Use the repository pull request template. A title should follow the commit format, for example:

```text
feat(component2): implement provider matching agent
```

The description should summarize the workflow, added files or features, and tests performed.

## 14. Shared File Rules

Do not modify the following shared systems without team discussion:

- Agent Foundation
- Agent Orchestrator
- Memory System
- Safety System
- Monitoring System
- Shared database context
- Core architecture

If a shared change is required, discuss the impact with the team before implementation.

## 15. Component Completion Definition

A component is complete only when all applicable items are delivered:

```text
Agent
	+ Database
	+ API
	+ Tools
	+ Memory Integration
	+ Safety Integration
	+ Monitoring Integration
	+ Tests
	+ Documentation
```

## 16. Development Sequence

Every member should follow:

```text
Phase 1: Database entities
	-> Phase 2: Application services
	-> Phase 3: Agent implementation
	-> Phase 4: Tools
	-> Phase 5: API integration
	-> Phase 6: Testing
	-> Phase 7: Documentation
	-> Phase 8: Pull request
```

## 17. Final Component Integration Flow

```text
Customer
	-> Component 1: Understand Problem
	-> Shared Workflow Memory
	-> Component 2: Find Provider
	-> Shared Workflow Memory
	-> Component 3: Quotation and Booking
	-> Approval
	-> Component 4: Track Service
	-> Completion
```

Components must remain independently testable while using the shared workflow contracts and Agent Foundation.

## 18. Team Communication Rules

Discuss changes to shared architecture before implementation. Inform the team before changing shared database files. Before merging, ensure the build and relevant tests pass.

## Completion Checklist

- [ ] All members have read this document.
- [ ] Branch naming is agreed.
- [ ] Folder structure is agreed.
- [ ] Agent rules are understood.
- [ ] Memory usage is understood.
- [ ] Safety usage is understood.
- [ ] Monitoring usage is understood.
- [ ] Pull request workflow is understood.
