# AssistLK Full System Understanding & UI Implementation Readiness Audit

**Status:** Complete Architectural Audit & Implementation Blueprint  
**Scope:** Component 1 (Service Request & Problem Understanding), Auth, Flutter Mobile, React Web, Admin Portal  
**Date:** September 2026  

---

## 1. Executive System Overview

AssistLK is an AI-powered service assistance platform designed to connect customers with localized, vetted service providers (e.g., plumbers, electricians, technicians) across Sri Lanka through a 4-agent collaborative architecture:

```text
Customer Service Problem
        |
        ↓
Component 1: Problem Understanding Agent (.NET 8 Native + Google Gemini 2.5/3.6 Flash)
        | (Handoff: Read-Only ServiceRequestForMatchingResponse)
        ↓
Component 2: Provider Matching Agent (Algorithmic Ranking + Match Selection)
        | (Handoff: Matched Candidates)
        ↓
Component 3: Quotation & Booking Agent (Cost Estimation, Benchmarking & Scheduling)
        | (Handoff: Confirmed Booking)
        ↓
Component 4: Service Tracking & Completion Agent (Milestones, Validation & Sign-off)
```

### Strategic Platform Ownership Decisions

| Persona | Platform | Core Responsibilities |
|---|---|---|
| **Customer** | **Flutter Mobile Application** | Registration, Login, Create Service Request (GPS + Description), View AI Problem Analysis, Answer Clarifications, Confirm Ready for Matching, View History |
| **Provider / Service Staff** | **Flutter Mobile Application** | Registration, Profile/Skill Management, Online/Offline Toggle, View Matched Requests, Issue Quotations, Update Job Progress |
| **Admin / Management** | **React Web Application** | System Dashboard, User Management, Provider Verification & Approval, Service Request Monitoring, Agent Monitoring & Telemetry |

---

## 2. Current Component 1 Status (Backend & Agent Pipeline)

Component 1 ("Smart Service Request & Problem Understanding") is the primary responsibility of Member 1. Its architectural mission is strictly confined to:
- Ingesting raw customer descriptions and geographic locations.
- Performing structured AI reasoning via Google Gemini (`gemini-3.6-flash`).
- Validating category, urgency, safety, and determining if more information is required.
- Transitioning requests through the state machine:
  $$\text{Created} \longrightarrow \text{Analyzing} \longrightarrow \begin{cases} \text{Analyzed} \longrightarrow \text{ReadyForMatching} \\ \text{AwaitingInformation} \longrightarrow (\text{Customer Edit}) \longrightarrow \text{Analyzing} \end{cases}$$
- Reaching the terminal handoff state: `ReadyForMatching`.

### Current Execution Flow

```text
1. Customer initiates request (Description, LocationText, Lat/Lon)
   ↓
2. ServiceRequest created (Status: Created, Category: "Unclassified", Urgency: Unknown)
   ↓
3. POST /api/service-requests/{id}/analyze invoked
   ↓
4. ProblemUnderstandingWorkflowService coordinates execution:
   a. Enforces Customer ownership
   b. Initiates AgentWorkflow & AgentExecution records
   c. Safety policy check ("ANALYZE_PROBLEM" -> Low risk, auto-approved)
   d. Transitions domain status: Created/AwaitingInformation -> Analyzing
   e. Loads prior memory from AgentMemory (if iterative)
   ↓
5. ProblemUnderstandingAgent execution pipeline:
   a. Step 1: LocationExtractionTool (normalizes coordinates & text; non-fatal)
   b. Step 2: GeminiService (calls Google Gemini API with system instructions; primary reasoning engine)
              Fallback: Degraded output produced ONLY if Gemini fails or returns unparseable JSON
   c. Step 3: ProblemClassificationTool (keyword-based category alignment; non-fatal)
   d. Step 4: ServiceKnowledgeTool (adds safe terminology & domain guidance; non-fatal)
   e. Step 5: ApplySafetyPolicies (strips dangerous DIY repair advice, enforces uncertainty language like "Possible...", prevents guaranteed diagnoses)
   ↓
6. Output Persistence:
   a. Structured memory saved to AgentMemory (problem.category, problem.summary, problem.urgency, problem.confidence, problem.follow_up_questions)
   b. ProblemAnalysis entity saved to DB (DetectedProblem, Confidence, AgentName)
   c. ServiceRequest updated with Category, Urgency, and Status (Analyzed or AwaitingInformation)
   d. AgentExecutionMetric recorded (ExecutionTimeMs, ToolCalls, Status)
   ↓
7. Returns ProblemUnderstandingResponseDto to client
```

