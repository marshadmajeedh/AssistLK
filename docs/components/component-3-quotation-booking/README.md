# Component 3: Quotation and Booking

> **Scope:** This overview separates implemented ASP.NET/Python workflow integration from remaining component responsibilities.

## Responsibility

Owns quotation creation and revisions, customer approval or rejection, booking confirmation, provider assignment, coordination history, and cancellation rules.

## AI contribution

The Service Coordination Agent supports quotation and booking workflow coordination. It cannot approve a quotation, assign a provider, or change a booking without the authorized API operation and required human approval.

## Main integration points

- ASP.NET quotation endpoints start and resume the Python workflow. The start response includes a `threadId`, which the authenticated customer supplies when approving or rejecting.
- ASP.NET validates the resumed quotation ID and decision, then owns quotation status changes and booking persistence.
- React staff workflows
- Flutter customer and provider workflows

The Python checkpoint is currently in memory; pending workflows are lost if the service restarts. Python service setup and endpoint contracts are documented in the [Quotation & Booking Agent README](../../../agent-services/quotation-booking-agent/README.md).

## Completion criteria

Every quotation decision and booking transition has an authorized actor, timestamp, current state, and audit trail.
