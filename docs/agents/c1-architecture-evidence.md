# C1 Architecture Decisions and Verification Evidence

## Evidence scope and provenance

This document consolidates earlier architecture, cleanup, and native Gemini reports. **Historical / Superseded** sections describe an earlier implementation, not current instructions. Current authority is the [agent architecture](../../agent-services/README.md), [Python service guide](../../agent-services/problem-understanding-agent/README.md), and [C1 domain guide](../components/component-1-problem-understanding/README.md).

The original reports remain recoverable from Git revision `213108c81834d6768f2573604be6367afdc5843c`, the pre-cleanup revision. Use `git show <revision>:<original-path>` for an exact original record. Source names below identify historical records, not live documentation links. This cleanup does not rerun or re-certify their tests.

## Historical / Superseded: original native C# era

C1 originally used a native `ProblemUnderstandingAgent.cs`, `IGeminiService.cs`/`GeminiService.cs`, and C# `LocationExtractionTool.cs`, `ProblemClassificationTool.cs`, and `ServiceKnowledgeTool.cs`. The provider secret was then resolved through backend configuration. Those files and that runtime are removed; those setup instructions must not be followed for current C1.

| Historical source under the C1 component documentation directory | Unique finding preserved |
|---|---|
| `gemini-debug-report.md` | Native classification/tool failures could prevent the model call; DI lifetime mismatch and configuration tracing were investigated. Passing mocked tests did not prove runtime model calls. |
| `gemini-pipeline-fix-report.md` | The native pipeline was reordered to model reasoning followed by tool validation/enrichment, with a DI lifetime correction and regression scenarios. This is not the current Python graph order. |
| `gemini-tool-architecture.md` | Preserved the distinction between model reasoning, taxonomy normalization, optional enrichment, and mandatory safety. Deterministic alignment must not override a valid model category indiscriminately. |
| `gemini-runtime-debug-report.md` | Runtime failure and degraded output were traced separately from mocked/offline tests. The report recorded a model endpoint HTTP 404 and recommended model/retry changes. |
| `gemini-model-update-report.md` | The 2026-09-09 report recorded changing the native configured model from gemini-2.5-flash to gemini-3.6-flash, retrying transient 429/503 responses, failing fast on permanent errors, and a manual execution/result trace. Remote availability claims are historical observations, not current guarantees. |
| `gemini-integration-verification.md` | Native integration verification covered structured outputs, uncertainty and dangerous-advice handling, workflow persistence, API compatibility, and secret handling. Its mock/test evidence and manual observations apply to that implementation. |

These reports originally lived under `docs/components/component-1-problem-understanding/`. Their original results remain in the pinned Git revision; current documentation does not repeat outdated passing totals or raw customer/model payloads.

## Historical / Superseded: migration and cleanup decisions

Earlier documentation allowed in-process .NET agents or optional Python services. The generic Python cookbook demonstrated future ProviderMatching examples and a generic envelope. That was not the final C1 schema. The current service returns clarification/confidence inside `result`, plus `errorMessage` and `metadata`, and uses the configured 45-second .NET timeout by default.

The following source groups were consolidated:

| Historical source | Preserved decision / replacement |
|---|---|
| `docs/architecture/agent-foundation.md` | Registry dispatch, context, persisted memory, safety, and monitoring remain in ASP.NET; see shared architecture. |
| `docs/architecture/external-python-agent-service.md` | Internal service isolation, typed adapter integration, local execution, and deployment boundaries; current concrete setup replaces hypothetical code. |
| `docs/architecture/external-agent-contract.md` | No frontend-to-Python calls, no DB credentials/ownership, minimal structured data, timeout/failure handling; actual schemas replace the generic contract. |
| `docs/development/how-to-create-agent.md` and `how-to-create-dotnet-agent.md` | Single responsibility, typed contracts, adapter registration, output validation, and isolated tests survive; removed Gemini-interface examples do not. |
| `docs/component-agent-integration.md`, `docs/memory-context.md`, `docs/monitoring-evaluation.md`, `docs/safety-policy.md` | Shared context, structured memory, execution metrics, and approval boundaries are explained together in the architecture README. Example metric values were illustrative, not performance evidence. |
| `docs/archive/AI_WORKFLOW.md` | Assignment value: objective, structured plan/state, validated tool results, approval boundaries, errors/retry status, and auditable outcomes. These are conceptual assessment topics, not claims every field or agent is implemented. |
| `docs/architecture/python-agent-documentation-update-report.md` | Records the earlier dual-model documentation decision. Its boundary-isolation rationale remains useful; optional-runtime framing is superseded for C1. |
| `docs/cleanup/classification-agent-removal.md` and `classification-removal-report.md` | Removal of the old demonstration classifier, preservation of then-current support tools, and boundary verification. Those C# support tools were subsequently removed with the native runtime. |
| `docs/cleanup/final-cleanup-report.md` | Earlier removal/registration decisions and build/test observations. Its conclusion that all reasoning ran natively is superseded. |

The short archived agent/tool/workflow overviews and obsolete unused-files report were superseded by these destinations. Broad cleanup and UI audits retain their original text under prominent historical notices. Repaired links lead to current replacements, not the historical source version.

## Python migration and native runtime removal

Current code registers only `ExternalProblemUnderstandingAgentAdapter` for logical name `ProblemUnderstandingAgent`. `ProblemUnderstandingHttpClient` delegates to FastAPI; Python runs LangGraph. There is no `NativeCSharp`/`ExternalPython` mode switch, `ProblemUnderstandingMode`, or native fallback. The named modes here are obsolete concepts, not available configuration.

The migration retains ASP.NET authorization, lifecycle, persistence, deterministic domain validation, monitoring, and recovery. Python owns request-scoped state, reasoning, deterministic functions, providers, ambiguity evaluation, and guardrails. Removing the native runtime does not remove shared C# orchestration interfaces or dictate future component implementations.

## Gemini verification history and OpenAI limitation

**Historical:** Native Gemini reports contain manual execution evidence for the earlier runtime. They cannot prove successful Python Gemini execution.

**Prior verification reported by the project owner:** Gemini live execution through the evolved service was previously verified. This documentation cleanup does not independently repeat that execution or infer it from native-era logs.

**OpenAI limitation:** Prior verification established API reachability with invalid credentials, not successful live inference. Adapter support and mocked tests are not live-provider certification. Repository model defaults are configuration, not remote availability guarantees.

## Final architecture and viva explanation

Explain C1 as a distinct internal Agentic AI component: request-scoped LangGraph state carries grounded tool results into provider reasoning, then ambiguity evaluation and guardrails produce one structured result. ASP.NET validates and persists it. The service README owns the exact graph, including empty-input finalization.

Clarification is a backend-controlled two-round process: answered Round 2 still receives final re-analysis, and no third round is created. Smart Location reverse geocoding is separate Flutter/ASP.NET/Nominatim infrastructure. Provider independence comes from the Python abstraction, not multiple C1 runtimes.

Python can successfully return degraded structured analysis; transport failure instead enters ASP.NET recovery. Neither path silently selects a native agent. Persist structured facts and audit evidence, never hidden reasoning. Distinguish transient graph state from persisted workflow memory, and Python content guardrails from application approval policies.

## Final test and isolation principles

Normal .NET tests fake the Python client and do not require Python running. PostgreSQL tests retain dedicated database safeguards. Python tests isolate provider behavior; Flutter and React tests mock public APIs. The separate live smoke path can return early when unavailable, so a passing test alone does not prove live execution. See [commands and smoke limitations](../development/testing-guide.md).

No new passing totals or inference claims are introduced by documentation consolidation. Preserve reproducible commands, source provenance, observed failures, and verification limits rather than treating historical green reports as present-day certification.
