# Database Ownership

## Runtime and schema scope

ASP.NET application services own persisted workflow memory, analysis, clarification history, and lifecycle changes. Python has no database ownership; its LangGraph state is request-scoped and not a competing persistence system. Hidden reasoning is not persisted. Entity sketches below are conceptual ownership examples, not exhaustive current EF schemas; later-component tables describe planned domains. See [agent architecture](../../agent-services/README.md).


PostgreSQL is shared by the platform, but tables and business rules have clear primary owners.


## Ownership rules

# AssistLK Database Ownership and Integration Guide

This document defines the shared database structure and ownership responsibilities for AssistLK. Its purpose is to prevent duplicate tables, conflicting migrations, incorrect relationships, and changes that break another component.

## 1. Purpose

PostgreSQL is a shared platform resource, but each component owns specific entities, invariants, and application use cases. Developers may modify their own component's data with the normal review process and must discuss changes to another component's data before implementation.

## 2. Database Architecture Overview

AssistLK uses one shared database containing core platform tables and component-owned tables.

```text
AssistLK Database
	-> Core Tables
	-> Component 1 Tables
	-> Component 2 Tables
	-> Component 3 Tables
	-> Component 4 Tables
```

## 3. Database Ownership Rules

| Component | Owner | Tables |
|---|---|---|
| Component 1 | Member 1 | `ServiceRequest`, `ProblemAnalysis` |
| Component 2 | Member 2 | `Provider`, `ProviderSkill`, `ProviderAvailability`, `ProviderRating` |
| Component 3 | Member 3 | `Quotation`, `QuotationItem`, `Booking`, `BookingStatusHistory` |
| Component 4 | Member 4 | `ServiceTracking`, `ServiceStatusHistory`, `Notification`, `ServiceIssue` |

The owning component defines data invariants and application use cases. Other components reference published identifiers and contracts rather than changing another component's tables directly. Cross-component writes must go through the owning application service or an agreed domain event.

## 4. Shared Core Tables

The following tables are used by multiple components and must not be modified without team discussion.

### User

Used by customers, providers, and administrators.

```text
Id
Name
Email
Phone
Role
```

### AgentWorkflow

Stores workflow execution state for all agents.

```text
Id
UserId
CurrentAgent
Status
CreatedDate
```

### AgentMemory

Stores structured context shared between agents.

```text
Id
WorkflowId
Key
Value
CreatedDate
```

### AgentExecutionMetric

Stores agent monitoring data.

```text
AgentName
Status
ExecutionTime
ToolCalls
```

Shared tables require coordinated schema changes, migration review, and regression testing.

## 5. Complete Database Relationship

```text
User
	-> AgentWorkflow
	-> AgentMemory
	-> ServiceRequest
	-> Provider
	-> Quotation
	-> Booking
	-> ServiceTracking
```

The main service flow is:

```text
ServiceRequest (Component 1)
	-> Quotation (Component 3)
	-> Booking (Component 3)
	-> ServiceTracking (Component 4)
```

Provider information used by quotations and bookings is owned by Component 2. Relationships should use stable foreign keys and published contracts.

## 6. Component 1 Database Design

**Owner:** Member 1

### ServiceRequest

Stores customer problems.

```text
Id
CustomerId
ServiceCategory
Description
Location
Urgency
Status
CreatedDate
```

Relationship:

```text
User -> ServiceRequest
```

### ProblemAnalysis

Stores AI analysis results.

```text
Id
ServiceRequestId
DetectedProblem
Confidence
AgentName
CreatedDate
```

Relationship:

```text
ServiceRequest -> ProblemAnalysis
```

## 7. Component 2 Database Design

**Owner:** Member 2

### Provider

```text
Id
UserId
BusinessName
ServiceCategory
Location
Description
Status
```

### ProviderSkill

Stores provider expertise such as battery repair and engine repair.

```text
Id
ProviderId
SkillName
```

### ProviderAvailability

```text
Id
ProviderId
AvailableDate
AvailableTime
Status
```

### ProviderRating

```text
Id
ProviderId
Rating
Review
```

## 8. Component 3 Database Design

**Owner:** Member 3

### Quotation

Stores provider offers and references the service request and provider.

```text
Id
ServiceRequestId
ProviderId
LabourCost
PartsCost
TotalCost
Status
```

Relationship:

```text
ServiceRequest -> Quotation <- Provider
```

### QuotationItem

Stores cost breakdown.

```text
Id
QuotationId
ItemName
Quantity
Price
```

### Booking

Stores confirmed services.

```text
Id
CustomerId
ProviderId
QuotationId
AppointmentDate
Status
```

### BookingStatusHistory

```text
Id
BookingId
OldStatus
NewStatus
ChangedDate
```

## 9. Component 4 Database Design

**Owner:** Member 4

### ServiceTracking

```text
Id
BookingId
CurrentStatus
EstimatedArrival
StartedTime
CompletedTime
```

### ServiceStatusHistory

```text
Id
TrackingId
OldStatus
NewStatus
ChangedDate
```

### Notification

```text
Id
UserId
Message
Type
IsRead
CreatedDate
```

### ServiceIssue

```text
Id
BookingId
Description
Status
CreatedDate
```

## 10. Component Data Flow

```text
Customer
	-> ServiceRequest (Component 1)
	-> ProblemAnalysis
	-> Provider (Component 2)
	-> Quotation (Component 3)
	-> Booking
	-> ServiceTracking (Component 4)
	-> Completion
```

## 11. Migration Rules

Before creating a migration, synchronize with the team's agreed integration branch:

```text
git checkout develop
git pull origin develop
```

Then create and test the migration:

```text
dotnet ef migrations add MigrationName
```

Migration changes require review when they affect shared tables or another component's foreign keys.

## 12. Migration Naming Convention

Use a clear component-specific name:

```text
AddProblemAnalysisTables
AddProviderManagementTables
AddQuotationBookingTables
AddServiceTrackingTables
```

## 13. Database Conflict Rules

If a component needs to modify another component's table:

1. Discuss the change with the owning member.
2. Agree on the schema and contract change.
3. Create an issue or task documenting the decision.
4. Update and test the migration together.

## 14. Entity and Relationship Rules

Use PascalCase for entity and property names where that matches the C# model conventions:

```text
ServiceRequest
ProviderRating
BookingStatusHistory
```

Always use primary keys, foreign keys, and proper relationships. For example, a quotation should store `ProviderId` referencing `Provider`, not a duplicated mutable `ProviderName`.

Avoid unbounded arrays and duplicate copies of mutable business data. Protect sensitive data with least-privilege access and audit requirements.

## 15. Testing Database Changes

Before opening a pull request:

```text
dotnet build
dotnet ef database update
```

Verify that tables are created, relationships work, migrations apply cleanly, and existing features remain functional. Add or update unit and integration tests for affected application behavior.

## Final Database Checklist

- [ ] Entities created.
- [ ] Relationships created.
- [ ] Migration added.
- [ ] Database updated.
- [ ] Existing APIs still work.
- [ ] Tests pass.
- [ ] Documentation updated.
- [ ] Pull request approved.
