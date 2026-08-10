# Backend

Mandatory public backend: **ASP.NET Core Web API**.

Recommended Phase 1 structure:

```text
backend/
├─ src/
│  ├─ AssistLK.Api
│  ├─ AssistLK.Application
│  ├─ AssistLK.Domain
│  └─ AssistLK.Infrastructure
└─ tests/
   ├─ AssistLK.Api.Tests
   └─ AssistLK.IntegrationTests
```

The real solution/project files should be scaffolded by the team during Phase 1.

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
