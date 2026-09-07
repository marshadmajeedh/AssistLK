# Component 1: Smart Service Request & Problem Understanding Agent

## 1. Overview & Purpose

The **Smart Service Request & Problem Understanding Agent** is the initial intelligence gateway of the AssistLK platform. It converts unstructured, ambiguous, and colloquial natural language service requests submitted by Sri Lankan customers into structured, validated, and categorized service requests ready for automated provider matching.

In Sri Lanka, customers seeking home and vehicle services frequently struggle to describe the technical root cause of their issues (e.g., describing a dead battery, failed alternator, or broken starter motor simply as *"vehicle won't start near Battaramulla"*). The Problem Understanding Agent bridges this communication gap by:
1. Identifying the canonical service category.
2. Formulating a precise, uncertainty-aware technical summary.
3. Extracting geographic locations and local landmarks across Sri Lankan districts.
4. Evaluating urgency levels with domain-specific rationale.
5. Identifying missing information and generating professional follow-up questions.
6. Persisting structured data and workflow memory for downstream platform components.

```
+-------------------------------------------------------------------------------+
|                                  AssistLK                                     |
|                                                                               |
|  [ Customer Request ]                                                         |
|         |                                                                     |
|         v                                                                     |
|  +-------------------------------------------------------------------------+  |
|  |             COMPONENT 1: Problem Understanding Agent                    |  |
|  |   - Analyzes raw problem description and location                       |  |
|  |   - Classifies canonical category & evaluates urgency                   |  |
|  |   - Generates follow-up clarification questions if ambiguous            |  |
|  |   - Enforces lifecycle state machine & persists to PostgreSQL           |  |
|  +-------------------------------------------------------------------------+  |
|         |                                                                     |
|         v (ServiceRequestForMatchingResponse)                                 |
|  +-------------------------------------------------------------------------+  |
|  |             COMPONENT 2: Provider Matching Agent (Downstream)           |  |
|  +-------------------------------------------------------------------------+  |
+-------------------------------------------------------------------------------+
```

---

## 2. Core Responsibilities

- **Natural Language Parsing & Interpretation**: Ingest unstructured customer inputs including Sinhala/Tamil transliterations and local colloquialisms.
- **Problem Classification**: Map requests into AssistLK's canonical categories: `Plumbing`, `Electrical`, `Vehicle Repair`, `Appliance Repair`, or `Unclassified`.
- **Entity & Location Extraction**: Extract Sri Lankan towns, districts, postal areas, and prominent landmarks.
- **Urgency Assessment**: Score urgency (`Low`, `Medium`, `High`, `Emergency`) based on safety hazards, potential property damage, and customer mobility impact.
- **Clarification & Question Generation**: Determine if critical information is missing (`NeedsMoreInformation = true`) and formulate helpful, targeted follow-up questions.
- **Domain State Persistence**: Persist `ServiceRequest` and `ProblemAnalysis` entities in PostgreSQL with foreign key relationships, audit timestamps, and concurrency protection.
- **Workflow Memory Management**: Write strictly 8 standard memory keys into the shared agent memory store.
- **Lifecycle State Machine Enforcement**: Enforce strict status transitions (`Created` -> `Analyzing` -> `AwaitingInformation` / `Analyzed` -> `ReadyForMatching`).
- **Secure REST API**: Provide authenticated API endpoints with JWT claim validation and customer data isolation.
- **Component 2 Handoff**: Deliver clean, validated handoff payloads (`ServiceRequestForMatchingResponse`).

---

## 3. Strict Non-Responsibilities (System Boundaries)

To preserve architectural modularity and team ownership boundaries, Component 1 strictly forbids the following operations:
- **No Provider Matching or Ranking**: Locating, filtering, and scoring service providers is exclusively the responsibility of **Component 2 (Provider Matching Agent)**.
- **No Quotations or Bookings**: Requesting quotes, evaluating bids, customer quote approvals, and booking scheduling belong exclusively to **Component 3 (Quotation & Booking Agent)**.
- **No Service Tracking or Progress Updates**: Real-time status tracking, job milestones, arrival alerts, and completion verification belong exclusively to **Component 4 (Service Tracking Agent)**.
- **No Financial Transactions**: Handling payments, escrow, or invoices.
- **No Direct Agent-to-Agent Invocation**: Agents do not directly call other agents. Handoff is decoupled via PostgreSQL domain entities and shared workflow memory.
- **No Dangerous DIY Instructions**: The agent never instructs customers to dismantle high-voltage equipment, pressurized gas lines, or vehicle brake systems.

---

## 4. System Architecture & Layers

Component 1 follows Clean Architecture principles across the AssistLK backend solution:

