# Component 1 Classification Logic Cleanup Report

**Audit Date:** September 2026  
**Target Solution:** `backend/AssistLK.sln`  
**Component:** Component 1 – Problem Understanding Agent  
**Reasoning Engine:** Google Gemini LLM (`gemini-2.5-flash` via `IGeminiService`)

---

## 1. Executive Summary

An audit of Component 1 (`ProblemUnderstandingAgent`) was conducted to ensure all reasoning relies on Google Gemini LLM rather than legacy deterministic or keyword-matching classification mechanisms. 

During the audit:
- The obsolete keyword-matching classifier and duplicate agent (`DemoProblemAgent.cs`) was removed.
- All registrations for `DemoProblemAgent` in `backend/src/AssistLK.Api/Program.cs` were purged.
- Dependencies of `ProblemUnderstandingAgent` were verified to strictly comply with Clean Architecture boundaries (no `DbContext`, no repository, no direct database access).
- Baseline validation/support tools (`ProblemClassificationTool`, `LocationExtractionTool`, `ServiceKnowledgeTool`) and safety infrastructure were verified and preserved intact.
- Both backend test suites passed with 100% success (232 tests passing, 0 failures).

---

## 2. Component 1 Architecture Verification

### Active Architecture: LLM Reasoning Engine

The primary reasoning engine for natural language understanding is **Google Gemini LLM** (`gemini-2.5-flash`), orchestrated by [ProblemUnderstandingAgent](file:///g:/SE3090_A1/AssistLK/backend/src/AssistLK.Agents/Agents/ProblemUnderstandingAgent.cs).

```
Customer Description
        │
        ▼
┌─────────────────────────────────────────────────────────────┐
│                 ProblemUnderstandingAgent                   │
│                                                             │
│  1. Location Tool Execution (LocationExtractionTool)        │
│  2. Baseline Validation Tool (ProblemClassificationTool)    │
│  3. Gemini LLM Reasoning (IGeminiService)                   │
│  4. Service Knowledge Retrieval (ServiceKnowledgeTool)      │
│  5. In-Agent Safety Enforcement (ApplySafetyPolicies)       │
└─────────────────────────────────────────────────────────────┘
        │
        ▼
ProblemUnderstandingOutput (JSON: Category, Urgency, Questions)
```

### Dependency Audit: ProblemUnderstandingAgent

| Dependency | Status | Classification | Purpose |
| :--- | :--- | :--- | :--- |
| `ToolExecutor` | **Injected** | Allowed | Executes auxiliary support tools via decoupled registry |
| `IGeminiService` | **Injected** | Allowed | Calls Gemini reasoning API (`gemini-2.5-flash`) |
| Safety policies | **Internal** | Allowed | In-agent sanitization (`ApplySafetyPolicies`) |
| `DbContext` | **Excluded** | Forbidden | *Strictly absent — zero database context access* |
| `IRepository` | **Excluded** | Forbidden | *Strictly absent — zero repository access* |
| Rule-based Classifier Services | **Excluded** | Forbidden | *Strictly absent — no external rule service* |
| Direct DB / SQL Queries | **Excluded** | Forbidden | *Strictly absent* |

---

## 3. Removed Classes & Logic

### 1. `DemoProblemAgent.cs`

- **File Path:** `backend/src/AssistLK.Agents/Agents/DemoProblemAgent.cs`
- **Category:** Keyword-matching classifier / Hardcoded problem categorization / Duplicate classification agent
- **Description:** Early demonstration agent that analyzed customer strings using hardcoded keyword matching:
  ```csharp
  if (input.Contains("start"))
  {
      problem = "Possible battery or ignition issue";
  }
  else if (input.Contains("flat"))
  {
      problem = "Possible tyre issue";
  }
  else
  {
      problem = "General vehicle issue";
  }
  ```
- **Action Taken:** Completely deleted from `backend/src/AssistLK.Agents/Agents/`.

### 2. Dependency Injection & Agent Registry Purge

- **File Path:** `backend/src/AssistLK.Api/Program.cs`
- **Removals:**
  - Removed service registration:
    ```csharp
    // REMOVED
    builder.Services.AddScoped<DemoProblemAgent>();
    ```
  - Removed registry initialization:
    ```csharp
    // REMOVED
    var demoAgent = scope.ServiceProvider.GetRequiredService<DemoProblemAgent>();
    registry.Register(demoAgent);
    ```

---

## 4. Preserved Support Tools & Safety Services

As specified in the architectural mandate, the following validation tools, support tools, and safety mechanisms are **preserved**:

1. **`ProblemClassificationTool`** (`backend/src/AssistLK.Agents/Tools/ProblemClassificationTool.cs`)
   - *Role:* Validation and fallback baseline tool called via `ToolExecutor`. Does not perform database operations; verifies domain categories against canonical terms.
2. **`LocationExtractionTool`** (`backend/src/AssistLK.Agents/Tools/LocationExtractionTool.cs`)
   - *Role:* Support tool for geographic entity extraction and coordinates normalization.
3. **`ServiceKnowledgeTool`** (`backend/src/AssistLK.Agents/Tools/ServiceKnowledgeTool.cs`)
   - *Role:* Support tool providing safe terminology and recommending professional inspections.
4. **`AgentSafetyService` & `AgentSafetyPolicyEngine`** (`AssistLK.Application` & `AssistLK.Agents.Core`)
   - *Role:* Enforces safety policy rules, audit trails, and approval barriers before agent actions execute.
5. **`ToolExecutor`** (`backend/src/AssistLK.Agents/Core/ToolExecutor.cs`)
   - *Role:* Decoupled dispatcher for auxiliary tool calls.
6. **`AgentOrchestrator`** (`backend/src/AssistLK.Agents/Core/AgentOrchestrator.cs`)
   - *Role:* Central agent lifecycle coordinator.

---

## 5. Build and Test Verification

### Solution Build

```bash
dotnet build backend/AssistLK.sln
```
**Result:** Build succeeded with **0 Warning(s)** and **0 Error(s)**.

### Test Execution

```bash
dotnet test backend/AssistLK.sln
```

| Test Project | Passed | Failed | Skipped | Total | Duration |
| :--- | :---: | :---: | :---: | :---: | :---: |
| `AssistLK.Api.Tests.dll` | 36 | 0 | 0 | 36 | 2.0s |
| `AssistLK.IntegrationTests.dll` | 196 | 0 | 0 | 196 | 6.0s |
| **Total** | **232** | **0** | **0** | **232** | **8.0s** |

All tests covering Gemini LLM reasoning, workflow coordination, tool execution, degraded mode fallback, and safety sanitization continue to pass without error.
