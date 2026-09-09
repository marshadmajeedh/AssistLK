# Architecture

```text
Flutter ─────┐
             v
        ASP.NET Core Web API ───── PostgreSQL
             ^
React ───────┘
             |
             +──── Agentic AI internal service
             |
             +──── Maps / Location service
```

## Core rules

1. ASP.NET Core is the public backend.
2. React and Flutter never access PostgreSQL directly.
3. React and Flutter use the same authentication, permissions and business rules.
4. A separate Python agent service, if used, is internal and called by ASP.NET Core.
5. Important AI actions are validated and can be paused for human approval.
6. AI workflow state is stored as structured data, not hidden chain-of-thought.
