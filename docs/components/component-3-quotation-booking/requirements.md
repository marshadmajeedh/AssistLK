# Component 3 Requirements Document

# Quotation and Booking Management Agent

This document is for Member 3. It defines the workflow connecting customer need, provider recommendation, quotation, customer approval, and booking creation.

## 1. Component Overview

### Component Name

Quotation and Booking Management Agent

### Purpose

This component manages the quotation and booking process between customers and service providers. It helps customers request quotations, receive provider offers, compare options, select a suitable quotation, approve the booking, and create a confirmed service appointment.

### Example

Component 1 provides the vehicle battery issue and Colombo location. Component 2 provides ABC Auto Service. Component 3 requests a quotation, receives the provider offer, compares it, asks for customer approval, and creates the booking only after approval.

```text
Customer Need
  -> Provider Recommendation
  -> Quotation
  -> Customer Approval
  -> Booking Creation
```

## 2. Problem Statement (Sri Lankan Context)

Service booking in Sri Lanka is often handled through calls and informal messages. Customers manually negotiate prices, compare labour and parts costs, arrange appointments without a consistent record, and may lack control over the final provider and quotation selection. A structured workflow improves transparency, traceability, and trust.

## 3. Component Objectives

### Primary objectives

- Automate the quotation workflow.
- Allow providers to submit quotations.
- Allow customers to compare quotations.
- Create confirmed bookings after approval.

### Secondary objectives

- Improve price transparency.
- Reduce communication delays.
- Maintain quotation history.
- Improve customer trust.

## 4. Scope

### Included

- Quotation request creation.
- Provider quotation submission.
- Quotation comparison.
- Customer approval or rejection.
- Booking creation.
- Appointment scheduling.
- Booking status management.

### Excluded

- Analysing customer problems, which belongs to Component 1.
- Finding providers, which belongs to Component 2.
- Tracking service progress after booking, which belongs to Component 4.
- Handling an actual payment gateway, which is a future enhancement.

## 5. Responsibilities

### Create quotation request

Receive the customer problem and selected provider, then create a quotation request for that provider.

### Collect provider quotations

Store provider pricing such as labour cost, parts cost, total cost, and estimated completion time.

```json
{
  "labourCost": 3000,
  "partsCost": 5000,
  "totalCost": 8000
}
```

### Compare quotations

Compare price, estimated completion time, provider rating, service quality, warranty information, and other approved factors.

### Recommend a quotation

Provide a concise explanation, such as: "Quotation A is recommended because it provides the lowest cost with a highly rated provider."

### Manage booking approval

Require customer approval before booking creation. The agent may recommend an option but may not approve or book it on the customer's behalf.

## 6. Non-Responsibilities (Important)

This component must not analyse customer problems, search for or rank providers, track service progress after booking, process payments, or automatically create a booking without the required approval. It must not modify another component's entities directly or call another agent directly.

## 7. User Roles

### Customer

- Request quotations.
- View and compare quotations.
- Approve a booking.
- Reject a quotation.
- Cancel a request according to business rules.

### Provider

- Receive quotation requests.
- Submit quotations.
- Update availability.

### Admin

- Monitor quotations.
- Manage disputes.
- View booking activity.

## 8. Agent Architecture

Component 3 uses the shared Agent Foundation and Phase 6F Safety and Approval system.

```text
Provider Matching Agent
  -> Workflow Memory
  -> Quotation Agent
  -> Quotation Tools
  -> Safety Policy
  -> Customer Approval
  -> Booking Creation
  -> Monitoring
```

## 9. Agent Workflow

```text
Receive Provider Recommendation
  -> Create Quotation Request
  -> Send Request to Provider
  -> Wait for Quotations
  -> Analyse Quotations
  -> Generate Recommendation
  -> Require Customer Approval
  -> Approved?
  -> Create Booking when approved
  -> Cancel or select another option when rejected
```

## 10. Agent Decision Flow

