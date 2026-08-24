# Agent Foundation

AssistLK uses specialized agents behind the ASP.NET Core API. Agents support business workflows; they do not replace authorization, validation, or human decisions.

## Agents

| Agent | Responsibility |
|---|---|
| Problem Understanding | Classifies the request, extracts structured details, and identifies missing information. |
| Provider Matching | Ranks verified providers using skills, category, availability, location, and urgency. |
| Service Coordination | Supports quotation, approval, booking, and provider assignment workflows. |
| Validation and Safety | Checks outputs, policy requirements, risk signals, and completion evidence. |

## Runtime flow

1. The API authenticates the user and validates the command.
2. The orchestrator creates a workflow and selects the required agent.
3. The agent receives only the context needed for the task.
4. Tools are invoked through approved, auditable interfaces.
5. Safety checks run before sensitive actions.
6. Human approval is required where policy or business rules require it.
7. The API persists the result, workflow state, and execution evidence.

## Boundaries

- Agents do not call the database directly.
- Agents do not expose public HTTP endpoints to React or Flutter.
- Agents return structured outputs validated against shared schemas.
- Secrets, hidden chain-of-thought, and unnecessary personal data are never persisted.
- Every important action has an actor, timestamp, status, and correlation identifier.
