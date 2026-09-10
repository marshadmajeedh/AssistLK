# Component 2 Requirements Document

# Provider Matching and Recommendation Agent

This document is for Member 2. It defines the problem, agent behavior, implementation responsibilities, shared foundation integration, and boundaries for Component 2.

## 1. Component Overview

### Component Name

Provider Matching and Recommendation Agent

### Purpose

This component finds and recommends suitable service providers based on customer requirements, service category, location, availability, provider skills, ratings, and previous service performance. It acts as the bridge between the customer's structured problem and suitable providers.

### Example

Component 1 produces:

```json
{
  "serviceType": "Vehicle Repair",
  "problem": "Battery issue",
  "location": "Colombo",
  "urgency": "High"
}
```

Component 2 may return:

```json
{
  "recommendedProviders": [
    {
      "name": "ABC Auto Service",
      "distance": "2km",
      "rating": 4.8,
      "availability": "Available"
    },
    {
      "name": "City Vehicle Care",
      "distance": "4km",
      "rating": 4.5,
      "availability": "Available"
    }
  ]
}
```

## 2. Problem Statement (Sri Lankan Context)

Sri Lankan customers often search Google, ask friends, check social media, and call multiple providers. Existing approaches may not consider the actual problem, provider expertise, availability, distance, ratings, or verification. Traffic and travel distance also make a nearby available provider more practical than one located much farther away.

## 3. Component Objectives

### Primary objectives

- Find suitable providers.
- Match providers with customer needs.
- Rank providers.
- Recommend the best available options.

### Secondary objectives

- Reduce customer search time.
- Improve service quality.
- Improve provider visibility.
- Support multiple service industries.

## 4. Scope

### Included

- Provider searching.
- Provider filtering.
- Provider ranking.
- Availability checking.
- Distance calculation.
- Recommendation generation.
- Provider matching memory.

### Excluded

- Analysing customer problems, which belongs to Component 1.
- Creating quotations or bookings, which belongs to Component 3.
- Tracking service progress, which belongs to Component 4.

## 5. Responsibilities

### Provider search

Search providers based on service category, location, and required skills.

### Provider filtering

Remove unsuitable providers using service type, location range, availability, experience, rating, and verification status.

### Provider ranking

Calculate suitability from approved factors such as distance, availability, rating, and experience. The scoring model must be explicit, testable, and configurable rather than hidden in a controller or prompt.

### Recommendation generation

Explain recommendations at a concise business level, for example: "Recommended because this provider is nearby, currently available, and has experience with battery replacement."

### Provider memory storage

Store the selected provider and recommendation reason in workflow memory for later components.

## 6. Non-Responsibilities (Important)

This component must not analyse or reinterpret the customer's original problem, create quotations, create bookings, assign providers without the required approval, track service progress, or make payment decisions. It must not directly call another agent or access the database without an approved tool.

## 7. User Roles

### Customer

- View recommendations.
- Compare providers.
- Select a provider for the next workflow step.

### Provider

- Maintain a profile.
- Update availability.
- Manage supported services and skills.

### Admin

- Manage providers.
- Review provider verification.
- Monitor recommendation quality and performance.

## 8. Agent Architecture

Component 2 uses the shared Agent Foundation.

```text
Customer Request
  -> Problem Understanding Agent
  -> Workflow Memory
  -> Provider Matching Agent
  -> Provider Tools
  -> Safety Check
  -> Recommendation
  -> Monitoring
```

The Agent Orchestrator controls execution order and passes structured context between components.

## 9. Agent Workflow

```text
Receive Problem Context
  -> Read Workflow Memory
  -> Understand Required Service
  -> Search Providers
  -> Check Availability
  -> Calculate Match Score
  -> Rank Providers
  -> Generate Recommendation
  -> Store Result in Memory
  -> Send Recommendation to Customer
```

## 10. Agent Decision Flow

For a battery issue in Colombo, the agent should confirm that a provider is needed, search vehicle service providers, filter for battery specialists, check availability, rank by distance and rating together with the configured score factors, and return recommendations with reasons.

## 11. Functional Requirements

### FR-001 - Receive Service Requirements

The system shall receive structured problem information from Component 1.

```json
{
  "type": "Plumbing",
  "issue": "Pipe leakage",
  "location": "Kandy"
}
```

### FR-002 - Search Providers

The system shall search providers based on service category.

### FR-003 - Filter Providers

The system shall filter providers based on location, service type, availability, skills, verification, and other approved eligibility rules.

### FR-004 - Check Provider Availability

The system shall determine whether providers are `Available`, `Busy`, or `Offline`.

### FR-005 - Calculate Provider Match

The system shall calculate provider suitability using distance, rating, experience, availability, and the customer's service requirements.

### FR-006 - Rank Providers

The system shall return eligible providers in ranked order, for example ABC Auto Service, City Vehicle Care, and Fast Repair LK.

### FR-007 - Generate Recommendation Explanation

The agent shall explain why each provider was selected without guaranteeing provider quality.

### FR-008 - Store Matching Decision

The system shall save selected provider information and the recommendation reason into workflow memory.

### FR-009 - Support Multiple Services

The agent shall support mechanics, plumbers, electricians, technicians, and additional configured service categories.

## 12. Non-Functional Requirements