### Component 1 Boundary Isolation Verification

| Area | Component 1 Allowed | Component 1 Prohibited (Strict Invariants) | Verification Status |
|---|---|---|---|
| **Data Entities** | `ServiceRequest`, `ProblemAnalysis` | `Provider`, `Quotation`, `Booking`, `ServiceTracking` | **Compliant** — No foreign tables accessed |
| **Logic** | Problem categorization, location parsing, urgency assessment, clarification questions | Provider matching, scoring, quotation calculations, booking confirmations | **Compliant** — Completely isolated |
| **Agent Tools** | `LocationExtractionTool`, `ProblemClassificationTool`, `ServiceKnowledgeTool` | Provider search, quoting, payment tools | **Compliant** — `DemoProviderSearchTool` exists in project but is unused by the agent |
| **Handoff Contract** | `IServiceRequestService.GetReadyForMatchingAsync()` | Directly invoking Component 2 services or mutating Component 2 tables | **Compliant** — Clean DTO boundary |

---

## 3. Existing UI Inventory

### Web UI (`web/src`)

The existing React application was built using Vite, React Router v6, and Vanilla inline CSS based on the centralized design system (`src/shared/theme`).

| Page / Component | Path | Purpose | Implementation Status |
|---|---|---|---|
| **LoginPage** | `features/auth/pages/LoginPage.jsx` | User authentication via `/api/auth/login` | **Complete** (Supports Customer & Admin login) |
| **CustomerLayout** | `shared/layouts/CustomerLayout.jsx` | Header navigation for Customer portal | **Complete** (My Requests, Create Request, Profile, Logout) |
| **AdminLayout** | `shared/layouts/AdminLayout.jsx` | Sidebar navigation for Admin portal | **Complete** (Dashboard, Service Requests, Providers, Quotations, Tracking, AI Workflows) |
| **ServiceRequestListPage** | `features/serviceRequests/pages/ServiceRequestListPage.jsx` | View list of customer's submitted requests | **Complete** (Status filter, empty state, card rendering) |
| **CreateServiceRequestPage** | `features/serviceRequests/pages/CreateServiceRequestPage.jsx` | Form to create new service request | **Complete** (Description, LocationText, validation) |
| **ServiceRequestDetailPage** | `features/serviceRequests/pages/ServiceRequestDetailPage.jsx` | Comprehensive request detail view & AI trigger | **Complete** (Analyze action, status lifecycle, edit/cancel triggers) |
| **EditServiceRequestPage** | `features/serviceRequests/pages/EditServiceRequestPage.jsx` | Edit description/location for existing request | **Complete** (Permitted in `Created` and `AwaitingInformation` states) |
| **AnalysisResultCard** | `features/serviceRequests/components/AnalysisResultCard.jsx` | Visual display of Gemini analysis results | **Complete** (Category, Urgency, Confidence %, Problem Summary) |
| **ClarificationSection** | `features/serviceRequests/components/ClarificationSection.jsx` | Displays follow-up questions from AI | **Complete** (Follow-up questions, "Update Details" and "Re-analyze" actions) |
| **ReadyForMatchingSection** | `features/serviceRequests/components/ReadyForMatchingSection.jsx` | Final handoff state display | **Complete** (Summary banner, confirmed specs, next steps guidance) |
| **ServiceRequestCard** | `features/serviceRequests/components/ServiceRequestCard.jsx` | List view summary card with status badges | **Complete** |

### Mobile UI (`mobile/lib`)

