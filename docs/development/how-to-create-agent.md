# Creating an Agent in AssistLK

AssistLK supports two agent development models depending on your technical requirements:

1. **[Native .NET Agent Guide](how-to-create-dotnet-agent.md) (In-Process):**
   - Implemented in C# (.NET 8) in `backend/src/AssistLK.Agents/`.
   - Used for core workflow reasoning, fast response times, and seamless dependency injection within the ASP.NET Core application runtime.
   - See: **[How to Create a .NET Agent](how-to-create-dotnet-agent.md)**.

2. **[External Python Agent Guide](../architecture/external-python-agent-service.md) (Out-of-Process):**
   - Implemented in Python 3.11+ using FastAPI, LangChain, LangGraph, or specialized ML packages in `agent-services/<service-name>/`.
   - Used when team members require Python-specific AI/ML ecosystems or advanced graph state machines.
   - Bound to .NET via the standardized [External Agent Contract](../architecture/external-agent-contract.md).
   - See: **[External Python Agent Service](../architecture/external-python-agent-service.md)**.

---

## Quick Reference: Clean Architecture Invariant
Regardless of whether you choose C# (.NET 8) or Python:
- **Agents NEVER directly access `AssistLKDbContext` or EF repositories.**
- **Agents NEVER mutate domain entities directly.**
- Application workflow services orchestrate state machines, validate outputs, and persist domain updates.
