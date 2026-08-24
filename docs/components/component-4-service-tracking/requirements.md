# Component 4 Requirements Document

# Service Tracking and Customer Communication Agent

This document is for Member 4. It defines the component that completes the service lifecycle after booking confirmation.

```text
Customer Request
  -> Problem Understanding Agent (Component 1)
  -> Provider Matching Agent (Component 2)
  -> Quotation and Booking Agent (Component 3)
  -> Service Tracking Agent (Component 4)
```

## 1. Component Overview

### Component Name

Service Tracking and Customer Communication Agent

### Purpose

This component monitors service progress after a booking has been confirmed. It helps customers know the service status, receive updates, track provider progress, communicate with providers, and confirm service completion.

### Example

After Component 3 creates a booking for ABC Auto Service at 10:30 AM, Component 4 manages the journey from booking confirmation through provider arrival, service progress, completion, customer confirmation, and service closure.

```text
Booking Confirmed
  -> Provider Starts Journey
  -> Provider Arrived
  -> Service Started
  -> Repair Completed
  -> Customer Confirmation
  -> Service Closed
```

## 2. Problem Statement (Sri Lankan Context)

After finding and booking a provider, customers often do not know what is happening. They rely on phone calls or WhatsApp messages to ask where the provider is, when they will arrive, whether work has started, and whether the service is complete. These channels provide limited history and make completion confirmation difficult to track.

## 3. Component Objectives

### Primary objectives

- Track service progress.
- Provide status updates.
- Improve customer-provider communication.
- Maintain service history.

### Secondary objectives

- Reduce unnecessary phone calls.
- Improve customer trust.
- Provide a transparent service experience.
- Create service completion records.

## 4. Scope

### Included

- Booking status tracking.
- Provider progress updates.
- Customer notifications.
- Service timeline.
- Completion confirmation.
- Service history.
- Service issue reporting.

### Excluded

- Understanding customer problems, which belongs to Component 1.
- Searching providers, which belongs to Component 2.
- Creating quotations, which belongs to Component 3.
- Processing payments, which is a future feature.

## 5. Responsibilities

### Service status management

Manage states such as `Confirmed`, `Assigned`, `OnTheWay`, `Arrived`, `Started`, `InProgress`, `Completed`, and `Cancelled`. Valid transitions must be enforced by the backend.

### Provider progress tracking

Allow providers to report that they started travelling, reached the location, started work, or completed the service.

### Customer notification

Inform customers about important events, such as: "Your service provider has arrived at your location."

### Service timeline creation

Record an ordered history of status changes with timestamps and the actor responsible.

### Completion confirmation

After the provider reports completion, request customer confirmation and retain the completion record.

## 6. Non-Responsibilities (Important)

This component must not analyse customer problems, search or rank providers, create quotations, process payments, or change booking ownership. It must not allow an unauthorized user or agent to perform a status transition.

## 7. User Roles

### Customer

- View service status.
- Receive notifications.
- Communicate with the provider.
- Confirm completion.
- Report service issues.

### Provider

- Update progress.
- Change status within authorized transitions.
- Send service updates.

### Admin

- Monitor service activities.
- Resolve complaints.
- View service history.
- Review exceptional or disputed completions.

## 8. Agent Architecture

Component 4 uses the shared Agent Foundation.

```text
Booking Created
  -> Service Tracking Agent
  -> Read Booking Memory
  -> Monitor Status
  -> Use Tracking Tools
  -> Send Notifications
  -> Update Memory
  -> Record Metrics
```

## 9. Agent Workflow

```text
Receive Confirmed Booking
  -> Create Tracking Workflow
  -> Monitor Provider Status
  -> Status Changed?
  -> Update Timeline
  -> Notify Customer
  -> Store Memory
  -> Wait for Next Update
  -> Service Completed?
  -> Request Customer Confirmation
  -> Close Workflow
```

## 10. Agent Decision Flow

When a provider changes the status to `Arrived`, the agent determines whether a customer notification is required, sends it through the notification tool, updates the timeline, saves the monitoring data, and waits for the next event.

## 11. Functional Requirements

### FR-001 - Create Service Tracking Workflow

The system shall create a tracking workflow after booking confirmation.

### FR-002 - Maintain Service Status

The system shall maintain the current service status and support `Confirmed`, `Assigned`, `OnTheWay`, `Arrived`, `Started`, `InProgress`, `Completed`, and `Cancelled`.

### FR-003 - Provider Status Updates

Authorized providers shall be able to update their service progress.

### FR-004 - Customer Status Viewing

Authorized customers shall be able to view the current service status.

### FR-005 - Generate Service Timeline

The system shall record each status transition in an ordered timeline.

### FR-006 - Send Notifications

The system shall notify customers about important updates such as provider assignment, arrival, service start, completion, cancellation, and issue escalation.

### FR-007 - Track Estimated Arrival

The system shall support an estimated arrival time and update it when reliable location or provider information changes.

### FR-008 - Store Service History

The system shall maintain completed service records and status history.

### FR-009 - Customer Completion Confirmation

The customer shall be able to confirm service completion or report a problem instead of confirming.

### FR-010 - Handle Service Issues

Customers shall be able to report problems during service, such as "Provider did not arrive". Issues must have an explicit status and history.

## 12. Non-Functional Requirements

- **Performance:** Status updates should normally appear within three seconds where dependencies are available.
- **Reliability:** Service status history and notifications must not be silently lost.
- **Availability:** Tracking should be available throughout the service lifecycle.
- **Security:** Only authorized users may view or update service status.
- **Scalability:** The system should support many active services simultaneously.
- **Maintainability:** Tracking logic must be separated from notification logic.

