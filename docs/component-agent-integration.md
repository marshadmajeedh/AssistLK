# Component Agent Integration Guide

All component agents must use the shared
Agent Foundation.

## Available Services

- `IAgent`
- `AgentContext`
- `AgentMemoryService`
- `ToolExecutor`
- `AgentSafetyService`
- `AgentMonitoringService`

## Example Component Agent

Component 1:

`ProblemUnderstandingAgent`

Responsibilities:

- Understand customer issue
- Classify problem
- Store findings

Should NOT:

- Directly modify bookings
- Ignore safety checks
- Bypass workflow