The existing Flutter application uses Flutter 3.13+, Provider pattern, Dio HTTP client, and `flutter_secure_storage`.

| Screen / Component | Path | Purpose | Implementation Status |
|---|---|---|---|
| **AuthGate** | `features/auth/screens/auth_gate.dart` | Root routing gate based on user role | **Complete** (Routes null -> Login, Customer -> CustomerHome, Provider -> ProviderHome) |
| **LoginScreen** | `features/auth/screens/login_screen.dart` | Email/Password login | **Complete** |
| **RegisterScreen** | `features/auth/screens/register_screen.dart` | Account creation (Customer/Provider) | **Complete** |
| **CustomerHomeScreen** | `features/service_requests/customer_home_screen.dart` | Customer landing screen | **Stub / Placeholder Only** (Displays welcome text and logout button) |
| **ProviderHomeScreen** | `features/providers/provider_home_screen.dart` | Provider landing screen | **Stub / Placeholder Only** (Displays welcome text and logout button) |
| **Shared Theme & Widgets** | `shared/theme/` and `shared/widgets/` | AppTheme, AppColors, AppTextStyles, AppButton, AppTextField, AppCard | **Complete** |

---

## 4. Missing UI Inventory

### Missing in Flutter Mobile (Customer Component 1 Scope)

Because Customer interactions must natively run on Flutter mobile, the entire Customer Component 1 workflow must be built in `mobile/`:

1. **Service Request Domain Models & DTOs:**
   - `service_request_model.dart`
   - `problem_analysis_model.dart`
   - `create_service_request_dto.dart`
   - `update_service_request_dto.dart`
2. **Service Request Service & API Client Integration:**
   - `service_request_service.dart` wrapping all backend endpoints
3. **Service Request State Management:**
   - `service_request_provider.dart` (fetching list, creating request, polling/triggering analysis, marking ready for matching, canceling)
4. **Customer Mobile Screens:**
   - **Service Request List Screen:** History of customer requests with status badges, urgency chips, and pull-to-refresh.
   - **Create Service Request Screen:** Natural language problem description input, location text input, device GPS location detection integration.
   - **Service Request Detail Screen:** Complete status display, problem description, location metadata, action buttons.
   - **Edit Service Request Screen:** Updating problem details when in `Created` or `AwaitingInformation` status.
5. **Mobile Component 1 Widgets:**
   - `StatusBadge` widget (Created, Analyzing, AwaitingInformation, Analyzed, ReadyForMatching, Cancelled)
   - `AnalysisResultCard` widget (Category, Urgency, Confidence %, Problem Summary)
   - `ClarificationSection` widget (Follow-up questions, direct answer / edit workflow)
   - `ReadyForMatchingSection` widget (Handoff confirmation banner)
   - `CancelConfirmationDialog`

### Missing in React Web (Admin Portal Scope)

Per the platform architecture decision, the Web Application is the designated platform for **Admin / Management**. Currently, all admin routes render `PlaceholderPage`:

1. **Admin Dashboard (`/dashboard`):**
   - KPI metrics: Total requests, active requests, analyzed requests, ready for matching, cancelled.
   - AI Agent Health & Latency metrics (fed from `AgentExecutionMetric`).
   - Recent system activity stream.
2. **Admin Service Request Monitoring (`/admin/service-requests`):**
   - Platform-wide request table (all customer requests).
   - Filters: Status, Urgency, Category, Date range.
   - Inspect request details, customer info, problem analysis log, and audit trail.
3. **Admin User Management (`/admin/users`):**
   - Directory of registered users (Customers, Providers, Admins).
   - Activate / Deactivate user accounts.
   - Role management.
4. **Admin Provider Approval Portal (`/providers`):**
   - Pending provider verification queue.
   - Inspect provider business credentials, category certifications, skill tags.
   - Approve, reject, or request more information for provider accounts.

---

## 5. Backend Changes Required (Future Implementation Scope)

> **REMINDER:** DO NOT modify backend code at this stage. These requirements are documented for the future execution phase.

