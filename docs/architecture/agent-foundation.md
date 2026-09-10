# AssistLK Agent Foundation Architecture

**Runtime Environment:** .NET 8 (C#) — `AssistLK.Agents`
**LLM Engine:** Google Gemini API (via `GeminiService`)
**Coordination:** `AgentOrchestrator` & `ToolExecutor`
**Safety & Governance:** `AgentSafetyPolicyEngine` & `AgentSafetyService`

---

## 1. Overview

The AssistLK Agent Foundation provides a native, high-performance, and auditable Agentic AI subsystem implemented directly within the .NET 8 solution (`backend/src/AssistLK.Agents`).

All AI agent capabilities are fully integrated into the backend's Clean Architecture without relying on external Python runtimes or LangGraph services. The Agent Foundation ensures that AI reasoning is constrained by deterministic tools, validated schemas, persistent memory, explicit safety policies, and human approval gates.

```text
ASP.NET Core Web API (Controllers)
       |
       v
Application Workflow Services (e.g., ProblemUnderstandingWorkflowService)
       |
       +---> AgentOrchestrator & AgentRegistry
       |        |
       |        +---> Specialized Agents (e.g., ProblemUnderstandingAgent)
       |                 |
       |                 +---> GeminiService (Google Gemini 1.5 / Flash LLM)
       |                 |
       |                 +---> ToolExecutor & ToolRegistry
       |                          |---> ProblemClassificationTool
       |                          |---> LocationExtractionTool
       |                          |---> ServiceKnowledgeTool
       |
       +---> AgentSafetyPolicyEngine (Risk Evaluation & Human Approval Gates)
       +---> AgentMemoryService (Persistent Workflow Context in PostgreSQL)
       +---> AgentMonitoringService (Execution Metrics & Performance Tracking)
```

---

## 2. Core Foundation Components

### 2.1 Agent Engine & Abstractions
- **`IAgent` Interface:** The core contract implemented by all specialized agents (`Name`, `ExecuteAsync(AgentContext, CancellationToken)` returning `AgentResult`).
- **`AgentContext`:** Scoped context carrying `WorkflowId`, `UserId`, `Input`, execution flags, and key-value workflow memory.
- **`AgentOrchestrator`:** Resolves agents from the `AgentRegistry`, executes lifecycle hooks, and coordinates execution flow.
- **`AgentRegistry`:** Scoped/singleton service registry maintaining approved, available agent implementations.

### 2.2 LLM Reasoning via Google Gemini (`GeminiService`)
- **Integration Layer:** `IGeminiService` implemented by `GeminiService.cs` using `System.Net.Http.HttpClient` and `System.Text.Json`.
- **Structured Output Generation:** Prompts are structured with strict JSON output schemas. The service extracts clean JSON payloads, strips Markdown code fences, and deserializes strongly-typed responses.
- **Deterministic Tool Fallbacks:** If the LLM call fails, times out, or returns malformed JSON, agents fall back to deterministic local tools to ensure platform reliability.
- **Privacy & Safety Filters:** No personally identifiable information (PII) or secrets are sent to external LLM endpoints.

### 2.3 Tool Framework (`ToolExecutor` & `ToolRegistry`)
- **`IAgentTool` Interface:** Contract for all deterministic tools (`Name`, `Description`, `ExecuteAsync(parameters)` returning `ToolResult`).
- **`ToolExecutor`:** Safe execution wrapper that verifies tool existence, logs tool calls, records latency, handles exceptions, and records audit entries.
- **Active Production Tools:**
  - `ProblemClassificationTool`: Deterministic classification of canonical categories (`Plumbing`, `Electrical`, `Vehicle Repair`, `Appliance Repair`).
  - `LocationExtractionTool`: Extraction and validation of Sri Lankan districts, cities, and GPS coordinates.
  - `ServiceKnowledgeTool`: Domain-specific safety advice, risk assessment, and uncertainty phrasing without hazardous DIY advice.

### 2.4 Agent Safety Policy Engine
- **`AgentSafetyPolicyEngine`:** Evaluates proposed agent actions against deterministic safety rules.
- **Risk Tiers:**
  - `Low`: Read-only, diagnostic actions (e.g., `ANALYZE_PROBLEM`). May execute autonomously without human approval.
  - `Medium`: Non-destructive state preparation. Reviewed where necessary.
  - `High`: External commitment actions (e.g., `CREATE_BOOKING`, `CANCEL_SERVICE`). Pauses workflow in `WaitingForApproval` state and requires human approval.
  - `Critical`: Financial and credential operations (e.g., `MAKE_PAYMENT`). Requires strict multi-party confirmation.
- **Human Approval Mechanism:** Coordinated via `AgentApproval` entities and `AgentSafetyService.RequestApprovalAsync()` / `ApproveAsync()`.

### 2.5 Workflow Memory System
- **`AgentMemoryService`:** Manages structured, cross-agent context stored in the `AgentMemories` table in PostgreSQL.
- **Decoupled Handoff:** Agents never call other agents directly. Component 1 writes structured keys (`problem.category`, `problem.location`, `problem.urgency`), which Component 2 subsequently reads to search and rank matching providers.
- **Zero Hidden Chain-of-Thought:** Only structured, validated attributes are stored. Internal conversational scratchpads and unformatted thoughts are never persisted.

### 2.6 Monitoring & Audit System
- **`AgentMonitoringService`:** Records execution time, success/failure counts, and tool invocation tallies in `AgentExecutionMetrics`.
- **`AgentAuditLog`:** Persists immutable security and workflow transition audit records with timestamps and actor correlation IDs.

---

## 3. Specialized Team Agents

| Component | Specialized Agent | Primary Responsibility | Input / Output |
|---|---|---|---|
| **Component 1** | `ProblemUnderstandingAgent` | Natural language ingestion, problem classification, location parsing, urgency scoring, clarification generation. | Raw customer request → Structured `ProblemAnalysis` & memory keys |
| **Component 2** | `ProviderMatchingAgent` | Provider search, availability checking, skill matching, distance ranking, recommendation generation. | Workflow memory (`category`, `location`) → Ranked provider list |
| **Component 3** | `ServiceCoordinationAgent` | Quotation negotiation, quote comparison, appointment scheduling, booking creation with human approval. | Selected provider & request → Approved `Booking` |
| **Component 4** | `ValidationSafetyAgent` | Service tracking validation, status transition guards, completion evidence checking, customer review logging. | Job updates → Verified service completion & feedback |

---

## 4. Architectural Invariants & Security Boundaries

1. **No Direct Database Access from Agents:** Agents interact with domain state exclusively through Application services, repositories, or approved tools.
2. **No Public Client Endpoints for Agents:** React and Flutter clients never call agent services directly; all requests flow through authenticated ASP.NET Core API controllers.
3. **Deterministic State Transitions:** AI output is treated as untrusted suggestion until validated and mapped to domain state machines.
4. **Resilience & Safe Failure:** If LLM inference fails, the agent framework records a safe failure state and alerts the user rather than leaving orphaned workflow records.
