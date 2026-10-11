# Component 1 Requirements Document

> **Requirement versus implementation:** This document preserves assignment/conceptual requirements. Current C1 is Python-only behind the ASP.NET adapter. [Domain implementation](README.md) and [Python service](../../../agent-services/problem-understanding-agent/README.md) define current behavior. Workflow diagrams describe responsibility, not direct Python database access.

# Smart Service Request & Problem Understanding Agent

## 1. Component Overview

### Component Name

Smart Service Request & Problem Understanding Agent

### Purpose

This component understands a customer's service problem and converts an unclear human request into structured information that the AssistLK platform can use. The agent is the first intelligent interaction point between the customer and the system.

### Example

Customer input:

> My vehicle suddenly stopped while travelling near Battaramulla. The engine makes a strange sound.

Structured output:

```json
{
  "serviceCategory": "Vehicle Repair",
  "problemType": "Possible Engine Issue",
  "location": "Battaramulla",
  "urgency": "High",
  "additionalInformation": "Engine making unusual sound"
}
```

## 2. Problem Statement (Sri Lankan Context)

Sri Lankan customers often cannot clearly describe their problem, must search manually across multiple channels, and may contact the wrong type of provider. A request such as "Vehicle is not working" does not identify whether the cause is the battery, engine, fuel system, or electrical system. Initial problem analysis helps route the request to an appropriate service category and provider.

## 3. Component Objectives

### Primary objectives

- Understand customer service requests.
- Identify the required service category.
- Extract important information.
- Classify urgency.
- Ask follow-up questions.
- Prepare structured service request data.

### Secondary objectives

- Reduce customer effort.
- Improve provider matching accuracy.
- Improve quotation accuracy.
- Create better workflow context for later agents.

## 4. Scope

### Included

- Receive customer requests.
- Understand natural language input.
- Analyse service problems.
- Identify service categories.
- Extract important details.
- Ask for missing information.
- Store information in workflow memory.
- Create a structured problem summary.

### Excluded

- Finding providers, which belongs to Component 2.
- Creating quotations or bookings, which belongs to Component 3.
- Tracking service progress, which belongs to Component 4.
- Making payments.

## 5. Responsibilities

The Problem Understanding Agent is responsible for:

- Converting human language into service and problem information.
- Extracting service type, problem description, location, urgency, preferred time, and additional details.
- Classifying requests such as vehicle repair, plumbing, electrical, and appliance repair.
- Generating follow-up questions when required information is missing.
- Creating workflow memory for future components.
- Producing a concise problem summary before handoff.

## 6. Non-Responsibilities (Important)

This component must not search for providers, rank providers, create quotations, approve or create bookings, track service progress, make payments, or directly call another agent. These responsibilities belong to other components or shared platform services.

## 7. User Roles

### Customer

- Submit service problems.
- Answer follow-up questions.
- Confirm extracted information.

### Provider

Providers do not directly interact with this component during problem understanding.

### Admin

- View service requests.
- Monitor agent performance.
- Review unclear or exceptional requests.

## 8. Agent Architecture

Component 1 uses the shared Agent Foundation.

```text
Customer
  -> Problem Understanding Agent
  -> Agent Orchestrator
  -> Agent Memory Service
  -> Problem Analysis Tools
  -> Safety Policy
  -> Monitoring
```

Agents must respect the repository architecture and use approved tools for external actions.

## 9. Agent Workflow

```text
Customer Request
  -> Create Workflow
  -> Problem Understanding Agent
  -> Read Existing Memory
  -> Analyse Request
  -> Extract Information
  -> Need More Information?
  -> Ask Customer Questions when required
  -> Update Memory
  -> Generate Problem Summary
  -> Pass structured context to Component 2
```

## 10. Agent Decision Flow

For an input such as "My vehicle stopped suddenly", the agent should determine whether the service category, location, and urgency are known. It should identify a vehicle service when possible, ask for missing location or urgency information, and complete the request only when the minimum required context is available.

The agent must communicate uncertainty rather than present an unverified diagnosis as fact.

## 11. Functional Requirements

### FR-001 - Receive Service Request

The system shall allow customers to submit service problems using natural language.

### FR-002 - Understand User Input

The agent shall analyse customer descriptions such as "My fridge is leaking water".

### FR-003 - Identify Service Category

The agent shall classify requests into categories including vehicle service, plumbing, electrical, appliance repair, and other supported services.

### FR-004 - Extract Problem Information

The system shall extract the problem description, location, service type, and urgency when available.

### FR-005 - Generate Follow-up Questions

The agent shall request missing information using clear customer-facing questions.

### FR-006 - Create Structured Service Request

The system shall convert customer input into structured data.

```json
{
  "category": "Plumbing",
  "issue": "Water leakage",
  "location": "Colombo"
}
```

### FR-007 - Store Information in Memory

The ASP.NET application workflow shall store the agent’s structured findings using `AgentMemoryService`.

### FR-008 - Provide Problem Summary

The system shall generate a summary before moving to the next component.

### FR-009 - Support Multiple Service Domains

The agent shall support vehicle, home, and technical services, with categories extensible without a major redesign.

## 12. Non-Functional Requirements

- **Performance:** Normal agent requests should complete within five seconds where external dependencies are available.
- **Scalability:** New service categories should be addable without major redesign.
- **Maintainability:** Code must follow the existing Clean Architecture.
- **Reliability:** Unclear, incomplete, and unsupported inputs must be handled gracefully.
- **Security:** Customer information must be protected and access controlled.
- **Availability:** The component should be available whenever customers submit requests.

## 13. Database Requirements

### ServiceRequest

