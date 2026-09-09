# Gemini Tool Architecture — Component 1: Problem Understanding Agent

**Component:** Component 1 — Problem Understanding Agent  
**Agent:** `ProblemUnderstandingAgent`  
**LLM:** Google Gemini (`gemini-2.5-flash`)  
**Updated:** 2026-09-09

---

## Agent Design Principle

> **"The LLM provides reasoning capability while deterministic tools enforce reliability,
> validation, taxonomy consistency, and safety constraints."**

Gemini understands language and intent. Deterministic tools enforce rules.  
Neither alone is sufficient; together they produce reliable, safe, structured output.

---

## Previous Architecture (Classification-First — Incorrect)

The original pipeline placed the deterministic classification tool **before** Gemini:

```
Customer Request
      ↓
ProblemUnderstandingAgent
      ↓
ProblemClassificationTool  ← HARD GATE
      |
   if fails
      ↓
CreateDegradedOutput()     ← Gemini never executes
```

### Why This Was Wrong

1. **Tool failure blocked LLM reasoning.** Any exception from `ProblemClassificationTool`
   (network timeout, unregistered tool, empty keyword match) caused an immediate degraded
   response before Gemini was ever called. The result was always:
   - `Category: Unclassified`
   - `Status: AwaitingInformation`
   - `Confidence: 0.2`

2. **The tool was acting as a classifier, not a validator.** A keyword-matching algorithm
   was deciding whether analysis happened — a responsibility that belongs to the LLM.

3. **Silent catch blocks hid failures.** All tool exceptions were swallowed without logging:
   ```csharp
   // BEFORE (incorrect)
   catch
   {
       classificationToolSucceeded = false;
   }
   // Gemini never reached
   ```

4. **Role inversion.** A deterministic rule engine was the primary decision maker.
   The LLM, which has far superior language understanding, was a secondary fallback.

---

## New Architecture (Gemini-First — Current)

```
Customer Request
      ↓
ProblemUnderstandingAgent
      │
      ├─ Step 1: LocationExtractionTool   (optional enrichment — failure is non-fatal)
      │
      ├─ Step 2: GeminiService            ← PRIMARY reasoning engine
      │          gemini-2.5-flash
      │          Produces: category, summary, urgency, confidence,
      │                    needsMoreInformation, followUpQuestions
      │          ↓
      │       [Only here can a degraded output be triggered — Gemini failure only]
      │
      ├─ Step 3: ProblemClassificationTool  (validation/alignment — failure is non-fatal)
      │
      ├─ Step 4: ServiceKnowledgeTool       (enrichment — failure is non-fatal)
      │
      └─ Step 5: Safety Validation          (mandatory — always runs)
            ↓
      ProblemAnalysis Output
```

### Key Properties

| Property | Value |
|---|---|
| Primary reasoning | Gemini LLM |
| Tool failure behaviour | Non-fatal — logged warning, execution continues |
| Degraded output trigger | Gemini returns `null` or unparseable JSON only |
| Tool role | Validation, enrichment, safety — never primary classification |

### Error Handling

All tool exceptions are caught and logged without exposing secrets:

```csharp
// AFTER (correct)
catch (Exception ex)
{
    _logger.LogWarning(
        "Agent tool execution failed: {Message}",
        ex.Message);
    // Execution continues — Gemini output remains authoritative
}
```

Never logged: API keys, Gemini prompts, raw Gemini responses, customer PII.

---

## Tool Responsibilities

### Gemini LLM (Primary)
- Understand the customer's natural-language problem description
- Determine the most likely service category from the canonical taxonomy
- Generate a concise, uncertainty-aware problem summary
- Estimate urgency based on described symptoms
- Decide whether more information is required
- Generate relevant follow-up questions when needed

### ProblemClassificationTool (Validation / Taxonomy Enforcement)
- **Validate** that the Gemini-provided category is in the canonical taxonomy
- **Normalize** near-miss category names (e.g. via the Unclassified promotion pathway)
- **Prevent** invalid taxonomy values from reaching the persistence layer

