# Agentic AI Workflow

## Agents

1. Problem Understanding Agent
2. Provider Matching Agent
3. Service Coordination Agent
4. Validation & Safety Agent

## Minimum assessed workflow design

```text
Objective
  ↓
Structured Plan
  ↓
Agent 1
  ↓
Agent 2 + approved tools
  ↓
Agent 4 deterministic validation
  ↓
Human approval pause
  ↓
Agent 3 coordination
  ↓
Auditable result / safe failure
```

## Structured state

Store:

- WorkflowId
- Objective
- Plan
- CurrentStep
- CompletedSteps
- ToolResults
- ValidationResults
- Errors
- RetryCount
- ApprovalStatus
- FinalOutcome
- timestamps

Do not store hidden reasoning.
