# Component 3: Quotation and Booking

> **Scope:** This overview describes planned component responsibilities and completion criteria, not verified implementation status.

## Responsibility

Owns quotation creation and revisions, customer approval or rejection, booking confirmation, provider assignment, coordination history, and cancellation rules.

## AI contribution

The Service Coordination Agent supports quotation and booking workflow coordination. It cannot approve a quotation, assign a provider, or change a booking without the authorized API operation and required human approval.

## Main integration points

- Backend quotations and bookings
- React staff workflows
- Flutter customer and provider workflows
- Agent integration location is not established by this overview; follow the shared internal-service boundary.

## Completion criteria

Every quotation decision and booking transition has an authorized actor, timestamp, current state, and audit trail.
