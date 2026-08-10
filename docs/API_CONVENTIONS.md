# API Conventions

Suggested route roots:

```text
/api/auth
/api/service-requests
/api/providers
/api/quotations
/api/bookings
/api/service-jobs
/api/ai-workflows
/api/reports
```

Rules:

- use DTOs,
- server-side validation,
- async I/O,
- correct HTTP status codes,
- JWT authentication,
- policy/role authorization,
- consistent error responses,
- pagination for large lists,
- Swagger/OpenAPI,
- important business rules in ASP.NET Core rather than only in the UI.