When a customer selects ABC Auto Service, the agent checks whether a quotation is available, compares its price and provider rating with other options, generates a recommendation, and pauses for customer approval before any booking operation.

## 11. Functional Requirements

### FR-001 - Create Quotation Request

The system shall allow customers to request quotations from selected providers.

### FR-002 - Send Request to Provider

The system shall notify providers about quotation requests.

### FR-003 - Provider Submit Quotation

Providers shall be able to submit service cost, parts cost, labour cost, and estimated completion time.

### FR-004 - Store Quotation History

The system shall maintain previous quotations and revisions with their status and timestamps.

### FR-005 - Compare Quotations

The agent shall compare multiple quotations using price, time, rating, service quality, and warranty information where available.

### FR-006 - Generate Recommendation

The agent shall recommend suitable quotations and provide a concise reason.

### FR-007 - Request Customer Approval

The system shall request customer confirmation before booking.

### FR-008 - Create Booking

After valid customer approval, the system shall create a service booking through an authorized application operation.

### FR-009 - Update Booking Status

The system shall support `Pending`, `Approved`, `Confirmed`, `Cancelled`, and `Completed` statuses, with valid transitions enforced by the backend.

### FR-010 - Store Booking Information

The system shall save the provider, customer, date, time, quotation, and service details.

## 12. Non-Functional Requirements

- **Performance:** Normal quotation processing should complete within five seconds where dependencies are available.
- **Security:** Customer details, provider pricing, and booking information must be protected.
- **Reliability:** Quotation information and approval history must not be lost.
- **Scalability:** The system should support multiple providers, quotations, and service categories.
- **Maintainability:** Quotation and booking logic must be separated from controllers and clients.
- **Availability:** Customers should be able to access quotation status at any time according to platform availability goals.

## 13. Database Requirements

### Quotation

```text
Id
ServiceRequestId
ProviderId
Description
LabourCost
PartsCost
TotalCost
EstimatedTime
Status
CreatedDate
```

### QuotationItem

Stores cost breakdown such as a battery, quantity one, and price 5000.

```text
Id
QuotationId
ItemName
Quantity
Price
```

### Booking

```text
Id
CustomerId
ProviderId
QuotationId
AppointmentDate
Status
CreatedDate
```

### BookingStatusHistory

```text
Id
BookingId
PreviousStatus
NewStatus
ChangedDate
```

## 14. Entity Design

A quotation belongs to a service request and provider. A quotation may contain multiple quotation items. A booking references the approved quotation, customer, and provider. Booking status history records every valid transition with its previous and new status and timestamp.

Use primary keys, foreign keys, proper relationships, decimal-safe money types, and domain validation for costs, dates, ownership, and status transitions. The Component 3 owner defines its entity invariants and application use cases.

## 15. API Requirements

### Create Quotation Request

```http
POST /api/quotations/request
```

```json
{
  "providerId": 1,
  "serviceRequestId": 10
}
```

### Submit Quotation

```http
POST /api/quotations
```

```json
{
  "labourCost": 3000,
  "partsCost": 5000
}
```

### Get Quotations

```http
GET /api/quotations/request/{id}
```

### Approve Booking

```http
POST /api/bookings/approve
```

### Get Booking

```http
GET /api/bookings/{id}
```

The ASP.NET Core API is the public boundary and owns authorization, validation, approval enforcement, and state transitions.

## 16. Agent Integration Requirements

Component 3 must use:

- `IAgent` for quotation agent implementation.
- `AgentContext` to access customer request and provider information.
- `AgentMemoryService` to store selected provider, approved quotation, and workflow status.
- `ToolExecutor` for quotation comparison and booking creation tools.
- `AgentSafetyService` before booking creation and payment-related actions.
- `AgentMonitoringService` to track quotation processing, booking creation, and failures.

Agents must return structured outputs validated against shared schemas and must not directly access the database or external services.

## 17. Required Tools

### QuotationComparisonTool

Compares quotations such as Quotation A at 8000 and Quotation B at 9500, then returns the recommended option with its comparison factors.

