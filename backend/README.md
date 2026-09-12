# Backend

Mandatory public backend: **ASP.NET Core Web API**.

Existing solution structure:

```text
backend/
├─ src/
│  ├─ AssistLK.Api
│  ├─ AssistLK.Application
│  ├─ AssistLK.Domain
│  ├─ AssistLK.Infrastructure
│  └─ AssistLK.Agents
└─ tests/
   ├─ AssistLK.Api.Tests
   └─ AssistLK.IntegrationTests
```

The existing solution is `AssistLK.sln`. Component 1 uses the external Python adapter in `AssistLK.Agents`; ASP.NET owns authorization, lifecycle, persistence, monitoring, and failure recovery. See [agent architecture](../agent-services/README.md) and [Python service integration](../agent-services/problem-understanding-agent/README.md#aspnet-integration).

Required concerns:

- Controllers
- DTOs
- services/application layer
- dependency injection
- EF Core/PostgreSQL
- JWT
- role authorization
- validation
- global error handling
- structured logging
- CORS
- Swagger
- AI workflow endpoints
- approval endpoints
- tests
