# Component 1 — Problem Understanding Agent: Gemini Runtime Debug Report

**Date:** 2026-09-09  
**Investigator:** Antigravity AI  
**Scope:** Diagnose why `ProblemUnderstandingAgent` always returns degraded output (`status: "AwaitingInformation"`, `category: "Unclassified"`, `confidence: 0.2`, `needsMoreInformation: true`) even for clear customer requests in the live runtime.  
**Rule Adherence:** No refactoring performed; exact runtime execution path and root cause identified; no secrets or customer data logged.

---

## 1. Executive Summary & Root Cause

### Primary Root Cause
The live runtime failure is caused by an **HTTP 404 (Not Found)** returned by Google's Generative Language API endpoint when attempting to invoke the model configured in `backend/src/AssistLK.Api/appsettings.json` (`"Gemini:Model": "gemini-2.5-flash"`):

```json
{
  "error": {
    "code": 404,
    "message": "This model models/gemini-2.5-flash is no longer available to new users. Please update your code to use models/gemini-3.6-flash for the latest features and improvements. We recommend you to use the Interactions API.",
    "status": "NOT_FOUND"
  }
}
```

### Why It Fails in Live Runtime but Passes in Unit/Integration Tests
1. **In Unit and Integration Tests**:
   - Tests either inject a mock `IGeminiService` (e.g. `FakeGeminiService`) or instantiate `new GeminiService()` with no configuration.
   - When no configuration/API key is present, `GeminiService` engages **offline simulation** (`SimulateOfflineReasoning`), which keyword-classifies plumbing, electrical, carpentry, etc. accurately.
2. **In Live Runtime (`AssistLK.Api`)**:
   - `dotnet user-secrets` contains `Gemini:ApiKey`.
   - `GeminiService.ResolveApiKey` successfully finds the key.
   - Because `apiKey` is present, offline simulation is **bypassed** and a real HTTP POST is dispatched to Google's API:
     `https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=[REDACTED]`
   - Google rejects the request with **HTTP 404 Not Found** because `gemini-2.5-flash` has been deprecated / retired for this API key / project tier.
   - `GeminiService.GenerateContentAsync` receives a non-success HTTP status code (404) and returns `null`.
   - `ProblemUnderstandingAgent.AnalyseWithGeminiAndToolsAsync` receives `rawGeminiResponse == null`, which triggers `CreateDegradedOutput(locationText)`.
   - The degraded output produces:
     ```json
     {
       "status": "AwaitingInformation",
       "category": "Unclassified",
       "confidence": 0.2,
       "needsMoreInformation": true
     }
     ```
     for **every single customer request**, regardless of how clear or detailed the prompt is.

---

## 2. Investigation Task Findings

