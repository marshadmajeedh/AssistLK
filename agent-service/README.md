# Agentic AI Internal Service

If the team uses Python/LangGraph or another separate service, this service is **internal**.

React and Flutter must not call it directly.

ASP.NET Core should initiate workflows and expose public workflow/approval endpoints.

Folders:

```text
agents/
orchestration/
tools/
schemas/
tests/
```

Agent behaviour must be controlled through:

- clear responsibility,
- input/output schema,
- allow-listed tools,
- validation,
- retry limits,
- safe failure,
- persisted structured state,
- human approval where required.