| Priority | Area | Required Change | Rationale |
|---|---|---|---|
| **High** | **Admin Service Request APIs** | Add `GET /api/admin/service-requests` and `GET /api/admin/service-requests/{id}` to a new `AdminServiceRequestsController` | `ServiceRequestsController` is restricted to `[Authorize(Roles = "Customer")]` and filters by `GetCurrentCustomerId()`. Admins currently have no endpoint to view platform-wide requests. |
| **High** | **Admin User Management APIs** | Implement `AdminUsersController` with `GET /api/admin/users` and `PUT /api/admin/users/{id}/status` | Admins cannot manage users or toggle `IsActive`. |
| **Medium** | **Problem Analysis Persistence** | Add `FollowUpQuestions` and `NeedsMoreInformation` to `ProblemAnalysis` entity or expose `GET /api/service-requests/{id}/analysis` | `ProblemAnalysis` in DB currently omits follow-up questions (they are only stored in `AgentMemory`). When a customer reopens a request, clarification questions cannot be re-fetched. |
| **Medium** | **Component 2 Handoff API** | Expose `GET /api/service-requests/{id}/ready-for-matching` on `ServiceRequestsController` | Documented in `component-boundaries.md` for Component 2 consumption, but currently only implemented as an internal C# method `IServiceRequestService.GetReadyForMatchingAsync`. |
| **Medium** | **Controller Security Hardening** | Add `[Authorize(Roles = "Admin")]` to `AgentWorkflowController` (`/api/agents/execute`) and `AgentMonitoringController` (`/api/agent-monitoring`) | Currently unauthenticated and exposed publicly. |
| **Low** | **Cleanup Unused Tool** | Remove or decouple `DemoProviderSearchTool` from `AssistLK.Agents` | Provider search belongs to Component 2; having it in Component 1 agent DI creates architectural confusion. |

---

## 6. Flutter Mobile Implementation Plan (Customer Component 1)

### Phase 1: Data & Service Layer
1. Create `mobile/lib/features/service_requests/models/`:
   - `service_request_model.dart`
   - `problem_analysis_model.dart`
   - `service_request_status_enum.dart`
   - `service_request_urgency_enum.dart`
2. Create `mobile/lib/features/service_requests/services/service_request_service.dart`:
   - Implement Dio calls matching backend endpoints:
     - `createServiceRequest(CreateServiceRequestDto dto)`
     - `getMyRequests()`
     - `getRequestById(String id)`
     - `updateServiceRequest(String id, UpdateServiceRequestDto dto)`
     - `cancelServiceRequest(String id)`
     - `analyzeProblem(String id)`
     - `markReadyForMatching(String id)`
3. Create `mobile/lib/features/service_requests/providers/service_request_provider.dart`:
   - Manage state for request list, active request, analysis loading states, error handling.

### Phase 2: UI Components & Widgets
1. `mobile/lib/features/service_requests/widgets/status_badge.dart`:
   - Color-coded badges for all 6 statuses matching web design tokens.
2. `mobile/lib/features/service_requests/widgets/analysis_result_card.dart`:
   - Structured card showing detected problem, confidence percentage bar, urgency badge, category chip.
3. `mobile/lib/features/service_requests/widgets/clarification_section.dart`:
   - Display follow-up questions, quick answer/clarification editing, and re-analyze trigger.
4. `mobile/lib/features/service_requests/widgets/ready_for_matching_section.dart`:
   - Handoff confirmation card with specs and next steps.

### Phase 3: Screens & Flow Integration
1. Replace `customer_home_screen.dart` with a modern Dashboard/Request List:
   - Header with greeting, active request count, "New Service Request" floating action button.
   - List of service request cards with pull-to-refresh.
2. Build `create_service_request_screen.dart`:
   - Problem description field with character counter and helpful prompt hints.
   - Location input with optional GPS coordinate fetch button.
   - Submit -> auto-navigate to detail screen.
