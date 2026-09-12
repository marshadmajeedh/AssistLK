# AssistLK Agentic AI Architecture

`agent-services/` contains internal Python reasoning services. Component 1 uses the [Problem Understanding service](problem-understanding-agent/README.md) as its only active reasoning runtime. The logical agent name is `ProblemUnderstandingAgent`.

## Trust and execution boundaries

```text
Flutter / React -> ASP.NET Core -> Application workflow
 -> AgentOrchestrator -> AgentRegistry -> .NET adapter / HTTP client
 -> internal Python FastAPI service -> LangGraph
```

ASP.NET owns authentication, authorization, lifecycle, deterministic domain validation, PostgreSQL persistence, monitoring, and failure recovery. Python owns request-scoped graph state, deterministic Python tools, provider abstraction, reasoning, ambiguity checks, and structured output guardrails. Frontends never call Python directly, and Python has no direct database ownership.

C1 transport failures return through the adapter to workflow recovery. There is no native C# fallback or runtime mode switch. Detailed graph, contract, provider configuration, authentication, failure behavior, setup, and tests live in the [service README](problem-understanding-agent/README.md).

## Shared foundation and integration principles

The .NET adapter implements `IAgent`, accepts `AgentContext`, and is registered with dependency injection and `AgentRegistry`. The orchestrator selects it by logical name. Shared C# `IAgentTool`, `ToolRegistry`, and `ToolExecutor` abstractions are distinct from Python tool functions; Python does not implement C# interfaces.

Application services load and persist concise workflow memory through `AgentMemoryService`/`AgentContextService`. Agents exchange approved structured context through application workflows, not direct agent-to-agent calls. Python graph state lasts for the request and is not persisted workflow memory.

ASP.NET safety services govern application actions and approval requirements. Python guardrails constrain analysis content. These are separate checks. Future sensitive booking/payment actions remain subject to their owning component's authorization and approval rules; C1 does not implement those operations.

Monitoring records execution identity, status, duration, errors, and tool usage through the backend. Python returns execution metadata; ASP.NET records authoritative metrics and audit evidence. Illustrative metrics are not benchmark results. Hidden reasoning is neither persisted nor exposed.

## Provider independence and testing

C1 selects Gemini, OpenAI, or offline behavior behind a Python provider abstraction. Provider keys belong to Python, never React/Flutter. Internal service authentication is supported; see the service README for its optional-key behavior and unprotected health route.

Normal .NET tests replace the Python client with a fake and do not require Python running. Python tests isolate providers. Frontend tests mock the public API. See the [testing guide](../docs/development/testing-guide.md) for exact commands and separate live smoke verification.

## Documentation

- [C1 service: setup, graph, providers, contract, security](problem-understanding-agent/README.md)
- [C1 domain, clarification, location, and public API](../docs/components/component-1-problem-understanding/README.md)
- [Historical architecture and viva evidence](../docs/agents/c1-architecture-evidence.md)
- [Component ownership and boundaries](../docs/architecture/component-boundaries.md)

Other component requirements describe planned responsibilities; this page does not prescribe or claim their implementation.
