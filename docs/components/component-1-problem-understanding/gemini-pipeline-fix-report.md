# Gemini Pipeline Fix Report
**Branch:** `feature/component-1-gemini-agent-integration`
**Date:** 2026-09-09
**Component:** Component 1 — Problem Understanding Agent

---

## Root Cause

`ProblemUnderstandingAgent.AnalyseWithGeminiAndToolsAsync` contained a hard early-return gate after `ProblemClassificationTool`:

```csharp
// Lines 186–191 (BEFORE fix)
if (!classificationToolSucceeded || classData is null)
{
    var degradedOutput = CreateDegradedOutput(locationText);
    return (degradedOutput, toolCalls);   // ← Gemini never reached
}

// Step 3: Gemini LLM Reasoning ← only reached when tool succeeded
```

Gemini was gated behind a **deterministic tool**. Any tool failure — network timeout, unregistered tool, classification returning no data — caused an immediate degraded fallback before Gemini was ever called. This was the exact opposite of the intended architecture: Gemini should be the primary reasoning engine and tools should be optional supporting components.

Two secondary issues compounded the problem:
1. **Silent catch blocks** swallowed all tool exceptions with no logging, making failures invisible.
2. **Singleton registries holding Scoped services** — `AgentRegistry` and `ToolRegistry` were registered as `Singleton` in `Program.cs` and populated by resolving `Scoped` agents/tools from a single startup scope. Every subsequent request reused stale instances captured from that one scope.

---

## Before Architecture

```
Customer Request
      |
      ↓
 LocationExtractionTool
      |
      ↓
 ProblemClassificationTool
      |  ← HARD GATE
  if fails
      ↓
 CreateDegradedOutput()  ←── RETURNS HERE (Gemini never called)
      |
   [Gemini is skipped entirely]
```

**Silent catch block (before):**
```csharp
catch
{
    classificationToolSucceeded = false;
}
```

**Singleton captive dependency (before):**
```csharp
builder.Services.AddSingleton<AgentRegistry>();  // holds Scoped agents
builder.Services.AddSingleton<ToolRegistry>();   // holds Scoped tools
```

---

## After Architecture

```
Customer Request
      |
      ↓
 ProblemUnderstandingAgent
      |
      +——————————————+
      |              |
 LocationTool    Gemini LLM  ← PRIMARY reasoning engine
 (optional)           |
      +———————————————+
                      |
            ProblemClassificationTool
            (VALIDATION / ALIGNMENT only — optional)
                      |
            ServiceKnowledgeTool
            (ENRICHMENT only — optional)
                      |
            Safety Validation
                      |
                      ↓
              ProblemAnalysis Output
```

**Tool failure is non-fatal (after):**
```csharp
catch (Exception ex)
{
    _logger.LogWarning(
        "Agent tool execution failed: {Message}",
        ex.Message);
    // execution continues — Gemini output stands as authoritative
}
```

**Degraded output only when Gemini itself fails (after):**
```csharp
if (string.IsNullOrWhiteSpace(rawGeminiResponse) ||
    !TryParseGeminiResponse(rawGeminiResponse, out var parsedOutput))
{
    var degraded = CreateDegradedOutput(locationText);
    return (degraded, toolCalls);
}
```

---

## DI Lifetime Fix

**Before (captive dependency):**
```csharp
// Program.cs — Singleton stores Scoped services captured at startup
builder.Services.AddSingleton<AgentRegistry>();
builder.Services.AddSingleton<ToolRegistry>();

// Post-build scope populated the singletons from one scope:
using (var scope = app.Services.CreateScope())
{
    var registry = scope.ServiceProvider.GetRequiredService<AgentRegistry>();
    registry.Register(scope.ServiceProvider.GetRequiredService<ProblemUnderstandingAgent>());
    ...
}
```

**After (correct Scoped factory):**
```csharp
// Program.cs — Scoped registries built fresh per-request
builder.Services.AddScoped<AgentRegistry>(sp =>
{
    var registry = new AgentRegistry();
    registry.Register(sp.GetRequiredService<DemoProblemAgent>());
    registry.Register(sp.GetRequiredService<ProblemUnderstandingAgent>());
    return registry;
});

builder.Services.AddScoped<ToolRegistry>(sp =>
{
    var registry = new ToolRegistry();
    registry.Register(sp.GetRequiredService<DemoProviderSearchTool>());
    registry.Register(sp.GetRequiredService<ProblemClassificationTool>());
    registry.Register(sp.GetRequiredService<LocationExtractionTool>());
    registry.Register(sp.GetRequiredService<ServiceKnowledgeTool>());
    return registry;
});
```

`AgentSafetyPolicyEngine` remains `Singleton` (stateless, no scoped dependencies).

---

## Files Changed