The tool does **not**:
- Decide whether analysis happens
- Replace Gemini's reasoning
- Override a confident Gemini classification

### LocationExtractionTool (Optional Enrichment)
- Normalize free-text location strings
- Validate and enrich latitude/longitude coordinates
- Failure degrades safely — original location text is preserved

### ServiceKnowledgeTool (Optional Enrichment)
- Add safe, customer-facing domain terminology to the analysis
- Flag whether professional inspection is recommended
- Failure degrades safely — analysis proceeds without enrichment

### Safety Validation (Mandatory)
- Strip dangerous DIY instructions from problem summaries
- Replace guaranteed diagnostic language with uncertainty language
- Ensure Unclassified output always requires more information
- Cap confidence scores for unclassified results

---

## Validation Policy Decision

### Taxonomy Normalization (Tool Promotes Unclassified)

When Gemini returns a category that does not exactly match the canonical taxonomy,
the parser maps it to `"Unclassified"`. The `ProblemClassificationTool` may then
**promote** this to a valid canonical category if it has sufficient keyword evidence:

```
Gemini: "Plumber"  →  parser: "Unclassified"
                                     ↓
                        ProblemClassificationTool: "Plumbing" (keywords matched)
                                     ↓
                          alignment: "Plumbing"  ✓
```

**Rule:** The tool may only promote from `"Unclassified"` — never override a valid category.

### Gemini Authority (Tool Must Not Override)

When Gemini returns a **valid canonical category** (Plumbing, Electrical, Vehicle Repair,
Appliance Repair), the classification tool result is ignored for category selection.

```
Gemini: "Electrical" (canonical, confidence 0.88)
ProblemClassificationTool: "Plumbing" (keyword match on "water")
                                     ↓
                          Final: "Electrical"  ✓  (Gemini wins)
```

**Rationale:** A keyword matcher cannot understand context. A description like
*"My lights keep tripping the water-pump circuit breaker"* contains plumbing keywords
but describes an electrical fault. Gemini's language understanding correctly identifies
this; a tool override would degrade quality.

**Code reference:**
```csharp
// Alignment only activates when Gemini returned Unclassified
if (classData is not null &&
    string.Equals(parsedOutput.Category, "Unclassified", StringComparison.OrdinalIgnoreCase) &&
    !string.Equals(classData.Category, "Unclassified", StringComparison.OrdinalIgnoreCase))
{
    parsedOutput.Category = classData.Category;
    parsedOutput.Confidence = Math.Max(parsedOutput.Confidence, classData.Confidence);
}
```

---

## Regression Test Coverage

Located in `backend/tests/AssistLK.Api.Tests/GeminiToolValidationTests.cs`:

| Test | Scenario | Verifies |
|---|---|---|
| `ClassificationToolFailure_DoesNotPreventGeminiExecution` | Classification tool throws | Gemini still called; `Status: Analyzed` |
| `GeminiUnavailable_ReturnsSafeFallback` | GeminiService returns `null` | `Category: Unclassified`; `Status: AwaitingInformation`; `Degraded` marker present |
| `InvalidGeminiCategory_IsNormalizedByValidator` | Gemini returns `"Plumber"` | Taxonomy normalization → `"Plumbing"` via validator promotion |
| `GeminiReasoning_RemainsAuthoritativeOverValidator` | Gemini: `"Electrical"`, Tool: `"Plumbing"` | Final: `"Electrical"` (Gemini wins) |

---

## Constraints

- ✅ No API contract changes
- ✅ No database schema changes
- ✅ No migrations added or removed
- ✅ No frontend code modified
- ✅ Components 2, 3, 4 untouched
- ✅ Gemini API key never logged
- ✅ Customer descriptions never logged
- ✅ Raw Gemini responses never logged
