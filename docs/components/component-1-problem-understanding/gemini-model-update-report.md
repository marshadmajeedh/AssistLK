# Component 1: Gemini Model Runtime Configuration & Resilience Report

**Component:** Component 1 – Problem Understanding Agent  
**Branch:** `bugfix/component-1-gemini-runtime-fix`  
**Date:** 2026-09-09  
**Status:** RESOLVED & VERIFIED  

---

## 1. Executive Summary

This report documents the resolution of the Component 1 Gemini runtime failure where the Problem Understanding Agent previously degraded all customer requests to a generic awaiting-information fallback:

```json
{
  "status": "AwaitingInformation",
  "category": "Unclassified",
  "confidence": 0.2,
  "needsMoreInformation": true
}
```

The underlying failure was caused by Google's retirement of the `gemini-2.5-flash` model endpoint for new users/projects (returning `HTTP 404 Not Found`). The system configuration has now been updated to use Google's current active model `gemini-3.6-flash`, default fallbacks updated, transient retry resilience implemented for HTTP 429 and 503, regression test coverage added, and manual runtime verification performed.

---

## 2. Root Cause Analysis

### Previous Model
- **Model Identifier:** `gemini-2.5-flash`
- **Configured In:** `backend/src/AssistLK.Api/appsettings.json` and fallback defaults in `GeminiService.cs`.

### Failure Trigger
When `ProblemUnderstandingAgent` invoked `GeminiService.GenerateContentAsync`, the service dispatched an HTTP POST to:
```
https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=[REDACTED]
```

Google Generative Language API rejected the request with `HTTP 404 Not Found` and the response body:
```json
{
  "error": {
    "code": 404,
    "message": "This model models/gemini-2.5-flash is no longer available to new users. Please update your code to use models/gemini-3.6-flash for the latest features and improvements. We recommend you to use the Interactions API.",
    "status": "NOT_FOUND"
  }
}
```

### Fallback Execution Path
Because `response.IsSuccessStatusCode` evaluated to `false`, `GeminiService` logged a warning and returned `null`. `ProblemUnderstandingAgent` caught `string.IsNullOrWhiteSpace(rawGeminiResponse)` and safely called `CreateDegradedOutput()`, resulting in the customer-facing `AwaitingInformation` degradation.

---

## 3. Architecture & Implementation Changes

### New Model
- **Model Identifier:** `gemini-3.6-flash`
- **API Endpoint:** `https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent`

### Changes Made
1. **Configuration (`backend/src/AssistLK.Api/appsettings.json`):**
   - Updated `"Gemini:Model"` to `"gemini-3.6-flash"`.
   - Maintained clean separation of secrets: zero API keys are stored in `appsettings.json`.
2. **Gemini Service (`backend/src/AssistLK.Agents/Services/GeminiService.cs`):**
   - Updated `DefaultModel` constant to `"gemini-3.6-flash"`.
   - Fallback logic ensures missing, empty, or whitespace configuration values resolve to `gemini-3.6-flash`.
   - Exposed `public string Model => _model;` property for testability.
   - Added lightweight retry resilience:
     - **Retried on:** HTTP 429 (`TooManyRequests`) and HTTP 503 (`ServiceUnavailable`) with exponential delay (up to 3 total attempts).
     - **Fail fast (no retry):** HTTP 400 (`BadRequest`), HTTP 401 (`Unauthorized`), HTTP 403 (`Forbidden`), HTTP 404 (`NotFound`).
   - Implemented safe diagnostic logging:
     - **Allowed:** Model name (`gemini-3.6-flash`), request started (`Gemini execution started`), response status (`Gemini response received: true/false`), non-sensitive failure reasons.
     - **Forbidden / Never logged:** API keys, customer prompts, customer descriptions, full Gemini response payloads.
3. **Problem Understanding Agent (`backend/src/AssistLK.Agents/Agents/ProblemUnderstandingAgent.cs`):**
   - Updated XML doc comments to reference `gemini-3.6-flash`.
   - Preserved all agent boundaries, tool orchestration, and safety filters.
4. **Architectural Invariants Strictly Preserved:**
   - No API endpoints modified.
   - No DTOs modified.
   - No database entities modified.
   - Zero EF Core migrations created.
   - Frontend unchanged.
   - Component boundaries intact.