### CostCalculationTool

Calculates parts, labour, and additional charges using validated money values.

### BookingCreationTool

Creates a confirmed booking only after the API verifies approval and all booking rules.

### AvailabilityValidationTool

Checks provider availability, date availability, and time availability.

All tools must implement `IAgentTool`, have clear responsibilities, handle errors, and be invoked through `ToolExecutor`.

## 18. Memory Usage

Store workflow context using `AgentMemoryService`:

```json
{
  "quotationAmount": 8000,
  "selectedProvider": "ABC Auto",
  "bookingStatus": "Approved"
}
```

Component 4 may use this information after booking. Do not create a separate memory system.

## 19. Safety and Approval Rules

This component must strictly follow approval rules. `CREATE_BOOKING` and `MAKE_PAYMENT` are high-risk actions requiring approval.

The agent must not automatically book without confirmation, modify a quotation after approval, or make payment decisions.

Correct customer interaction:

> A quotation of Rs.8000 is available. Would you like to confirm this booking?

Approval must be recorded with the authorized customer, timestamp, quotation version, and workflow correlation identifier before booking creation.

## 20. Monitoring Requirements

Track:

- Number of quotation requests.
- Average quotation response time.
- Booking success rate.
- Approval rate.
- Failed bookings.
- Tool usage and errors.

Use `AgentMonitoringService` for execution records and metrics.

## 21. Error Handling

### No quotation received

Return:

```text
No quotation available currently.
Try another provider.
```

### Provider unavailable

Mark the option unavailable and suggest eligible alternatives.

### Customer rejects quotation

Update the quotation to `Rejected` and do not create a booking.

### Approval or booking failure

Keep the workflow in an explicit recoverable state, log the failure through monitoring, and avoid partial booking records.

## 22. Security Requirements

- Authenticate customers, providers, and administrators.
- Authorize quotation and booking access by ownership and role.
- Validate quotation versions and prevent changes after approval.
- Protect customer details, provider pricing, and booking information.
- Prevent replay or duplicate booking approvals.
- Do not expose internal agent endpoints or database access to clients.
- Do not commit credentials, tokens, or private data in fixtures.

## 23. Testing Requirements

### Unit testing

Test cost calculation, comparison logic, approval validation, booking validation, status transitions, duplicate approval prevention, and quotation version handling.

### Integration testing

Test the complete flow:

```text
Provider Matching Agent
  -> Quotation Agent
  -> Customer Approval
  -> Booking
```

### Agent testing

- Customer accepts a quotation: booking is created.
- Customer rejects a quotation: no booking is created.
- A high-risk action is requested: approval is required.
- Provider becomes unavailable: booking is blocked or an alternative is suggested.
- Quotation changes after approval: the operation is rejected.

## 24. Git Development Workflow

Use the branch:

```text
feature/component-3-quotation-booking-agent
```

Keep Component 3 implementation within the established Quotation and Booking feature boundaries. Use commit messages such as:

```text
feat(component3): add quotation workflow
feat(component3): add booking approval flow
```

Create the pull request according to the repository workflow and target the team's agreed integration branch.

## 25. Expected Deliverables

- Quotation Agent.
- Quotation database entities and migration.
- Booking database entities and migration.
- Quotation comparison logic.
- Booking workflow.
- Approval integration.
- Memory integration.
- Safety integration.
- Monitoring integration.
- APIs.
- Unit and integration tests.
- Component documentation.

## 26. Completion Checklist

- [ ] Agent implemented.
- [ ] Quotation workflow completed.
- [ ] Provider quotation submission completed.
- [ ] Comparison logic completed.
- [ ] Approval flow completed.
- [ ] Booking creation completed.
- [ ] Safety applied.
- [ ] Memory integrated.
- [ ] Monitoring enabled.
- [ ] APIs completed.
- [ ] Tests completed.
- [ ] Documentation completed.
- [ ] Pull request created and reviewed.
