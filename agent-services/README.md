# AssistLK Agentic AI Architecture

`agent-services/` contains internal Python reasoning services. Component 1 uses the [Problem Understanding service](problem-understanding-agent/README.md); Component 3 uses the [Quotation & Booking service](quotation-booking-agent/README.md) through its ASP.NET application workflow.

## Trust and execution boundaries

```text
Flutter / React -> ASP.NET Core -> Application workflow
 -> AgentOrchestrator -> AgentRegistry -> .NET adapter / HTTP client
 -> internal Python FastAPI service -> LangGraph
```

ASP.NET owns authentication, authorization, lifecycle, deterministic domain validation, PostgreSQL persistence, and failure recovery. Python owns request-scoped graph state and reasoning. Frontends never call Python directly, and Python has no direct database ownership.

C1 transport failures return through its adapter to workflow recovery. Component 3's `QuotationService` calls the Python start/resume client; ASP.NET validates the returned quotation decision and performs the booking or rejection. C3 thread checkpoints currently live in Python memory and are lost on service restart. Detailed contracts, configuration, failure behavior, setup, and tests live in each service README. C1 has no native C# fallback or runtime mode switch.

## Shared foundation and integration principles

The C1 .NET adapter implements `IAgent`, accepts `AgentContext`, and is registered with dependency injection and `AgentRegistry`. The C3 typed HTTP client is invoked by its application service, not by the C1 orchestrator. Shared C# `IAgentTool`, `ToolRegistry`, and `ToolExecutor` abstractions are distinct from Python functions; Python does not implement C# interfaces.

Application services load and persist concise workflow memory through `AgentMemoryService`/`AgentContextService`. Agents exchange approved structured context through application workflows, not direct agent-to-agent calls. Python graph state lasts for the request and is not persisted workflow memory.

ASP.NET safety and authorization govern application actions and approval requirements. Python's C3 approval interrupt records the human decision but cannot create a booking; ASP.NET applies that decision under the authenticated customer's identity. Python guardrails and approval gates are separate from application authorization.

Monitoring records execution identity, status, duration, errors, and tool usage through the backend. Python returns execution metadata; ASP.NET records authoritative metrics and audit evidence. Illustrative metrics are not benchmark results. Hidden reasoning is neither persisted nor exposed.

## Provider independence and testing

C1 selects Gemini, OpenAI, or offline behavior behind a Python provider abstraction. Provider keys belong to Python, never React/Flutter. Internal service authentication is supported; see the service README for its optional-key behavior and unprotected health route.

Normal .NET tests replace the Python client with a fake and do not require Python running. Python tests isolate providers. Frontend tests mock the public API. See the [testing guide](../docs/development/testing-guide.md) for exact commands and separate live smoke verification.

## Documentation

- [C1 service: setup, graph, providers, contract, security](problem-understanding-agent/README.md)
- [C3 service: setup, quotation approval workflow, contract, security](quotation-booking-agent/README.md)
- [C1 domain, clarification, location, and public API](../docs/components/component-1-problem-understanding/README.md)
- [Historical architecture and viva evidence](../docs/agents/c1-architecture-evidence.md)
- [Component ownership and boundaries](../docs/architecture/component-boundaries.md)

Component 2 and Component 4 requirements describe planned responsibilities; this page does not prescribe or claim their implementation.