- **Performance:** Provider search should normally complete within three seconds where dependencies are available.
- **Scalability:** The system should support thousands of providers.
- **Reliability:** Incorrect recommendations should be minimized through deterministic eligibility and scoring rules.
- **Security:** Provider personal and contact information must be protected.
- **Maintainability:** Matching logic must be separated from API controllers.
- **Availability:** Provider search should be available continuously according to platform availability goals.

## 13. Database Requirements

### Provider

Stores provider information.

```text
Id
Name
Contact
ServiceCategory
Location
Description
Status
```

### ProviderSkill

Stores provider expertise, such as Battery Repair and Engine Repair for ABC Auto.

```text
Id
ProviderId
SkillName
```

### ProviderAvailability

Stores provider availability.

```text
Id
ProviderId
AvailableDate
AvailableTime
Status
```

### ProviderRating

Stores customer feedback.

```text
Id
ProviderId
Rating
Review
Date
```

## 14. Entity Design

Provider skills, availability, ratings, and provider profile data must have proper primary keys, foreign keys, and relationships. The Component 2 owner defines its entity invariants and application use cases. Other components should use published identifiers and contracts rather than changing these entities directly.

## 15. API Requirements

### Search Providers

```http
GET /api/providers/search?service=vehicle&location=Colombo
```

### Get Provider Details

```http
GET /api/providers/{id}
```

### Provider Recommendation

```http
POST /api/provider-agent/recommend
```

Example request:

```json
{
  "service": "Vehicle Repair",
  "problem": "Battery issue",
  "location": "Colombo"
}
```

Example response:

```json
{
  "recommendations": [
    {
      "name": "ABC Auto",
      "score": 95
    }
  ]
}
```

The ASP.NET Core API is the public boundary. Frontend clients must not call the agent service or database directly.

## 16. Agent Integration Requirements

Component 2 must use:

- `IAgent` for agent implementation.
- `AgentContext` to receive workflow information.
- `AgentMemoryService` to store selected providers and recommendation context.
- `ToolExecutor` to execute provider-related tools.
- `AgentSafetyService` before provider-related actions.
- `AgentMonitoringService` to record execution time, success or failure, and tool usage.

The agent must be registered with the orchestrator and return structured output validated against shared schemas.

## 17. Required Tools

### ProviderSearchTool

Searches eligible providers.

### LocationDistanceTool

Calculates distance, such as five kilometres between Colombo and Nugegoda.

### AvailabilityCheckTool

Checks provider availability.

### ProviderRankingTool

Ranks eligible providers using the approved matching score.

All tools must implement `IAgentTool`, have a clear responsibility, be reusable, and handle errors properly.

## 18. Memory Usage

Store the recommendation in `AgentMemoryService`:

```json
{
  "recommendedProvider": "ABC Auto Service",
  "reason": "Closest available battery specialist",
  "distance": "2km"
}
```

Component 3 may use this workflow context later. Component 2 must not create a separate memory system.

## 19. Safety and Approval Rules

The agent must not guarantee provider quality.

Incorrect:

> This is the best mechanic.

Correct:

> This provider is recommended based on availability, distance, and rating.

The agent must not automatically assign a provider without the required customer or workflow approval. It must not make financial decisions or bypass safety checks.

## 20. Monitoring Requirements

Track:

- Number of searches.
- Successful matches.
- Failed searches.
- Average response time.
- Most requested services.
- Tool usage and errors.

Use `AgentMonitoringService` for execution records and metrics.

## 21. Error Handling

### No provider found

Return:

```text
No suitable provider found.
Try changing location or service type.
```

### Provider offline

Exclude offline providers and recommend eligible alternatives where available.

### Database or tool failure

Return a recoverable error, log the failure through the monitoring system, and do not present incomplete results as valid recommendations.

## 22. Security Requirements

- Authenticate users and authorize access to provider data.
- Validate workflow context and agent output.
- Protect provider personal and contact information.
- Apply least-privilege access to provider records.
- Do not expose internal agent endpoints to clients.
- Do not commit credentials, tokens, or private data in fixtures.

## 23. Testing Requirements

### Unit testing

Test ranking algorithms, filtering logic, eligibility rules, distance calculation, availability handling, and recommendation explanations.

### Integration testing

Test the complete flow:

```text
Problem Agent
  -> Provider Agent
  -> Memory
  -> Recommendation
```

### Agent testing

Test vehicle battery issues, home water leakage, electrical power failure, no-provider results, offline providers, ambiguous locations, and tool failures.

## 24. Git Development Workflow

Use the branch:

```text
feature/component-2-provider-agent
```

Keep Component 2 implementation within the established Provider Matching feature boundaries. Use a commit message such as:

```text
feat(component2): add provider matching agent
```

Create the pull request according to the repository workflow and target the team's agreed integration branch.

## 25. Expected Deliverables

- Provider Matching Agent.
- Provider database entities and migration.
- Provider tools.
- Matching algorithm.
- Recommendation API.
- Memory integration.
- Safety integration.
- Monitoring integration.
- Unit and integration tests.
- Component documentation.

## 26. Completion Checklist

- [ ] Agent implemented.
- [ ] Provider database completed.
- [ ] Search tool completed.
- [ ] Ranking logic completed.
- [ ] Availability checking completed.
- [ ] Memory integrated.
- [ ] Safety applied.
- [ ] Monitoring enabled.
- [ ] APIs completed.
- [ ] Tests completed.
- [ ] Documentation completed.
- [ ] Pull request created and reviewed.