```
+------------------------------------------------------------------------------------+
| AssistLK.Api                                                                       |
| - ServiceRequestsController (POST, GET, PUT, /cancel, /analyze, /ready-for-matching) |
| - JWT Bearer Authentication & Customer Claims Extraction                           |
+------------------------------------------------------------------------------------+
                                      |
                                      v
+------------------------------------------------------------------------------------+
| AssistLK.Application                                                               |
| - Interfaces: IServiceRequestService                                               |
| - Services: ServiceRequestService, ProblemUnderstandingWorkflowService            |
| - DTOs: CreateServiceRequestRequest, ServiceRequestResponse, ServiceRequestForMatching|
| - Validation: ServiceRequestValidator                                              |
+------------------------------------------------------------------------------------+
                   |                                                 |
                   v                                                 v
+------------------------------------+   +-------------------------------------------+
| AssistLK.Agents                    |   | AssistLK.Domain                           |
| - ProblemUnderstandingAgent        |   | - Entities: ServiceRequest, ProblemAnalysis|
| - Tools:                           |   | - Enums: ServiceCategory,                 |
|     ProblemClassificationTool      |   |          ServiceRequestStatus,            |
|     LocationExtractionTool         |   |          UrgencyLevel                     |
|     ServiceKnowledgeTool           |   | - Repositories: IServiceRequestRepository,|
| - ToolExecutor Integration         |   |                 IProblemAnalysisRepository|
+------------------------------------+   +-------------------------------------------+
                   \                                                 /
                    \                                               /
                     v                                             v
+------------------------------------------------------------------------------------+
| AssistLK.Infrastructure                                                            |
| - ApplicationDbContext & EF Core Entity Configurations (Fluent API)                |
| - Repositories: ServiceRequestRepository, ProblemAnalysisRepository                |
| - PostgreSQL Migration: 20260907050809_AddServiceRequestAndProblemAnalysis        |
+------------------------------------------------------------------------------------+
```

---

## 5. Domain Model & Schema

### `ServiceRequest` Entity
Represents the customer's high-level service request lifecycle.
- `Id` (`Guid`, PK): Unique identifier.
- `CustomerId` (`Guid`, FK -> `Users.Id`): The customer who owns this request. Enforced with `OnDelete: Restrict`.
- `Title` (`string`, max 200 chars): Concise summary of the service requirement.
- `Description` (`string`, text): Detailed problem description provided by the customer.
- `Category` (`ServiceCategory` enum stored as string, max 50 chars): One of `Plumbing`, `Electrical`, `Vehicle Repair`, `Appliance Repair`, `Unclassified`.
- `Status` (`ServiceRequestStatus` enum stored as string, max 50 chars): Current lifecycle state (`Created`, `Analyzing`, `AwaitingInformation`, `Analyzed`, `ReadyForMatching`, `Cancelled`).
- `Location` (`string`, max 200 chars): Service location or breakdown spot.
- `Urgency` (`UrgencyLevel` enum stored as string, max 20 chars): `Low`, `Medium`, `High`, `Emergency`.
- `CreatedUtc` (`DateTime`, UTC): Creation timestamp.
- `UpdatedUtc` (`DateTime?`, UTC): Timestamp of last modification.
- Navigation: `ICollection<ProblemAnalysis> ProblemAnalyses` (1-to-many relationship with `OnDelete: Cascade`).

### `ProblemAnalysis` Entity
Stores immutable, structured analysis records produced by the Problem Understanding Agent.
- `Id` (`Guid`, PK): Unique identifier for this analysis run.
- `ServiceRequestId` (`Guid`, FK -> `ServiceRequests.Id`): Associated service request. Enforced with `OnDelete: Cascade`.
- `DetectedCategory` (`string`, max 50 chars): The AI-classified service category.
- `ProblemSummary` (`string`, text): Uncertainty-aware problem synthesis.
- `ConfidenceScore` (`decimal(5,2)`): Confidence score between `0.00` and `1.00`.
- `UrgencyAssessment` (`string`, max 20 chars): Recommended urgency level.
- `NeedsMoreInformation` (`bool`): Flag indicating if vital details are missing.
- `FollowUpQuestionsJson` (`string`, JSON text): Serialized array of clarifying questions.
- `ExtractedDetailsJson` (`string`, JSON text): Serialized dictionary of structured problem attributes.
- `CreatedUtc` (`DateTime`, UTC): Execution timestamp.

---

## 6. Problem Understanding Agent Flow & Tools

When `POST /api/service-requests/{id}/analyze` is invoked:

1. **Context Initialization**: `ProblemUnderstandingWorkflowService` retrieves the request and initializes workflow execution, setting request status to `Analyzing`.
2. **Tool Execution via `IToolExecutor`**:
   - **`ProblemClassificationTool`**: Matches domain patterns, detects canonical service category (`Plumbing`, `Electrical`, `Vehicle Repair`, `Appliance Repair`), evaluates ambiguity, and calculates classification confidence.
   - **`LocationExtractionTool`**: Scans input text for Sri Lankan districts, major cities (Colombo, Kandy, Galle, Gampaha, Kurunegala, etc.), suburbs, and landmark tokens.
   - **`ServiceKnowledgeTool`**: Cross-references symptoms with domain safety guidelines. Detects emergency safety triggers (gas leaks, sparks, total brake failure) and formulates uncertainty-aware summaries without hazardous DIY advice.
3. **Synthesis & Reasoning**: `ProblemUnderstandingAgent` aggregates tool results into `ProblemUnderstandingOutput`.
   - If confidence is below threshold (< 0.60) or vital technical/location details are missing, `NeedsMoreInformation` is set to `true`, and targeted follow-up questions are populated.
4. **Memory Synchronization**: Writes the 8 standardized keys into `AgentMemories`.
5. **State Finalization**:
   - If `NeedsMoreInformation == true`: Transition to `AwaitingInformation`.
   - If information is complete: Transition to `Analyzed`.
   - A new `ProblemAnalysis` entity is persisted in PostgreSQL.

---

## 7. Workflow Memory Contract

To maintain seamless interoperability across all 4 AssistLK agents, Component 1 writes strictly eight standard keys into `AgentMemories` scoped to the workflow execution:

| Memory Key | Format / Type | Example Value | Description |
| :--- | :--- | :--- | :--- |
| `problem.category` | String | `"Vehicle Repair"` | Canonical classified category |
| `problem.summary` | String | `"Possible alternator failure resulting in battery discharge"` | Technical synthesis |
| `problem.urgency` | String | `"High"` | Assessed urgency level |
| `problem.confidence` | String Decimal | `"0.85"` | Classification confidence (0.00 - 1.00) |
| `problem.needs_more_information` | String Boolean | `"false"` | Indicates if clarification is required |
| `problem.follow_up_questions` | JSON Array String | `["Is the battery warning light on?"]` | Follow-up questions |
| `problem.location` | String | `"Battaramulla, Colombo"` | Extracted geographical location |
| `problem.additional_information` | JSON Object String | `{"vehicleType":"Car","symptom":"Clicking"}` | Key-value technical attributes |

> **Contract Guarantees:**
> - No raw personal contact data (passwords, NIC numbers) is written to memory.
> - No unformatted internal thoughts or chain-of-thought tokens are stored.
> - Keys are deterministic and indexed for downstream retrieval by Component 2.

---

## 8. AI Safety & Risk Policy

- **Safety Policy Definition**: Registered in the agent safety framework as `ANALYZE_PROBLEM`.
- **Risk Level**: `RiskLevel.Low`.
- **Human Approval Requirement**: `RequiresApproval = false`. Because problem analysis is advisory, read-only intelligence that prepares data rather than committing financial transactions or bookings, automated execution is safe.
- **Uncertainty-Aware Phrasing**: The agent uses cautious, objective phrasing (*"Possible engine issue"*, *"Suspected pipe leakage"*, *"Customer reports strange noise"*) rather than definitive mechanical diagnoses.
- **Safety Precaution Over DIY**: The agent never suggests dangerous customer troubleshooting (e.g., opening electric distribution boards, working under unchocked vehicles, opening hot radiator caps). For emergencies, it flags urgency and advises waiting for certified technicians.

---

## 9. ServiceRequest State Machine & Lifecycle Rules

```
                      +-------------------+
                      |      Created      |
                      +-------------------+
                                |
                                | analyze (Agent triggered)
                                v
                      +-------------------+
         +----------> |     Analyzing     | <---------+
         |            +-------------------+           |
         |                      |                     |
         |                      | workflow complete   |
         |                      v                     |
         |      +-------------------------------+     |
         |      | NeedsMoreInfo?                |     |
         |      +-------------------------------+     |
         |             /                \             |
         |     Yes    /                  \   No       |
         |           v                    v           |
         |  +--------------------+    +--------------+|
         |  |AwaitingInformation |    |   Analyzed   ||
         |  +--------------------+    +--------------+|
         |           |                        |       |
         | customer  |                        | mark  |
         | updates   |                        | ready |
         +-----------+                        v       |
                                      +------------------+
                                      | ReadyForMatching | (Component 2 Handoff)
                                      +------------------+

  * Note: 'Cancelled' can be reached from Created, Analyzing, AwaitingInformation, or Analyzed.
  * 'Cancelled' and 'ReadyForMatching' are terminal states within Component 1.
```