### Task 1: Trace Fallback Execution (`CreateDegradedOutput`)
Inspected: [`ProblemUnderstandingAgent.cs`](file:///g:/SE3090_A1/AssistLK/backend/src/AssistLK.Agents/Agents/ProblemUnderstandingAgent.cs)

- **Call Site**: Exactly one call site exists at line 220 in `AnalyseWithGeminiAndToolsAsync` (method defined at line 552).
- **Condition Causing Fallback**:
  ```csharp
  if (string.IsNullOrWhiteSpace(rawGeminiResponse))
  {
      _logger.LogWarning("Gemini fallback triggered: reason - Gemini reasoning returned null or empty response. Returning degraded response.");
      var degraded = CreateDegradedOutput(locationText);
      return (degraded, toolCalls);
  }

  if (!TryParseGeminiResponse(rawGeminiResponse, out var parsedOutput))
  {
      _logger.LogWarning("Gemini fallback triggered: reason - Gemini reasoning produced unparseable JSON. Returning degraded response.");
      var degraded = CreateDegradedOutput(locationText);
      return (degraded, toolCalls);
  }
  ```
- **Was Gemini called before fallback?**:
  **YES**. Gemini (`_geminiService.GenerateContentAsync(prompt, SystemInstruction, cancellationToken)`) is always executed prior to this check.
- **Does a tool failure trigger fallback?**:
  **NO**.
  - Step 1 (`LocationExtractionTool`): Wrapped in `try / catch`. If it throws, a warning is logged, `locationText` remains un-normalized, and execution proceeds straight to Step 2 (Gemini).
  - Step 3 (`ProblemClassificationTool`) & Step 4 (`ServiceKnowledgeTool`): Run strictly *after* Gemini returns and JSON is successfully parsed. Any failure in these tools is caught and logged as a warning; neither tool calls `CreateDegradedOutput`.
- **Does parsing failure trigger fallback?**:
  **YES**. If Gemini returns text that cannot be parsed as a valid JSON object by `TryParseGeminiResponse`, the second guard fires and calls `CreateDegradedOutput`. In the current runtime, however, execution fails before parsing because `rawGeminiResponse` is `null`.

---

### Task 2: Verify Gemini Execution
Inspected: [`GeminiService.cs`](file:///g:/SE3090_A1/AssistLK/backend/src/AssistLK.Agents/Services/GeminiService.cs) and [`ProblemUnderstandingAgent.cs`](file:///g:/SE3090_A1/AssistLK/backend/src/AssistLK.Agents/Agents/ProblemUnderstandingAgent.cs)

| Check | Verification | Result |
|---|---|---|
| **Is `GenerateContentAsync` called?** | Verified in agent Step 2 | **YES** |
| **Is API key loaded?** | `ResolveApiKey` checks `GOOGLE_API_KEY`, then `Gemini:ApiKey` from `IConfiguration`, then environment | **YES** (`Gemini:ApiKey` present in user-secrets) |
| **Is Gemini HTTP request sent?** | Dispatched via `_httpClient.PostAsync` to `https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent` | **YES** |
| **Is response received?** | HTTP response received from Google endpoint | **YES (Status: 404 Not Found)** |
| **Is JSON parsing successful?** | Not reached because HTTP 404 causes `GenerateContentAsync` to return `null` | **N/A (`null` response)** |

#### Safe Diagnostic Logs Added (Complying with redaction rules)
- `[Information] Gemini execution started`
- `[Information] Gemini API key configured: true`
- `[Information] Calling Gemini model: gemini-2.5-flash`
- `[Warning] Gemini fallback triggered: reason - non-success HTTP status code 404`
- `[Information] Gemini response received: false`
- `[Warning] Gemini fallback triggered: reason - Gemini reasoning returned null or empty response. Returning degraded response.`

*No API keys, JWT tokens, customer data, prompts, or model output were logged.*

---

### Task 3: Verify Dependency Injection
Inspected: [`backend/src/AssistLK.Api/Program.cs`](file:///g:/SE3090_A1/AssistLK/backend/src/AssistLK.Api/Program.cs)

Confirmed DI registrations:
1. `builder.Services.AddScoped<IGeminiService, GeminiService>();` — Registered correctly.
2. `builder.Services.AddScoped<ToolExecutor>();` — Registered correctly.
3. `builder.Services.AddScoped<ProblemUnderstandingAgent>();` — Registered correctly, receives `ToolExecutor`, `IGeminiService`, and `ILogger<ProblemUnderstandingAgent>`.
4. `AgentRegistry` is registered as a Scoped factory registering `ProblemUnderstandingAgent`:
   ```csharp
   builder.Services.AddScoped<AgentRegistry>(sp =>
   {
       var registry = new AgentRegistry();
       registry.Register(sp.GetRequiredService<DemoProblemAgent>());
       registry.Register(sp.GetRequiredService<ProblemUnderstandingAgent>());
       return registry;
   });
   ```
5. `ProblemUnderstandingWorkflowService` resolves `AgentOrchestrator` and explicitly calls:
   `_orchestrator.ExecuteAsync("ProblemUnderstandingAgent", context)`
6. **No obsolete agent is being executed.**

---

### Task 4: Verify Runtime Configuration
Command: `dotnet user-secrets list --project backend/src/AssistLK.Api`

- **Project UserSecretsId**: `15fcbe56-7908-4746-bfbe-0692dc0c8045`
- **Result**: `Gemini:ApiKey` **EXISTS** (53 characters length). Value kept private.
- **Model Setting in `appsettings.json`**:
  ```json
  "Gemini": {
    "Model": "gemini-2.5-flash"
  }
  ```

---

## 3. Runtime Path Before Fix

```
Customer Request: "Water leaking heavily from kitchen sink pipe"
       │
       ▼
ProblemUnderstandingWorkflowService.StartAnalysisAsync
       │
       ▼
AgentOrchestrator.ExecuteAsync("ProblemUnderstandingAgent", context)
       │
       ▼
ProblemUnderstandingAgent.ExecuteAsync
       │
       ▼
ProblemUnderstandingAgent.AnalyseWithGeminiAndToolsAsync
       │
       ├─► Step 1: LocationExtractionTool (Optional - extracts "Colombo")
       │
       ├─► Step 2: Gemini LLM Reasoning (PRIMARY ENGINE)
       │     │
       │     ├─► Log: "Gemini execution started"
       │     ├─► GeminiService.GenerateContentAsync
       │     │     ├─► ResolveApiKey() → Key loaded from user-secrets (true)
       │     │     ├─► Log: "Gemini API key configured: true"
       │     │     ├─► URL: models/gemini-2.5-flash:generateContent
       │     │     ├─► Log: "Calling Gemini model: gemini-2.5-flash"
       │     │     ├─► HTTP POST to generativelanguage.googleapis.com
       │     │     │
       │     │     ▼
       │     │   GOOGLE API RESPONSE: HTTP 404 Not Found
       │     │   Body: "This model models/gemini-2.5-flash is no longer available to new users.
       │     │          Please update your code to use models/gemini-3.6-flash..."
       │     │     │
       │     │     ├─► Log: "Gemini fallback triggered: reason - non-success HTTP status code 404"
       │     │     ├─► Log: "Gemini response received: false"
       │     │     └─► Returns: null
       │     │
       │     ▼
       │   rawGeminiResponse == null
       │     │
       │     ├─► Log: "Gemini fallback triggered: reason - Gemini reasoning returned null or empty response. Returning degraded response."
       │     └─► CreateDegradedOutput(locationText)
       │
       ▼
Degraded Output Generated:
{
  "Category": "Unclassified",
  "Confidence": 0.2,
  "NeedsMoreInformation": true,
  "Urgency": "Unknown",
  "AdditionalInformation": { "Degraded": "True" }
}
       │
       ▼
AgentResult: NextAction = "AwaitingInformation"
       │
       ▼
ProblemUnderstandingWorkflowService:
Status set to ProblemAnalysisStatus.AwaitingInformation
Saved to ProblemAnalyses table & returned to caller
```

---

## 4. Evidence from Live API Testing

We tested available models against Google's Generative Language API endpoint using the runtime API key:

| Model ID | HTTP Status | Response / Error |
|---|---|---|
| `gemini-2.5-flash` | **404 Not Found** | `"This model models/gemini-2.5-flash is no longer available to new users. Please update your code to use models/gemini-3.6-flash..."` |
| `gemini-2.5-pro` | **404 Not Found** | `"This model models/gemini-2.5-pro is no longer available to new users. Please update your code to use models/gemini-3.1-pro-preview..."` |
| `gemini-2.5-flash-lite` | **404 Not Found** | `"This model models/gemini-2.5-flash-lite is no longer available to new users. Please update your code to use models/gemini-3.5-flash-lite..."` |
| `gemini-3.6-flash` | **200 OK** | Successfully classified request (`"category": "Plumbing"`, `"confidence": 0.95`, `"urgency": "High"`). (Occasional transient 503 high demand spike observed). |
| `gemini-3.7-flash` | **200 OK** | Successfully classified request (`"category": "Plumbing"`, `"confidence": 0.95`, `"urgency": "High"`). |
| `gemini-3.8-flash` | **503 / High Demand** | Model experiencing temporary high demand spikes. |

---

## 5. Minimal Fix Recommendations

> [!IMPORTANT]
> As per instructions, **no functional code fixes have been applied yet**. The following minimal changes are recommended for implementation once approved.

### Recommendation 1: Update Model Configuration in `appsettings.json`
Update `backend/src/AssistLK.Api/appsettings.json`:
```json
  "Gemini": {
    "Model": "gemini-3.6-flash"
  }
```
*(Or `gemini-3.7-flash` / `gemini-3.8-flash` according to target performance and availability).*

### Recommendation 2: Update Fallback Default in `GeminiService.cs`
Update the default fallback string in `GeminiService.cs`:
```csharp
_model = configuration?["Gemini:Model"] ?? "gemini-3.6-flash";
```

### Recommendation 3: Add Transient Retry Policy for 503 / 429
Because newer models on the v1beta endpoint occasionally return transient `503 Server Unavailable` ("spikes in demand are usually temporary"), implement a lightweight retry (1–2 retries with 500ms delay) or fallback model list in `GeminiService.cs` so that transient spikes do not cause immediate degraded fallback.
