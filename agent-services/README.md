# AssistLK External Python Agent Services (`agent-services/`)

This directory is the canonical location for out-of-process Python agent microservices in the AssistLK ecosystem.

---

## Architectural Rules

1. **Internal Microservices Only:**
   Python agent services are internal reasoning workers. They are **never** directly accessible to React or Flutter frontend clients. All communication must pass through the ASP.NET Core Web API (`AssistLK.Api` / `AssistLK.Application`).

2. **No Direct Database Access:**
   Python agent services must **never** connect directly to the AssistLK PostgreSQL database, execute migrations, or instantiate EF Core models. All state and entity persistence is owned authoritatively by .NET.

3. **Adherence to Shared Contract:**
   All communication between .NET and Python services must adhere to the provider-neutral JSON specification defined in:
   👉 **[docs/architecture/external-agent-contract.md](../docs/architecture/external-agent-contract.md)**

4. **Authoritative Guide:**
   For complete instructions on building, containerizing, and integrating Python agents, see:
   👉 **[docs/architecture/external-python-agent-service.md](../docs/architecture/external-python-agent-service.md)**

---

## Recommended Service Directory Layout

When a teammate implements a Python agent (for example, for Component 2 Provider Matching), they should create their isolated service directory following this structure:

```text
agent-services/
├── README.md                          # This file
└── provider-matching-agent/           # Component 2 Python agent service (example)
    ├── Dockerfile                     # Container definition
    ├── requirements.txt               # Dependencies (FastAPI, pydantic, langchain, etc.)
    ├── README.md                      # Local service documentation
    ├── app/
    │   ├── __init__.py
    │   ├── main.py                    # FastAPI application entrypoint
    │   ├── agent.py                   # Agent reasoning pipeline
    │   ├── models/                    # Pydantic request/response schemas
    │   │   ├── request.py
    │   │   └── response.py
    │   ├── tools/                     # Python-native tools / ML models
    │   └── safety/                    # Input/output safety validation
    └── tests/
        ├── test_contract.py           # Contract compliance tests
        └── test_agent.py              # Unit tests with mocked LLM
```

> **Note for Teammates:** Do not commit empty placeholder logic or unverified dependencies. Only create a subfolder when you are actively implementing your component's agent service.