3. Build `service_request_detail_screen.dart`:
   - Request summary and status tracking.
   - "Analyze Problem with AI" action button.
   - Conditional rendering of Analysis Card, Clarification Section, or Ready-For-Matching Card based on status.
   - Cancel and Edit modal flows.
4. Build `edit_service_request_screen.dart`:
   - Pre-filled description and location editing.

---

## 7. Web Implementation Plan (Admin Portal Focus)

### Architecture Transition
The current web app contains Customer pages (`ServiceRequestListPage`, `CreateServiceRequestPage`, `ServiceRequestDetailPage`, `EditServiceRequestPage`). To align with the platform decision:
- Retain existing Customer web pages as a secondary / development reference, OR isolate them under `/customer/*`.
- Develop dedicated Admin features under `web/src/features/admin/` to replace placeholder screens in `AdminLayout`.

### Phase 1: Shared Admin Components
1. Admin Stat Card component (`StatCard.jsx`) for KPI figures.
2. Data Table component (`DataTable.jsx`) with pagination, search, and sorting.
3. Filter Toolbar component (`FilterToolbar.jsx`).

### Phase 2: Admin Feature Pages
1. **Admin Dashboard (`web/src/features/admin/pages/AdminDashboardPage.jsx`):**
   - KPI metrics: Total Requests, Analyzed Requests, Ready for Matching, Cancelled.
   - Gemini performance widget: Average reasoning latency, tool call metrics (from `/api/agent-monitoring`).
2. **Admin Service Request Monitoring (`web/src/features/admin/pages/AdminServiceRequestMonitoringPage.jsx`):**
   - Table of all platform requests.
   - Detailed inspection drawer: View customer info, raw problem description, AI analysis result, urgency, and timestamps.
3. **Admin User Management (`web/src/features/admin/pages/AdminUserManagementPage.jsx`):**
   - Users table (Customers, Providers, Admins).
   - Activate/Deactivate actions.
4. **Admin Provider Approval Portal (`web/src/features/admin/pages/AdminProviderApprovalPage.jsx`):**
   - List of unverified providers.
   - Verification review action (mocked until Component 2 DB tables arrive).

---

## 8. Role and Permission Model

### Existing Roles in Domain (`UserRole.cs`)

```csharp
public enum UserRole
{
    Customer = 1,
    Provider = 2,
    Admin = 3
}
```

### Role Matrix & Access Boundaries

| Capability / Resource | Customer | Provider | Admin | Enforcing Layer |
|---|:---:|:---:|:---:|---|
| **Register Account** | Allowed | Allowed | Denied (Seeded only) | Backend `AuthService.RegisterAsync` |
| **Login** | Allowed | Allowed | Allowed | Backend `AuthService.LoginAsync` |
| **Create Service Request** | Allowed | Denied | Denied | `[Authorize(Roles = "Customer")]` |
| **View Own Service Requests** | Allowed | Denied | Denied | `[Authorize(Roles = "Customer")]` + Customer ID matching |
| **Edit/Cancel Own Request** | Allowed | Denied | Denied | `[Authorize(Roles = "Customer")]` + State validation |
| **Trigger AI Problem Analysis** | Allowed | Denied | Denied | `[Authorize(Roles = "Customer")]` + Customer ID matching |
| **Mark Ready for Matching** | Allowed | Denied | Denied | `[Authorize(Roles = "Customer")]` + State validation |
| **View All Platform Requests** | Denied | Denied | Allowed | `[Authorize(Roles = "Admin")]` (Needs endpoint) |
| **Manage Users & Status** | Denied | Denied | Allowed | `[Authorize(Roles = "Admin")]` (Needs endpoint) |
| **Approve Providers** | Denied | Denied | Allowed | `[Authorize(Roles = "Admin")]` (Needs endpoint) |
| **View Agent Execution Metrics** | Denied | Denied | Allowed | `[Authorize(Roles = "Admin")]` (Needs hardening) |

---

## 9. API Contract Review Summary

### Component 1 Public APIs