### Transition Rules:
1. **`Created` -> `Analyzing`**: Initiated when AI analysis starts.
2. **`Analyzing` -> `AwaitingInformation`**: Set when `NeedsMoreInformation == true`.
3. **`Analyzing` -> `Analyzed`**: Set when classification and details are sufficient for downstream processing.
4. **`AwaitingInformation` -> `Analyzing`**: Re-analysis triggered after customer provides additional details.
5. **`Analyzed` -> `ReadyForMatching`**: Explicit handoff transition indicating the request is approved for Component 2 matching.
6. **Forbidden Transitions**:
   - Direct `Created` -> `ReadyForMatching` (Must undergo problem understanding first; returns `409 Conflict`).
   - `AwaitingInformation` -> `ReadyForMatching` (Cannot match while critical information is missing; returns `409 Conflict`).
   - `ReadyForMatching` -> any prior state (Locked once handed off).
   - `Cancelled` -> any state (Terminal state; cannot be reopened or edited).

---

## 10. Authenticated REST API Reference

All endpoints are hosted under `/api/service-requests` and require a valid JWT Bearer token in the `Authorization` header.

### Identity & Security Model
- Customer identity is extracted exclusively from the JWT token's `ClaimTypes.NameIdentifier` claim.
- Client payloads cannot specify or override the `CustomerId`.
- Cross-customer access is forbidden: attempting to view, update, or analyze another customer's service request returns `403 Forbidden`.

### Endpoint Summary

| HTTP Method | Endpoint Path | Description | Expected Status Codes |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/service-requests` | Create a new service request | `201 Created`, `400 Bad Request`, `401 Unauthorized` |
| `GET` | `/api/service-requests/{id}` | Retrieve request details by ID | `200 OK`, `401 Unauthorized`, `403 Forbidden`, `404 Not Found` |
| `GET` | `/api/service-requests/my` | List all service requests for authenticated customer | `200 OK`, `401 Unauthorized` |
| `PUT` | `/api/service-requests/{id}` | Update title, description, category, location, urgency | `200 OK`, `400 Bad Request`, `401 Unauthorized`, `403 Forbidden`, `409 Conflict` |
| `POST` | `/api/service-requests/{id}/cancel` | Cancel an active service request | `200 OK`, `401 Unauthorized`, `403 Forbidden`, `409 Conflict` |
| `POST` | `/api/service-requests/{id}/analyze` | Trigger Problem Understanding Agent analysis | `200 OK`, `401 Unauthorized`, `403 Forbidden`, `404 Not Found`, `409 Conflict` |
| `POST` | `/api/service-requests/{id}/ready-for-matching` | Transition status to `ReadyForMatching` | `200 OK`, `401 Unauthorized`, `403 Forbidden`, `404 Not Found`, `409 Conflict` |

---

## 11. Component 2 Handoff Contract

When a service request reaches the `ReadyForMatching` status, it satisfies the prerequisite contract required by Component 2 (Provider Matching Agent). The downstream agent consumes the request as a `ServiceRequestForMatchingResponse`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "customerId": "7ca92f18-1245-4231-9876-1e54a321bcde",
  "title": "Vehicle engine stalls on start",
  "description": "My car engine clicks and won't turn over near Battaramulla.",
  "category": "Vehicle Repair",
  "status": "ReadyForMatching",
  "location": "Battaramulla, Colombo",
  "urgency": "High",
  "problemSummary": "Possible starter motor or battery electrical failure",
  "extractedDetails": {
    "vehicleType": "Car",
    "symptom": "Clicking on ignition",
    "locationArea": "Battaramulla"
  },
  "createdUtc": "2026-09-07T10:30:00Z"
}
```

---

## 12. Complete Testing Strategy (Actually Implemented)

The Component 1 testing architecture employs a tiered strategy combining fast in-memory execution with dedicated, real PostgreSQL verification:

```
+-------------------------------------------------------------------------------+
|                           COMPONENT 1 TEST SUITE                              |
|                                                                               |
|  [ Unit & In-Memory Tests ]                [ Real PostgreSQL Integration ]    |
|  - Domain rules & state transitions        - Schema migrations & triggers     |
|  - Agent tools & uncertainty phrasing      - Check constraints & cascades     |
|  - Workflow memory contracts               - Concurrency & row-level locks    |
|  - WebApplicationFactory API tests         - Destructive safety guards        |
|  (Pass: 36 Api.Tests + 140 Integration)   (Pass: 40 Real PostgreSQL tests)   |
|                                                                               |
|              TOTAL TEST SUITE: 216 PASSED (0 FAILED, 0 SKIPPED)               |
+-------------------------------------------------------------------------------+
```

