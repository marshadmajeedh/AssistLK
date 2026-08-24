# Pull Request

## Title Format

Please follow:

`type(component): short description`

Examples:

```text
feat(component2): add provider matching agent
fix(component3): fix quotation comparison logic
docs(component4): update tracking documentation
```

---

# Description

## What does this PR do?

<!--
Explain clearly what was implemented.
Example:

Implemented Provider Matching Agent with provider search,
filtering, ranking and recommendation workflow.
-->

---

# Component Information

## Component

Select one:

- [ ] Component 1 - Smart Service Request & Problem Understanding Agent
- [ ] Component 2 - Provider Matching & Recommendation Agent
- [ ] Component 3 - Quotation & Booking Management Agent
- [ ] Component 4 - Service Tracking & Customer Communication Agent
- [ ] Shared Agent Foundation
- [ ] Documentation
- [ ] Bug Fix

---

# Changes Made

## Added

<!-- List new features, classes, or files. -->

-

## Modified

<!-- List changed files or features. -->

-

## Removed

<!-- List removed code or files if any. -->

-

---

# Architecture Checklist

Before requesting review, confirm:

## Agent Integration

- [ ] Agent implements `IAgent`
- [ ] Agent uses `AgentContext`
- [ ] Agent uses `AgentMemoryService`
- [ ] Agent uses `AgentSafetyService`
- [ ] Agent uses `AgentMonitoringService`

## Tool Integration

- [ ] Agent uses tools through `ToolExecutor`
- [ ] No direct database access from agents
- [ ] No direct external API access from agents

## Database Changes

Does this PR include database changes?

- [ ] No database changes
- [ ] New entities added
- [ ] Existing entities modified
- [ ] Migration added

Migration name:

```text
<!-- Example: AddProviderManagementTables -->
```

---

# API Changes

Does this PR add or change APIs?

- [ ] No API changes
- [ ] New endpoints added
- [ ] Existing endpoints modified

API list:

```text
<!-- Example:
POST /api/providers/recommend
GET /api/providers
-->
```

---

# Testing

## Build

- [ ] `dotnet build` successful

## Unit Tests

- [ ] Added unit tests
- [ ] Existing tests passed
- [ ] No tests required

## Integration Tests

- [ ] Tested workflow integration
- [ ] Tested database operations
- [ ] Tested agent execution

---

# AI Agent Testing

If this PR contains an agent:

## Tested Scenarios

Example:

```text
Input:

"My car battery died in Colombo"

Expected:

- Vehicle service identified
- Battery issue detected
- Provider workflow started
```

Test cases:

1.

2.

---

# Memory Integration

Does this component use workflow memory?

- [ ] No memory required
- [ ] Stores memory
- [ ] Reads memory

Memory data:

Example:

```json
{
	"problem": "Battery issue",
	"location": "Colombo"
}
```

---

# Safety Review

Does this PR perform sensitive actions?

- [ ] No sensitive actions
- [ ] Approval required
- [ ] Safety policy updated

Actions requiring approval:

```text
<!-- Example:
CREATE_BOOKING
MAKE_PAYMENT
-->
```

---

# Monitoring

Does this PR record agent metrics?

- [ ] No monitoring required
- [ ] Execution metrics added
- [ ] Tool usage tracked
- [ ] Error logging added

---

# Documentation

Updated documentation?

- [ ] No documentation changes
- [ ] Component documentation updated
- [ ] README updated
- [ ] API documentation updated

---

# Screenshots / Evidence

For UI changes, add screenshots here.

For API changes, add request and response examples.

---

# Reviewer Notes

Anything reviewers should know?
