# Component 4: Service Tracking

> **Scope:** This overview describes planned component responsibilities and completion criteria, not verified implementation status.

## Responsibility

Owns assigned and progress states, provider location or status updates, completion evidence, validation, feedback, and service history.

## AI contribution

The Validation and Safety Agent checks completion evidence, policy signals, and exceptional outcomes. It can flag work for review but cannot silently mark a disputed or unsafe service complete.

## Main integration points

- Backend tracking, completion, and feedback use cases
- React admin monitoring
- Flutter provider and customer tracking workflows
- Agent integration location is not established by this overview; follow the shared internal-service boundary.

## Completion criteria

Progress is recorded as an ordered event history, completion is validated, exceptions are reviewable, and feedback is linked to the completed service.
