# Component Ownership Matrix

> **Authoritative Specification:** For full architectural boundaries, data ownership, allowed interactions, and handoff contracts, see **[Component Boundaries](architecture/component-boundaries.md)**.

## Team Ownership Mapping

| Member | Component | Domain Scope | Primary AI Agent | Frontend Feature Directories |
|---|---|---|---|---|
| **Member 1** | **Component 1** | Service Request & Problem Understanding | `ProblemUnderstandingAgent` (Python, via ASP.NET adapter) | Web: `web/src/features/serviceRequests/`<br>Mobile: `mobile/lib/features/service_requests/` |
| **Member 2** | **Component 2** | Provider Management & Intelligent Matching | `ProviderMatchingAgent` (.NET or Python) | Web: `web/src/features/providers/`<br>Mobile: `mobile/lib/features/providers/` |
| **Member 3** | **Component 3** | Quotation, Booking & Service Coordination | `ServiceCoordinationAgent` (.NET or Python) | Web: `web/src/features/quotations/`<br>Mobile: `mobile/lib/features/quotations/` |
| **Member 4** | **Component 4** | Service Tracking, Completion & Feedback | `ValidationSafetyAgent` (.NET or Python) | Web: `web/src/features/tracking/`<br>Mobile: `mobile/lib/features/tracking/` |

---

## Architectural Principles of Ownership

Ownership means the designated member is the primary technical lead and architectural custodian of the component across all tiers:

```text
Database Entities → Application Services → Agent Execution → React Pages → Flutter Screens → Automated Tests
```

### Invariants for All Members:
1. **Never bypass application service contracts:** Component 2 must not query or mutate Component 1's repository or database tables directly.
2. **Persistence belongs in Application:** Agents do not interact directly with Entity Framework `DbContext` or raw database connections.
3. **Contracts are published:** Cross-component integrations must adhere strictly to the published [Component Integration Contracts](api/component-contracts.md).