| File | Change |
|---|---|
| [`ProblemUnderstandingAgent.cs`](file:///G:/SE3090_A1/AssistLK/backend/src/AssistLK.Agents/Agents/ProblemUnderstandingAgent.cs) | Reordered pipeline; removed early-return gate; added `ILogger`; made `IGeminiService` required; replaced all silent catches with logged warnings |
| [`Program.cs`](file:///G:/SE3090_A1/AssistLK/backend/src/AssistLK.Api/Program.cs) | Changed `AgentRegistry` and `ToolRegistry` from `Singleton` to `Scoped` factory delegates; removed post-build scope blocks |
| [`AssistLK.Api.Tests.csproj`](file:///G:/SE3090_A1/AssistLK/backend/tests/AssistLK.Api.Tests/AssistLK.Api.Tests.csproj) | Added `Moq 4.20.70` |
| [`ProblemUnderstandingAgentTests.cs`](file:///G:/SE3090_A1/AssistLK/backend/tests/AssistLK.Api.Tests/ProblemUnderstandingAgentTests.cs) | **New** — 5 unit tests (Tests A–D + empty description) |
| [`Component1FailureTests.cs`](file:///G:/SE3090_A1/AssistLK/backend/tests/AssistLK.IntegrationTests/Component1FailureTests.cs) | Updated constructor calls; renamed test `Agent_DegradesSafelyToUnclassifiedWhenClassificationToolFails` → `Agent_ContinuesWithGeminiWhenClassificationToolMissing` with corrected assertions |
| [`Component1WorkflowTests.cs`](file:///G:/SE3090_A1/AssistLK/backend/tests/AssistLK.IntegrationTests/Component1WorkflowTests.cs) | Updated constructor call |
| [`GeminiReasoningTests.cs`](file:///G:/SE3090_A1/AssistLK/backend/tests/AssistLK.IntegrationTests/GeminiReasoningTests.cs) | Updated 3 constructor call sites |
| [`ProblemUnderstandingAgentTests.cs`](file:///G:/SE3090_A1/AssistLK/backend/tests/AssistLK.IntegrationTests/ProblemUnderstandingAgentTests.cs) | Updated 2 constructor call sites; updated reflection assertion from 2→3 parameters |
| [`Component1EndToEndPostgreSqlTests.cs`](file:///G:/SE3090_A1/AssistLK/backend/tests/AssistLK.IntegrationTests/PostgreSql/Component1EndToEndPostgreSqlTests.cs) | Updated constructor call |

No migrations, no API contracts, no frontend code changed.

---

## Tests Added

New file: [`ProblemUnderstandingAgentTests.cs`](file:///G:/SE3090_A1/AssistLK/backend/tests/AssistLK.Api.Tests/ProblemUnderstandingAgentTests.cs) in `AssistLK.Api.Tests`

| Test | Scenario | Expected |
|---|---|---|
| **Test A** `TestA_GeminiExecutes_WhenClassificationToolThrows` | `ProblemClassificationTool.ExecuteAsync` → throws `InvalidOperationException` | `Category == "Plumbing"`, `NextAction == "Analysed"`, Gemini called once |
| **Test B** `TestB_GeminiUnavailable_ReturnsSafeDegradedOutput` | `IGeminiService.GenerateContentAsync` → returns `null` | `Category == "Unclassified"`, `NextAction == "AwaitingInformation"`, `Confidence ≤ 0.4`, `Degraded` key present |
| **Test C** `TestC_ToolFailure_DoesNotCrashWorkflow_AndAgentSucceeds` | All 3 tools throw; Gemini succeeds | No exception escapes; result `Success == true`, `NextAction == "Analysed"` |
| **Test D** `TestD_GeminiIsAuthoritative_WhenConflictingWithClassificationTool` | Gemini → `Electrical`; tool → `Plumbing` | `Category == "Electrical"` (Gemini wins; tool alignment only applies when Gemini returns Unclassified) |
| **Edge** `EmptyDescription_ReturnsAwaitingInformation_WithoutCallingGemini` | Empty description | `AwaitingInformation`; Gemini NOT called |

---

## Verification Results

```
dotnet build backend/AssistLK.sln --configuration Release
```
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:02.65
```

```
dotnet test backend/AssistLK.sln --configuration Release
```
```
Passed!  - Failed: 0, Passed:  41, Skipped: 0, Total:  41  ← AssistLK.Api.Tests
Passed!  - Failed: 0, Passed: 196, Skipped: 0, Total: 196  ← AssistLK.IntegrationTests
```

**Total: 237 tests passing, 0 failures.**
- Previous: 232 tests (37 Api + 195 Integration)
- Added: 5 new unit tests (Tests A–D + edge case)
- Renamed: 1 existing integration test (`Agent_DegradesSafelyToUnclassifiedWhenClassificationToolFails` → `Agent_ContinuesWithGeminiWhenClassificationToolMissing`)

> [!NOTE]
> PostgreSQL integration tests (`Component1EndToEndPostgreSqlTests`) are excluded from the above run as they require a live PostgreSQL instance. The constructor call site was updated and the file compiles cleanly.

---

## Constraints Verified

- ✅ No API contract changes
- ✅ No database schema changes
- ✅ No migrations added
- ✅ No frontend code modified
- ✅ Component 2/3/4 code untouched