Stores customer requests. Required fields include:

```text
Id
CustomerId
Category
Description
Location
Urgency
Status
CreatedDate
```

### ProblemAnalysis

Stores agent analysis. Required fields include:

```text
Id
ServiceRequestId
DetectedProblem
Confidence
AgentName
CreatedDate
```

Entity names use PascalCase and relationships must use primary keys and foreign keys.

## 14. Entity Design

`ProblemAnalysis` belongs to a `ServiceRequest` and records the structured result of an analysis. Analysis records should preserve the agent name, confidence, creation time, and request relationship. Domain invariants and state transitions belong in the backend domain and application layers.

Avoid storing unsupported diagnostic certainty. Store a concise business rationale and validation metadata where auditability requires it, not hidden chain-of-thought.

## 15. API Requirements

### Create Service Request

```http
POST /api/service-requests
```

Example request:

```json
{
  "description": "My vehicle stopped",
  "location": "Colombo"
}
```

### Get Request Status

```http
GET /api/service-requests/{id}
```

### Agent Analysis

```http
POST /api/problem-agent/analyse
```

The ASP.NET Core API remains the public boundary. React and Flutter must not call the agent service or database directly.

## 16. Agent Integration Requirements

Component 1 must use:

- `IAgent` for the ASP.NET external agent adapter.
- `AgentContext` for workflow information.
- `AgentMemoryService` for storing extracted knowledge.
- Python functions for C1 deterministic tools; shared C# ToolExecutor applies only to C# tools.
- `AgentSafetyService` before risky actions.
- `AgentMonitoringService` for performance and execution tracking.

The agent must be registered with the orchestrator and return structured output validated against the shared schema.

## 17. Required Tools

### ProblemClassificationTool

Classifies the service category. For example, "No electricity in house" should produce an electrical service classification.

### LocationExtractionTool

Extracts and normalizes location information from customer input.

### ServiceKnowledgeTool

Provides safe, basic service knowledge, such as possible causes of a battery failure, without claiming a confirmed diagnosis or providing dangerous repair instructions.

These are conceptual tool responsibilities. Current implementations are Python `problem_classification.py`, `location_extraction.py`, and `service_knowledge.py`; they do not implement C# `IAgentTool`. Location extraction is not Nominatim reverse geocoding. Tools must have clear responsibilities and handle errors.

## 18. Memory Usage

Persisted workflow information is owned by ASP.NET through `AgentMemoryService`; Python state remains request-scoped.

Example stored memory:

```json
{
  "serviceType": "Vehicle Repair",
  "problem": "Battery Issue",
  "location": "Colombo",
  "urgency": "High"
}
```

Component 2 may read this workflow context to find suitable providers. Component 1 must not create a separate memory solution.

## 19. Safety and Approval Rules

The agent must not provide dangerous repair instructions, guarantee a diagnosis, make financial decisions, or perform sensitive actions. It should use uncertainty-aware language such as "Possible engine-related issue. A professional inspection is recommended."

Any sensitive operation must pass through `AgentSafetyService` and the required approval workflow. Component 1 does not create bookings or payments.

## 20. Monitoring Requirements

Track:

- Number of requests.
- Successful analyses.
- Failed analyses.
- Average response time.
- Most common problems.
- Agent name and execution status.
- Tool usage and errors.

Use `AgentMonitoringService` for execution records and metrics.

## 21. Error Handling

### Empty request

Return a customer-friendly response such as:

```text
Please describe your problem.
```

### Unknown problem

Ask for more information:

```text
More information is required.
```

### System failure

Return a recoverable error, log the failure, and notify the monitoring system. Do not create a partial workflow state without recording its status.

## 22. Security Requirements

- Authenticate customers and authorize request access.
- Validate all input and agent output.
- Protect customer location and other personal information.
- Do not expose internal agent endpoints to clients.
- Do not allow agents to bypass API authorization or access the database directly.
- Do not commit credentials, tokens, or personal data in fixtures.

## 23. Testing Requirements

### Unit testing

Test classification logic, information extraction, follow-up question generation, uncertainty handling, and memory saving.

### Integration testing

Test the complete flow:

```text
Customer Request
  -> Agent
  -> Memory
  -> Workflow
```

### Agent testing

Use varied inputs such as:

- "My car doesn't start"
- "My pipe is leaking"
- "My AC is not working"

Also test empty, ambiguous, unsupported, and unsafe requests.

## 24. Git Development Workflow

Use the branch:

```text
feature/component-1-problem-agent
```

Keep Component 1 implementation within the established Problem Understanding feature boundaries. Use commit messages such as:

```text
feat(component1): add problem classification agent
```

Create the pull request according to the repository workflow and target the team's agreed integration branch.

## 25. Expected Deliverables

- Problem Understanding Agent.
- Database entities and migration.
- Public APIs.
- Agent tools.
- Memory integration.
- Safety integration.
- Monitoring integration.
- Unit and integration tests.
- Component documentation.

## 26. Completion Checklist

- [ ] Agent implemented.
- [ ] Agent registered.
- [ ] Workflow integration completed.
- [ ] Memory working.
- [ ] Tools working.
- [ ] Safety applied.
- [ ] Monitoring enabled.
- [ ] Database completed.
- [ ] API completed.
- [ ] Tests completed.
- [ ] Documentation completed.
- [ ] Pull request created and reviewed.

## Current clarification requirement

At most two persisted rounds are allowed. Round 1 answers are re-analyzed; Round 2 answers must also be re-analyzed. No Round 3 is created. If still insufficient, the customer improves the main description. ASP.NET owns these rules; Python returns structured analysis and questions.
