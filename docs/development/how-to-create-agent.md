# Creating a New Agent in AssistLK

This guide provides engineering standards and instructions for creating new intelligent agents within the AssistLK ecosystem. All agents must adhere to Clean Architecture boundaries, Google Gemini reasoning integration, and the shared agent foundation.

---

## 1. Agent Purpose

Every agent in AssistLK must have **exactly one single responsibility**. An agent should never cross domain boundaries or take on downstream workflow operations.

### Example: `ProviderMatchingAgent` (Component 2)
* **Responsible for:**
  - Analyzing categorized service requests.
  - Querying provider search tools based on location, category, and availability.
  - Scoring and ranking suitable service providers.
* **Not responsible for:**
  - Creating bookings or scheduling appointments.
  - Generating quotations or processing payments.
  - Tracking service delivery or managing provider status.

---

## 2. Agent Structure

All agent implementations reside in the `AssistLK.Agents` project, separated cleanly into dedicated subdirectories:

```text
backend/src/AssistLK.Agents/
├── Abstractions/
│   ├── IAgent.cs
│   ├── IAgentTool.cs
│   └── IGeminiService.cs
├── Agents/
│   ├── ProblemUnderstandingAgent.cs   (Component 1)
│   └── ProviderMatchingAgent.cs       (Component 2 example)
├── Core/
│   ├── AgentContext.cs
│   ├── AgentOrchestrator.cs
│   ├── AgentRegistry.cs
│   ├── AgentSafetyPolicyEngine.cs
│   ├── ToolExecutor.cs
│   └── ToolRegistry.cs
├── Models/
│   ├── AgentResult.cs
│   ├── AgentSafetyRule.cs
│   ├── ProblemUnderstandingInput.cs
│   └── ProblemUnderstandingOutput.cs
├── Services/
│   └── GeminiService.cs
└── Tools/
    ├── LocationExtractionTool.cs
    ├── ProblemClassificationTool.cs
    ├── ProviderSearchTool.cs
    └── ServiceKnowledgeTool.cs
```

---

## 3. Agent Interface