| Method | Route | Auth / Role | Request DTO | Response DTO | Preconditions |
|---|---|---|---|---|---|
| `POST` | `/api/service-requests` | `Customer` | `CreateServiceRequestRequest`<br>• Description (req)<br>• LocationText (req)<br>• Lat/Lon (opt) | `ServiceRequestResponse`<br>(Status: `Created`, Category: `"Unclassified"`, Urgency: `Unknown`) | Valid customer token |
| `GET` | `/api/service-requests/my` | `Customer` | None | `List<ServiceRequestResponse>` | None |
| `GET` | `/api/service-requests/{id}` | `Customer` | Route Param `id` | `ServiceRequestResponse` | Must be owner |
| `PUT` | `/api/service-requests/{id}` | `Customer` | `UpdateServiceRequestRequest`<br>• Description (req)<br>• LocationText (req)<br>• Lat/Lon (opt) | `ServiceRequestResponse` | Must be owner; Status must be `Created` or `AwaitingInformation` |
| `POST` | `/api/service-requests/{id}/cancel` | `Customer` | Route Param `id` | `ServiceRequestResponse`<br>(Status: `Cancelled`) | Must be owner; Status cannot be `ReadyForMatching` or `Cancelled` |
| `POST` | `/api/service-requests/{id}/analyze` | `Customer` | Route Param `id` | `ProblemUnderstandingResponseDto`<br>• WorkflowId<br>• ExecutionId<br>• Category<br>• ProblemSummary<br>• Urgency<br>• Confidence<br>• NeedsMoreInformation<br>• FollowUpQuestions | Must be owner; Status must be `Created` or `AwaitingInformation` |
| `POST` | `/api/service-requests/{id}/ready-for-matching` | `Customer` | Route Param `id` | `ServiceRequestResponse`<br>(Status: `ReadyForMatching`) | Must be owner; Status must be `Analyzed`; Category cannot be `Unclassified`; Urgency cannot be `Unknown`; Valid Confidence > 0 |

### Component 2 Downstream Handoff Contract

Component 1 hands off data to Component 2 via `ServiceRequestForMatchingResponse`:

```json
{
  "serviceRequestId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "category": "Plumbing",
  "problemSummary": "Possible burst pipe causing localized kitchen flooding.",
  "confidence": 0.92,
  "urgency": "High",
  "locationText": "Colombo 07, Sri Lanka",
  "latitude": 6.9056,
  "longitude": 79.8667,
  "status": "ReadyForMatching",
  "createdAt": "2026-09-09T10:00:00Z",
  "updatedAt": "2026-09-09T10:05:00Z"
}
```

*Component 1 does not transmit customer phone numbers, password hashes, or internal LLM prompts/reasoning traces to downstream components.*

---

## 10. Risks and Recommendations

| Risk | Impact | Recommended Mitigation |
|---|---|---|
| **1. Disconnect between Web UI and Mobile Strategy** | Medium | The existing React customer UI is already fully operational and serves as a verified reference specification. Keep it intact for development verification while building the primary Customer UI in Flutter. |
| **2. Transient Clarification Questions** | High | `ProblemAnalysis` entity in DB does not persist `FollowUpQuestions`. If the customer refreshes the app while in `AwaitingInformation` status, the follow-up questions cannot be re-fetched. Persist questions in `ProblemAnalysis` table or return them from an expanded detail DTO. |
| **3. Missing Admin Service Request API** | High | Admin Web cannot display customer requests because `ServiceRequestsController` strictly enforces `Roles = "Customer"`. Implement `AdminServiceRequestsController` with `[Authorize(Roles = "Admin")]` before implementing Admin Web UI. |
| **4. Unauthenticated Agent Endpoints** | High | `/api/agents/execute` and `/api/agent-monitoring` have no `[Authorize]` attribute. Protect them with `[Authorize(Roles = "Admin")]`. |
| **5. Missing HTTP Handoff GET Endpoint** | Medium | While `IServiceRequestService.GetReadyForMatchingAsync` exists in C#, an HTTP GET endpoint `GET /api/service-requests/{id}/ready-for-matching` will be needed if Component 2 is hosted as an independent Python microservice. |
