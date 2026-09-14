# Component 1: Service Requests and Problem Understanding

## Responsibility and boundaries

Component 1 owns customer service requests, problem analysis, clarification history, location context, lifecycle validation, and the ReadyForMatching handoff. It does not perform provider matching, quotation, booking, scheduling, tracking, completion, or feedback.

Problem understanding uses the Python-only [C1 service](../../../agent-services/problem-understanding-agent/README.md). The [requirements](requirements.md) preserve assignment intent. Provider setup and the complete graph/contract belong in the service README rather than this domain guide.

## Runtime integration

```text
Customer -> ASP.NET -> ProblemUnderstandingWorkflowService
 -> AgentOrchestrator -> AgentRegistry -> ExternalProblemUnderstandingAgentAdapter
 -> ProblemUnderstandingHttpClient -> Python FastAPI -> LangGraph
```

The adapter is registered as `ProblemUnderstandingAgent`. ASP.NET checks customer ownership, loads context and answered history, starts analysis, validates structured output, persists results, and records execution evidence. Python has no direct database ownership.

## Domain data and CategoryHint

`ServiceRequest` holds the customer description, final category, urgency, status, location text/coordinates/provenance, and timestamps. `ProblemAnalysis` stores authoritative persisted analysis. `ServiceRequestClarification` stores ordered questions, answers, rounds, and supersession state. Consult the [DTO](../../../backend/src/AssistLK.Application/ServiceRequests/DTOs/ServiceRequestResponse.cs) for the current response shape, including `LatestAnalysis` and `Clarifications`.

`CategoryHint` is the customer's category preference, not the final classification. Python receives it as non-authoritative context. Display final `Category` as authoritative; label the hint as customer preference. Neither a preference nor GPS acquisition determines the final AI category.

## Lifecycle

```text
Created -> Analyzing -> Analyzed -> ReadyForMatching
                    -> AwaitingInformation -> Analyzing
```

Cancellation and editing remain controlled by the current [ServiceRequestService](../../../backend/src/AssistLK.Application/Services/ServiceRequestService.cs) rules. Analysis is allowed only under lifecycle and clarification preconditions. Transport/service failures restore the valid pre-analysis state through workflow recovery rather than leaving the request stuck in Analyzing. There is no native C# agent fallback.

Successful degraded Python output differs from invocation failure: it can persist an Unclassified, low-confidence analysis requiring more information. Domain validation remains authoritative.

## Clarification

Initial analysis can create Round 1. Answering it permits re-analysis, which can create Round 2. Answering Round 2 permits final re-analysis: reaching two rounds does not block this analysis. No Round 3 is created. If information remains insufficient, the customer is directed to improve the main description.

The current actionable round must be answered before re-analysis. ASP.NET passes answered history ordered by round and sequence; Python returns questions but cannot create persisted rounds or submit answers. Superseded questions remain history rather than active unanswered requirements.

## Smart Location

```text
Flutter GPS -> ASP.NET POST /api/location/reverse-geocode -> OpenStreetMap Nominatim
```

Reverse geocoding is deterministic infrastructure, not an AI agent tool. Location text, optional coordinates, and source/provenance remain backend-controlled request data. Python location extraction only normalizes supplied context; it never calls Nominatim or invents coordinates. A human-readable location is required for ReadyForMatching; GPS coordinates are not an unconditional prerequisite.

## Public customer API

All routes below require Customer authorization and applicable request ownership. See [ServiceRequestsController](../../../backend/src/AssistLK.Api/Controllers/ServiceRequestsController.cs).

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/service-requests` | Create request |
| GET | `/api/service-requests/my` | List owned requests |
| GET | `/api/service-requests/{id}` | Owned detail with latest analysis and clarifications |
| PUT | `/api/service-requests/{id}` | Update under lifecycle rules |
| POST | `/api/service-requests/{id}/cancel` | Cancel under lifecycle rules |
| POST | `/api/service-requests/{id}/analyze` | Analyze or re-analyze |
| POST | `/api/service-requests/{id}/clarifications/answers` | Submit clarification answers |
| POST | `/api/service-requests/{id}/ready-for-matching` | Confirm readiness under domain preconditions |

Customer location reverse-geocoding uses the separate location controller. Frontends do not call Python execution endpoints directly.

## ReadyForMatching and C1-to-C2 handoff

The backend requires Analyzed status, a classified category, known urgency, non-empty location text, no active unanswered clarifications, and a latest analysis with confidence greater than zero and at most one. The authorized customer operation marks readiness; analysis alone does not mark it ready.

`IServiceRequestService.GetReadyForMatchingAsync` returns a read-only `ServiceRequestForMatchingResponse` only for eligible requests. This application-service contract is not a Provider-authorized HTTP endpoint. The Customer detail route must not be documented as cross-role access. See [component contracts](../../api/component-contracts.md).

## Memory, safety, and monitoring

ASP.NET persists concise structured memory scoped to workflow execution: `problem.category`, `problem.summary`, `problem.urgency`, `problem.confidence`, `problem.needs_more_information`, `problem.follow_up_questions`, `problem.location`, and `problem.additional_information`. Python graph state is request-scoped, not persisted memory. Hidden reasoning is neither persisted nor exposed to clients.

Application safety checks govern `ANALYZE_PROBLEM`; Python guards analysis content and ASP.NET validates its semantics again. Uncertainty-aware summaries and bounded questions do not guarantee a diagnosis or comprehensive sanitization. Customer-facing branding is **AssistLK AI**.

Admin-only read monitoring uses `GET /api/admin/service-requests` (optional status/category/urgency filters), `GET /api/admin/service-requests/{id}`, and backend agent monitoring APIs. Admin request monitoring does not mutate customer requests. The workflow records status, timing, agent identity, failures, and tool usage. Internal provider metadata must not become customer-facing prompts or reasoning traces.

## Verification

See the [testing guide](../../development/testing-guide.md) for exact .NET, Python, Flutter, and React commands. Normal .NET tests fake the Python client and require no running Python service. Real PostgreSQL fixtures use dedicated test databases and destructive-operation guards; never point reset/drop tests at application data.

Coverage includes ownership/authentication, lifecycle, persistence, clarification rounds, adapter failures/recovery, monitoring, and frontend behavior. Current passing totals are intentionally not embedded in this guide. Live inference requires explicit execution evidence separate from mocked tests and health checks.