---

## 4. Secret Handling Chain Confirmation

The secret handling architecture remains strictly adhered to:

```
dotnet user-secrets
       │
       ▼
IConfiguration
       │
       ▼
GeminiService
```

- **Verification:** `dotnet user-secrets list --project backend/src/AssistLK.Api` contains `Gemini:ApiKey`.
- **Integrity Check:** Zero API keys exist in `appsettings.json`, source files, test fixtures, or repository documentation.

---

## 5. Regression Test Suite

Created dedicated test suite: `backend/tests/AssistLK.IntegrationTests/GeminiModelConfigurationTests.cs`.

### Test Cases Implemented:
1. `DefaultModel_ResolvesToGemini36Flash`: Verifies default constructor resolves to `gemini-3.6-flash`.
2. `ConfigurationOverride_WorksAsExpected`: Verifies custom configuration overrides the model.
3. `MissingConfiguration_UsesValidFallback`: Verifies null, empty, and whitespace configuration safely fall back to `gemini-3.6-flash`.
4. `TransientFailure_RetriesAndSucceedsOnSubsequentAttempt`: Verifies HTTP 429 and HTTP 503 are retried and succeed on subsequent 200 responses.
5. `NonRetriableFailure_DoesNotRetry_ReturnsNull`: Verifies HTTP 400, 401, 403, and 404 fail fast after 1 attempt without retrying.
6. `ExhaustedRetries_ReturnsNull`: Verifies 3 consecutive 503 errors exhaust retries and return `null` for safe degradation.
7. `SafeDiagnosticLogging_LogsAllowedFields_NeverLeaksSensitiveData`: Verifies logging contains only allowed fields and never logs API keys, prompts, or response payloads.
8. `LiveGemini_WhenApiKeyConfigured_WaterLeakingHeavilyFromKitchenSink_ReturnsExpectedAnalysis`: Verifies live end-to-end integration when API key is available.

---

## 6. Verification Results

### Non-Incremental Build
```bash
dotnet build backend/AssistLK.sln --no-incremental
```
**Result:** `Build succeeded. 0 Warning(s), 0 Error(s).`

### Full Test Suite Run
```bash
dotnet test backend/AssistLK.sln
```
**Results:**
- `AssistLK.Api.Tests`: 45 passed, 0 failed.
- `AssistLK.IntegrationTests`: 208 passed, 0 failed.
- **Total:** 253 passed (all 241 existing tests + 12 new test permutations passed, 0 failed).

### Repository Search
```bash
grep -r "gemini-2.5-flash"
```
**Result:** Zero active runtime code or configuration files reference `gemini-2.5-flash`. Only historical post-mortem debug reports reference the retired model.

---

## 7. Manual Runtime Verification

### Request:
```
"Water leaking heavily from kitchen sink"
```

### Live Execution Log:
```
Diagnostic: Calling Gemini model: gemini-3.6-flash
Diagnostic: Gemini execution started
Diagnostic: HTTP Status Code: 200
Diagnostic: Gemini response received: true
```

### Gemini Generated Output:
```json
{
  "category": "Plumbing",
  "problemSummary": "Heavy water leak reported near or under the kitchen sink, which may indicate a damaged pipe, loose connection, or broken seal.",
  "urgency": "High",
  "needsMoreInformation": false,
  "followUpQuestions": [],
  "confidence": 0.95,
  "additionalInformation": {
    "location": "Colombo"
  }
}
```

### Service Request Response Object:
```json
{
  "status": "Analyzed",
  "category": "Plumbing",
  "urgency": "High",
  "needsMoreInformation": false,
  "confidence": 0.95,
  "problemSummary": "Heavy water leak reported near or under the kitchen sink, which may indicate a damaged pipe, loose connection, or broken seal."
}
```

### Result:
- Gemini is actively invoked and returns HTTP 200 OK.
- Classification category correctly resolves to `"Plumbing"`.
- Urgency correctly resolves to `"High"`.
- Needs more information resolves to `false`.
- Service request transitions to `"Analyzed"` status instead of degrading.
- Safe diagnostic logging verified with zero secret leaks.
