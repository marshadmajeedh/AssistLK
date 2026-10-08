"""
Tool selection and safe-failure tests for HTTP tools.

Maps to SE3110 AI sub-items:
  - tool-selection testing
  - safe-failure testing at the tool boundary

Note: tools.py calls load_dotenv() at import time, so SERVICE_BEARER_TOKEN
is captured into a module-level constant before tests can set env vars.
These tests patch the module attribute directly.
"""

import asyncio
import pytest


# ------------------------------------------------------------------
# TC-C3-AI-27 — Authorization header is added when token present
# ------------------------------------------------------------------
def test_headers_include_bearer_when_token_set(monkeypatch):
    import app.tools as tools_module

    monkeypatch.setattr(tools_module, "BEARER_TOKEN", "abc123")
    h = tools_module._headers()

    assert h["Authorization"] == "Bearer abc123"
    assert h["Content-Type"] == "application/json"


# ------------------------------------------------------------------
# TC-C3-AI-28 — Authorization header omitted when token empty
# ------------------------------------------------------------------
def test_headers_omit_bearer_when_token_empty(monkeypatch):
    import app.tools as tools_module

    monkeypatch.setattr(tools_module, "BEARER_TOKEN", "")
    h = tools_module._headers()

    assert "Authorization" not in h


# ------------------------------------------------------------------
# TC-C3-AI-29 — get_quotation raises on non-2xx
# ------------------------------------------------------------------
def test_get_quotation_raises_on_failure(monkeypatch):
    class _FailingClient:
        def __init__(self, *_, **__):
            pass

        async def __aenter__(self):
            return self

        async def __aexit__(self, *_, **__):
            return False

        async def get(self, *_, **__):
            class _Resp:
                status_code = 500

                def raise_for_status(self):
                    raise RuntimeError("HTTP 500")

            return _Resp()

    import app.tools as tools_module
    monkeypatch.setattr(tools_module.httpx, "AsyncClient", _FailingClient)

    with pytest.raises(RuntimeError):
        asyncio.run(tools_module.get_quotation(1))


# ------------------------------------------------------------------
# TC-C3-AI-30 — get_quotation returns parsed JSON on success
# ------------------------------------------------------------------
def test_get_quotation_returns_json(monkeypatch):
    class _OkClient:
        def __init__(self, *_, **__):
            pass

        async def __aenter__(self):
            return self

        async def __aexit__(self, *_, **__):
            return False

        async def get(self, *_, **__):
            class _Resp:
                status_code = 200

                def raise_for_status(self):
                    pass

                def json(self):
                    return {"totalAmount": 1500.0}

            return _Resp()

    import app.tools as tools_module
    monkeypatch.setattr(tools_module.httpx, "AsyncClient", _OkClient)

    result = asyncio.run(tools_module.get_quotation(1))
    assert result["totalAmount"] == 1500.0


# ------------------------------------------------------------------
# TC-C3-AI-31 — BACKEND_URL is defined and normalized
# ------------------------------------------------------------------
def test_backend_url_is_normalized():
    import app.tools as tools_module
    assert tools_module.BACKEND_URL
    assert not tools_module.BACKEND_URL.endswith("/")