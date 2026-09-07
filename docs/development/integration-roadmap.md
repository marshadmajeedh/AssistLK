# AssistLK Component Development Timeline and Integration Roadmap

## 1. Purpose

This document defines the development order, component dependencies, integration strategy, testing milestones, and team coordination plan. AssistLK must be built as one complete system rather than four isolated components.

## 2. Final System Development Strategy

```text
Phase 6: Shared Agent Foundation
	-> Completed
Phase 7: Individual Component Development
	-> Phase 8: Component Integration
	-> Phase 9: System Testing
	-> Phase 10: Final Demo Preparation
```

Every phase should leave the previously completed workflow usable and must not require frontend clients to bypass the ASP.NET Core API.

## 3. Component Dependency Order

Components depend on the structured data produced by earlier components:

```text
Component 1: Problem Understanding
	-> Component 2: Provider Matching
	-> Component 3: Quotation and Booking
	-> Component 4: Service Tracking
```

### Component 1 to Component 2

Component 1 produces service type, problem, location, and urgency. Component 2 uses this workflow context to find and recommend providers.

### Component 2 to Component 3

Component 2 produces the selected or recommended provider. Component 3 uses that provider and the service request to request quotations.

### Component 3 to Component 4

Component 3 produces an approved booking, provider, and appointment time. Component 4 uses the booking to track the service.

## 4. Phase 7: Component Development

Each member develops the assigned agent independently while following the shared database, architecture, memory, safety, monitoring, and testing rules.

### Phase 7A: Component 1 Development

**Owner:** Member 1

**Build:** Problem Understanding Agent

**Tasks:** Database entities, agent implementation, problem classification, information extraction, memory integration, API endpoints, and tests.

**Output:** Customer request to structured service request.

> **Note on Component 1 Internal Sub-Phases (7A–7G):**
> While the overall project roadmap allocates Phase 7 by component ownership (Phase 7A = Component 1, Phase 7B = Component 2, Phase 7C = Component 3, Phase 7D = Component 4), Component 1 internally executed seven structured development sub-phases:
> - **7A:** Domain Entities (`ServiceRequest`, `ProblemAnalysis`), Enums, EF Core Mapping & Migration
> - **7B:** Application Layer (DTOs, Repositories, `ServiceRequestService`, Ownership & Validation)
> - **7C:** Problem Understanding Agent Implementation & Typed Model Contracts
> - **7D:** Agent Tools (`ProblemClassification`, `LocationExtraction`, `ServiceKnowledge`), Memory & Safety Enforcement
> - **7E:** Authenticated REST API (`ServiceRequestsController`), JWT Identity Claims & Response Contracts
> - **7F:** Real PostgreSQL Integration Testing (Migrations, Constraints, Cascades, Concurrency & Guards)
> - **7G:** Component 1 Cleanup, Architecture & Testing Documentation, Release Readiness

### Phase 7B: Component 2 Development

**Owner:** Member 2

**Build:** Provider Matching Agent

**Tasks:** Provider database, search, filtering, ranking algorithm, recommendation API, memory integration, and tests.

**Output:** Service request to recommended providers.

### Phase 7C: Component 3 Development

**Owner:** Member 3

**Build:** Quotation and Booking Agent

**Tasks:** Quotation workflow, provider quotation submission, comparison logic, approval workflow, booking creation, safety integration, and tests.

**Output:** Provider to quotation to booking.

### Phase 7D: Component 4 Development

**Owner:** Member 4

**Build:** Service Tracking Agent

**Tasks:** Status workflow, timeline, notifications, progress updates, completion confirmation, and tests.

**Output:** Booking to tracked service.

## 5. Development Rules During Phase 7

Every member should complete work in this order:

```text
1. Database entities, relationships, and migration
2. Application services, business rules, and validators
3. Agent implementation with IAgent, context, memory, and tools
4. IAgentTool implementations
5. API controllers and integration
6. Unit and integration testing
7. Documentation
```

## 6. Phase 8: Component Integration

Begin integration after individual component workflows and focused tests are complete.

### Integration 1: Component 1 and Component 2

Test:

```text
Customer Request
	-> Problem Analysis
	-> Provider Recommendation
```

### Integration 2: Component 2 and Component 3

Test:

```text
Provider Selected
	-> Quotation
	-> Customer Approval
	-> Booking
```

### Integration 3: Component 3 and Component 4

Test:

```text
Booking Created
	-> Service Tracking
```

## 7. Complete System Flow Test

The final end-to-end scenario should cover:

```text
Customer: "My vehicle stopped near Colombo"
	-> Component 1 understands the problem
	-> Component 2 finds suitable mechanics
	-> Component 3 obtains a quotation and creates a booking after approval
	-> Component 4 tracks the repair
	-> Customer confirms service completion
```

## 8. Integration Meeting Plan

### Meeting 1: After Phase 7 starts

Discuss progress, blockers, and planned database changes.

### Meeting 2: After component completion

Discuss API compatibility, shared schemas, and workflow integration.

### Meeting 3: Before the final demo

Discuss full-system testing, known limitations, and presentation readiness.

## 9. Branch Strategy

```text
main
	-> develop
		-> feature/component-1-problem-agent
		-> feature/component-2-provider-agent
		-> feature/component-3-quotation-booking-agent
		-> feature/component-4-service-tracking-agent
```

Use the team's current integration branch if it differs from `develop`, and follow the repository's branch naming rules.

## 10. Pull Request Rules

Before merging, the developer must confirm:

- Build is successful.
- Relevant tests pass.
- There are no conflicts with the integration branch.
- Database migrations are tested.
- Documentation is updated.

## 11. Integration Testing Checklist

Before the final demo, verify:

- [ ] User registration works.
- [ ] Customer request works.
- [ ] Problem analysis works.
- [ ] Provider matching works.
- [ ] Quotation works.
- [ ] Booking works.
- [ ] Approval works.
- [ ] Tracking works.
- [ ] Notifications work.
- [ ] Agent metrics are recorded.
