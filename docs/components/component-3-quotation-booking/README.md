# Component 3: Quotation and Booking

## Responsibility

Owns quotation creation and revisions, customer approval or rejection, booking confirmation, provider assignment, coordination history, and cancellation rules.

## AI contribution

The Service Coordination Agent supports quotation and booking workflow coordination. It cannot approve a quotation, assign a provider, or change a booking without the authorized API operation and required human approval.

## Main integration points

- Backend quotations and bookings
- React staff workflows
- Flutter customer and provider workflows
- Agent service `agents/service_coordination/`

## Completion criteria

Every quotation decision and booking transition has an authorized actor, timestamp, current state, and audit trail.