## 13. Database Requirements

### ServiceTracking

```text
Id
BookingId
CurrentStatus
EstimatedArrival
StartedTime
CompletedTime
CreatedDate
```

### ServiceStatusHistory

```text
Id
TrackingId
PreviousStatus
NewStatus
ChangedBy
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

## 14. Entity Design

A tracking record belongs to a booking and represents the current service state. Each status history record references the tracking record and records the previous status, new status, actor, and timestamp. Notifications belong to a user, while service issues belong to a booking and have their own lifecycle.

Use primary keys, foreign keys, proper relationships, and domain validation for status transitions. The Component 4 owner defines its entity invariants and application use cases.

## 15. API Requirements

### Get Service Status

```http
GET /api/service-tracking/{bookingId}
```

Example response:

```json
{
  "status": "Provider Arrived",
  "estimatedCompletion": "1 hour"
}
```

### Update Service Status

```http
POST /api/service-tracking/update
```

```json
{
  "bookingId": 1001,
  "status": "Started"
}
```

### Get Timeline

```http
GET /api/service-tracking/timeline/{id}
```

### Send Notification

```http
POST /api/notifications
```

### Report Issue

```http
POST /api/service-issues
```

The ASP.NET Core API remains the public boundary and owns authorization and state-transition validation.

## 16. Agent Integration Requirements

Component 4 must use:

- `IAgent` for the service tracking agent.
- `AgentContext` to access booking, provider, and customer information.
- `AgentMemoryService` to store tracking context.
- `ToolExecutor` for notification sending, location updates, and status management.
- `AgentSafetyService` before closing a service or changing important records.
- `AgentMonitoringService` to track status updates, notification success, and agent performance.

The agent must be registered with the orchestrator and return structured outputs validated against shared schemas.

## 17. Required Tools

### NotificationTool

Sends customer updates such as "Provider arrived at your location."

### LocationTrackingTool

Tracks provider movement where the user has granted the required permission and the integration is available.

### StatusUpdateTool

Updates service status after authorization and valid transition checks.

### ETACalculationTool

Calculates expected arrival time using approved location and traffic information.

All tools must implement `IAgentTool`, have clear responsibilities, handle errors, and be invoked through `ToolExecutor`.

## 18. Memory Usage

Store workflow context using `AgentMemoryService`:

```json
{
  "bookingId": 1001,
  "providerStatus": "On The Way",
  "estimatedArrival": "15 minutes"
}
```

Do not create a separate memory system. Memory should contain only the minimum information needed by later workflow steps.

## 19. Safety and Approval Rules

### Unauthorized status changes

A customer cannot change the service to `Completed` on behalf of the provider. Every status update must verify the actor, booking relationship, and allowed transition.

### False completion

A provider should not complete a service without the required workflow evidence and validation.

### Sensitive actions

Approval is required when closing disputes or cancelling confirmed bookings. The agent must not bypass `AgentSafetyService`.

## 20. Monitoring Requirements

Track:

- Number of active services.
- Average completion time.
- Failed services.
- Notification delivery success.
- Agent response time.
- Status update failures and tool errors.

Use `AgentMonitoringService` for execution records and metrics.

## 21. Error Handling

### Provider does not update

Send a reminder notification and raise an exception or admin review signal according to the configured time threshold.

### Customer cannot receive notification

Log the delivery failure, retain the notification state, and provide an alternative visible in-app status where possible.

### Service cancelled

Update the service to `Cancelled`, record the transition, and notify affected users.

### Invalid or unauthorized update

Reject the update, preserve the current state, and record the security or validation event.

## 22. Security Requirements

- Authenticate customers, providers, and administrators.
- Authorize status updates by role, booking ownership, and valid state transition.
- Protect customer location and provider location information.
- Require consent and least privilege for location tracking.
- Prevent clients from changing status through direct database or agent-service access.
- Do not commit credentials, tokens, or private data in fixtures.

## 23. Testing Requirements

### Unit testing

Test status transitions, authorization rules, notification logic, timeline creation, ETA calculation, completion confirmation, and issue handling.

### Integration testing

Test the complete flow:

```text
Booking Agent
  -> Tracking Agent
  -> Notification
  -> Completion
```

### Agent testing

- Provider arrives: customer receives a notification.
- Provider completes service: customer confirmation is requested.
- Unauthorized update: the update is rejected.
- Provider does not update: a reminder or escalation is generated.
- Notification delivery fails: the failure is logged and visible.
- Customer reports an issue: completion is not silently closed.

## 24. Git Development Workflow

Use the branch:

```text
feature/component-4-service-tracking-agent
```

Keep Component 4 implementation within the established Service Tracking feature boundaries. Use commit messages such as:

```text
feat(component4): add service tracking workflow
feat(component4): add notification system
```

Create the pull request according to the repository workflow and target the team's agreed integration branch.

## 25. Expected Deliverables

- Service Tracking Agent.
- Tracking database entities and migration.
- Status workflow.
- Notification system.
- Timeline management.
- Memory integration.
- Safety integration.
- Monitoring integration.
- APIs.
- Unit and integration tests.
- Component documentation.

## 26. Completion Checklist

- [ ] Agent implemented.
- [ ] Tracking workflow completed.
- [ ] Status management completed.
- [ ] Notification system completed.
- [ ] Timeline completed.
- [ ] Memory integrated.
- [ ] Safety applied.
- [ ] Monitoring enabled.
- [ ] APIs completed.
- [ ] Tests completed.
- [ ] Documentation completed.
- [ ] Pull request created and reviewed.