### 1. Unit & In-Memory Tests
- **Domain & Service Validation**: Exercises `ServiceRequestService`, verifying title/description validation, legal state transitions, and tenant ownership enforcement.
- **Agent Reasoning & Tools**: Tests `ProblemUnderstandingAgent`, `ProblemClassificationTool`, `LocationExtractionTool`, and `ServiceKnowledgeTool` against Sri Lankan test corpora.
- **Safety Policy Enforcement**: Validates that risk policies and uncertainty-aware phrasing are strictly upheld.

### 2. WebApplicationFactory API Tests (`AssistLK.Api.Tests`)
- Uses ASP.NET Core `WebApplicationFactory<Program>` with real JWT Bearer authentication pipeline.
- Validates route bindings, HTTP response status codes (`201`, `200`, `400`, `401`, `403`, `404`, `409`), authorization policies, and JSON serialization contracts.

### 3. Dedicated Real PostgreSQL Test Databases
- To verify real PostgreSQL persistence, schema constraints, foreign key cascades, and concurrency behaviors, tests execute against dedicated local PostgreSQL databases:
  - **`assistlk_test_integration`**: Dedicated database for `AssistLK.IntegrationTests`.
  - **`assistlk_test_api`**: Dedicated database for `AssistLK.Api.Tests`.
- Tests verify:
  - Application of all 6 EF Core migrations.
  - Foreign key cascades (deleting a `ServiceRequest` cascades to `ProblemAnalyses`).
  - Restrictive foreign keys (deleting a `User` with active `ServiceRequests` is rejected).
  - PostgreSQL enum check constraints and maximum column lengths.
  - Concurrency token checks.

> **Important Architecture Clarification on Docker / Testcontainers:**
> Docker and Testcontainers are **NOT** currently used in this repository because Docker was unavailable in the development and Phase 7F environment. All database tests run against dedicated, live PostgreSQL test databases. Containerized testing with Testcontainers may be evaluated in future work as an optional CI pipeline strategy.

---

## 13. How to Run Tests

### Prerequisites
1. .NET 8.0 SDK installed.
2. Local PostgreSQL server running.
3. Dedicated test databases created: `assistlk_test_integration` and `assistlk_test_api`.

### Setting Configuration
Configure the connection string using the designated environment variable (do not commit passwords to source control):

```powershell
# In PowerShell:
$env:ASSISTLK_TEST_POSTGRESQL_CONNECTION = "Host=localhost;Port=5432;Database=assistlk_test_integration;Username=postgres;Password=YourPassword;"
```

Alternative configuration keys supported by the test harness:
- `ConnectionStrings__TestConnection`
- .NET User Secrets (ID: `15fcbe56-7908-4746-bfbe-0692dc0c8045`)

### Running the Test Suite
From the repository root or `backend/` directory:

```powershell
# Run the entire test solution:
dotnet test backend/AssistLK.sln

# Run only the API security & controller tests:
dotnet test backend/tests/AssistLK.Api.Tests/AssistLK.Api.Tests.csproj

# Run only the domain, agent, and PostgreSQL integration tests:
dotnet test backend/tests/AssistLK.IntegrationTests/AssistLK.IntegrationTests.csproj
```

---

## 14. PostgreSQL Destructive Safety Guards

To prevent accidental data loss in development or production environments, `PostgreSqlTestDatabase.cs` and `PostgreSqlApiTestDatabase.cs` implement mandatory safety guardrails:

1. **Whitelisted Database Names**: The test harness strictly validates the target database name before executing any operation:
   - Allowed: `assistlk_test_integration` and `assistlk_test_api`.
   - Any attempt to point tests at `assistlk_dev`, `assistlk_prod`, `postgres`, or unapproved database names throws an immediate `InvalidOperationException`.
2. **Deterministic Cleanup via Truncation**:
   - Between test executions, tables are cleaned using:
     ```sql
     TRUNCATE TABLE
         "ProblemAnalyses",
         "ServiceRequests",
         "AgentMemories",
         "AgentExecutionMetrics",
         "AgentActions",
         "AgentApprovals",
         "AgentAuditLogs",
         "AgentExecutions",
         "AgentWorkflows",
         "Users"
     RESTART IDENTITY CASCADE;
     ```
3. **Migration History Preservation**: The `__EFMigrationsHistory` table is strictly protected from truncation to maintain EF Core migration tracking integrity.