Every agent must implement the [`IAgent`](file:///g:/SE3090_A1/AssistLK/backend/src/AssistLK.Agents/Abstractions/IAgent.cs) abstraction:

```csharp
namespace AssistLK.Agents.Abstractions;

public interface IAgent
{
    string Name { get; }

    Task<AgentResult> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default);
}
```

### Core Execution Flow

```csharp
public sealed class MyNewAgent : IAgent
{
    public string Name => "MyNewAgent";

    private readonly ToolExecutor _toolExecutor;
    private readonly IGeminiService _geminiService;

    public MyNewAgent(ToolExecutor toolExecutor, IGeminiService geminiService)
    {
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
        _geminiService = geminiService ?? throw new ArgumentNullException(nameof(geminiService));
    }

    public async Task<AgentResult> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        // 1. Resolve structured input from context
        // 2. Invoke deterministic support tools via _toolExecutor
        // 3. Call Gemini LLM reasoning via _geminiService
        // 4. Validate and sanitize response (safety policies)
        // 5. Return structured AgentResult with strongly-typed Data payload
    }
}
```

---

## 4. Agent Dependencies

Strict dependency boundaries are enforced to maintain testability and Clean Architecture separation:

### Allowed Dependencies
- `IGeminiService` (LLM reasoning)
- `ToolExecutor` (Support tool dispatcher)
- Safety services (`AgentSafetyService`, `AgentSafetyPolicyEngine`)
- Configuration abstractions (`IConfiguration`, options models)
- In-memory DTOs and value objects

### Forbidden Dependencies
- **`DbContext` / Entity Framework Core**: Agents must never touch the database directly.
- **Repositories**: Database persistence is the sole responsibility of Application workflow services and Domain services.
- **Controllers / API Layer**: Agents are invoked via `AgentOrchestrator`, never directly coupled to HTTP controllers.
- **Direct Database Access / Raw SQL**: Direct queries are strictly forbidden.

---

## 5. Tool Development Rules

Auxiliary tools provide deterministic capabilities (data normalization, validation, domain lookups) without autonomous LLM hallucinations.

### Rules for Tools:
1. **Implement `IAgentTool`**:
   ```csharp
   public interface IAgentTool
   {
       string Name { get; }
       string Description { get; }
       Task<ToolResult> ExecuteAsync(
           Dictionary<string, object> parameters,
           CancellationToken cancellationToken = default);
   }
   ```
2. **One Focused Operation**: Each tool does one specific task (e.g., `LocationExtractionTool` normalizes coordinates; `ProblemClassificationTool` validates canonical taxonomy).
3. **No Database Writes**: Tools must be stateless or read-only. Never perform database mutations inside a tool.
4. **No Cross-Component Logic**: Do not reach into other components or call downstream workflow services.

---

## 6. LLM Integration Rules

When incorporating Google Gemini LLM reasoning:

1. **Structured JSON Output**: Always mandate strict JSON output in the system prompt with zero markdown formatting.
2. **Schema Parsing & Validation**: Parse raw text into structured JSON nodes. Verify all expected fields exist and fall within allowable ranges.
3. **Never Trust Raw LLM Output**:
   - Verify category names against canonical allowlists.
   - Clamp confidence scores to `[0.0, 1.0]`.
   - Strip prohibited phrases, guaranteed diagnoses, and dangerous instructions.
4. **Safety Sanitization**:
   - Enforce uncertainty phrasing ("Possible...", "may indicate...").
   - Eliminate dangerous DIY repair advice (e.g. electrical wiring instructions).
5. **Safe Graceful Degradation**:
   - Wrap Gemini API calls in `try / catch` blocks.
   - If the API times out, returns HTTP 429/500, or produces unparseable output, degrade safely to baseline support tools and set `Category = "Unclassified"` with `NeedsMoreInformation = true`.

---

## 7. Memory Rules

AssistLK provides `AgentMemoryService` for cross-step semantic state retention across the workflow.

### What to Store:
- **Approved semantic keys**: Useful extracted facts (e.g. `problem.category`, `problem.urgency`, `problem.summary`, `problem.location`).
- **Normalized parameters**: Clean values needed by downstream agents.

### What NEVER to Store:
- Full LLM prompts or prompt templates.
- Raw model reasoning traces, chains-of-thought, or internal thoughts.
- Personally identifiable information (PII) beyond validated contact details.
- Secrets, credentials, or transient connection tokens.

---

## 8. Testing Requirements

Every agent must be validated by comprehensive unit and integration tests before merging.

### Unit Tests
Located in `backend/tests/AssistLK.IntegrationTests/` (or dedicated test suites):
- **Valid Response Test**: Verifies successful LLM reasoning and proper output mapping.
- **Invalid / Malformed Response Test**: Verifies fallback handling when the LLM returns invalid JSON or hallucinated categories.
- **Safety Policy Enforcement Test**: Verifies that guaranteed claims and dangerous DIY advice are sanitized or rejected.
- **Missing / Empty Input Test**: Verifies prompt degradation when input strings are whitespace or empty.
- **Tool Failure Test**: Verifies agent continues functioning or degrades gracefully when support tools fail.

### Integration Tests
- **Workflow Orchestration**: End-to-end execution through `ProblemUnderstandingWorkflowService` (or equivalent workflow service).
- **Domain Persistence**: Verifies `ProblemAnalysis` entity creation in PostgreSQL / Test database.
- **Lifecycle Transition**: Verifies state progression (e.g. `Created` $\rightarrow$ `Analyzing` $\rightarrow$ `Analyzed` / `AwaitingInformation`).
- **Monitoring & Metrics**: Verifies execution logs and metrics recorded in `AgentExecutionMetric`.
