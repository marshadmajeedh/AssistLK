# AssistLK Classification Logic Audit & Removal Report

**Date:** September 2026  
**Scope:** Repository-wide audit of classification logic across `backend/src/`, `backend/tests/`, and documentation.

---

## 1. Audit Scope and Objectives

Following the migration of Component 1 (Problem Understanding) to **Google Gemini LLM** (`gemini-2.5-flash`), this audit inspected the entire repository for obsolete deterministic classification artifacts:
- Obsolete classification agents (e.g. `ProblemClassificationAgent`, `ClassificationAgent`, `DemoProblemAgent`)
- Keyword/dictionary pattern-matching classifiers (`RuleBasedClassifier`, `KeywordClassifier`, `ClassificationService`)
- Hardcoded problem category mappings or if/else problem categorizers
- Redundant registrations in Dependency Injection and `AgentRegistry`

---

## 2. Classification Logic Classification Matrix

| Item / File | Status | Classification | Rationale |
| :--- | :---: | :---: | :--- |
| `backend/src/AssistLK.Agents/Agents/DemoProblemAgent.cs` | **REMOVED** | A (Old reasoning logic) | Contained obsolete keyword-matching categorization (`input.Contains("start")`, `input.Contains("flat")`). Duplicated classification responsibility. |
| `backend/src/AssistLK.Api/Program.cs` (`DemoProblemAgent` DI & Registry) | **REMOVED** | A (Old reasoning logic) | Purged `builder.Services.AddScoped<DemoProblemAgent>()` and `registry.Register(demoAgent)` to eliminate duplicate agent registration. |
| `backend/src/AssistLK.Agents/Tools/ProblemClassificationTool.cs` | **KEPT** | B (Deterministic support tool) | Required tool implementing `IAgentTool`. Invoked via `ToolExecutor` inside `ProblemUnderstandingAgent` for deterministic baseline validation, canonical taxonomy checking, and safe fallback. |
| `backend/src/AssistLK.Agents/Tools/LocationExtractionTool.cs` | **KEPT** | B (Deterministic support tool) | Required tool implementing `IAgentTool`. Extracts location entities and normalizes coordinates. |
| `backend/src/AssistLK.Agents/Tools/ServiceKnowledgeTool.cs` | **KEPT** | B (Deterministic support tool) | Required tool implementing `IAgentTool`. Provides canonical domain terminology and advises professional inspection. |
| `backend/src/AssistLK.Domain/Enums/ServiceRequestUrgency.cs` | **KEPT** | Canonical Domain Enum | Defines domain urgency levels (`Low`, `Medium`, `High`, `Critical`, `Unknown`). |
| `backend/src/AssistLK.Domain/Enums/ServiceRequestStatus.cs` | **KEPT** | Canonical Domain Enum | Defines request lifecycle states (`Created`, `Analyzing`, `AwaitingInformation`, `Analyzed`, etc.). |
| `backend/src/AssistLK.Agents/Models/ProblemUnderstandingInput.cs` | **KEPT** | DTO Contract | Clean structured input model passed across boundaries. |
| `backend/src/AssistLK.Agents/Models/ProblemUnderstandingOutput.cs` | **KEPT** | DTO Contract | Structured output model containing canonical category, urgency, uncertainty summary, follow-up questions, and confidence. |

---

## 3. Detailed Actions Taken

### Removed Artifacts
1. **`backend/src/AssistLK.Agents/Agents/DemoProblemAgent.cs`**
   - **Reason:** Old deterministic agent using hardcoded string matching.
   - **Replaced by:** `ProblemUnderstandingAgent` with `GeminiService` (`gemini-2.5-flash`).
2. **`Program.cs` Scoped Service & Registry Entries**
   - Lines 92–93: `builder.Services.AddScoped<DemoProblemAgent>();` (Removed)
   - Lines 203–207: `var demoAgent = scope.ServiceProvider.GetRequiredService<DemoProblemAgent>(); registry.Register(demoAgent);` (Removed)

### Kept Artifacts
1. **`ProblemClassificationTool`**
   - **Reason:** Does not perform autonomous classification decisions; acts as a support and validation tool called by `ToolExecutor` during agent execution. Enables safe degradation if the Gemini API is unreachable.
2. **Domain Enums & Canonical Category Lists**
   - Categories ("Plumbing", "Electrical", "Vehicle Repair", "Appliance Repair", "Unclassified") are kept as canonical domain constants in `ProblemUnderstandingAgent` and `ProblemClassificationTool`.

---

## 4. Remaining Classification Flow

With all obsolete deterministic classification logic removed, the production classification flow operates strictly through Gemini LLM reasoning:

```
1. Customer Request
   └─► ProblemUnderstandingWorkflowService.AnalyzeAsync(input)
       │
2. Safety Pre-check
   └─► AgentSafetyService.CheckActionAsync("ANALYZE_PROBLEM")
       │
3. Agent Orchestrator
   └─► AgentOrchestrator.ExecuteAsync("ProblemUnderstandingAgent", context)
       │
4. ProblemUnderstandingAgent Execution
   ├─► ToolExecutor.ExecuteAsync("LocationExtractionTool")
   ├─► ToolExecutor.ExecuteAsync("ProblemClassificationTool") [Baseline validation]
   ├─► GeminiService.GenerateContentAsync(prompt, systemInstruction) [LLM Reasoning]
   ├─► ToolExecutor.ExecuteAsync("ServiceKnowledgeTool") [Domain terminology]
   └─► ApplySafetyPolicies() [Enforces uncertainty language & eliminates dangerous DIY]
       │
5. Output Validation & Memory Persistence
   ├─► Validates confidence score [0.0 - 1.0] and non-empty summary
   └─► AgentMemoryService.SaveAsync() [Semantic keys: problem.category, urgency, summary]
       │
6. Domain Persistence
   └─► IServiceRequestService.ApplyProblemAnalysisResultAsync() [Creates ProblemAnalysis entity]
```
