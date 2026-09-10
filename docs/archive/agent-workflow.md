# Agent Workflow

## Execution Flow

1. User sends request.

2. System creates workflow.

3. Orchestrator selects required agent.

4. Agent receives context.

5. Agent uses memory.

6. Agent decides whether tools are required.

7. Safety policy evaluates actions.

8. Approval is requested if required.

9. Action is executed.

10. Execution metrics are recorded.

## Workflow States

Pending

↓

Running

↓

WaitingForApproval

↓

Completed / Failed / Cancelled
