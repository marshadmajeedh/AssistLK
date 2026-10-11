"""
Safe-failure and failure-recovery tests.

Maps to SE3110 AI sub-items:
  - safe-failure testing
  - failure-recovery testing
"""

import asyncio

from app.agent import analyze_quotation_node


# ------------------------------------------------------------------
# TC-C3-AI-09 — Malformed JSON sets llm_error, does not crash
# ------------------------------------------------------------------
def test_malformed_json_sets_llm_error(patch_chat_model, malformed_json_model):
    patch_chat_model(malformed_json_model)

    state = {
        "quotation_id": 1,
        "items": [{"description": "Repair", "amount": 1000, "quantity": 1}],
        "total_amount": 1000.0,
        "validation_passed": True,
    }

    result = asyncio.run(analyze_quotation_node(state))
    assert result["risk_assessment"] is None
    assert result["llm_error"] is not None
    assert "non-JSON" in result["llm_error"] or "JSON" in result["llm_error"]


# ------------------------------------------------------------------
# TC-C3-AI-10 — Runtime error in LLM sets llm_error, does not crash
# ------------------------------------------------------------------
def test_runtime_error_sets_llm_error(patch_chat_model, raising_model):
    patch_chat_model(raising_model)

    state = {
        "quotation_id": 1,
        "items": [{"description": "Repair", "amount": 1000, "quantity": 1}],
        "total_amount": 1000.0,
        "validation_passed": True,
    }

    result = asyncio.run(analyze_quotation_node(state))
    assert result["risk_assessment"] is None
    assert result["llm_error"] is not None
    assert "RuntimeError" in result["llm_error"]


# ------------------------------------------------------------------
# TC-C3-AI-11 — Timeout in LLM sets llm_error, does not crash
# ------------------------------------------------------------------
def test_timeout_sets_llm_error(patch_chat_model, timeout_model):
    patch_chat_model(timeout_model)

    state = {
        "quotation_id": 1,
        "items": [{"description": "Repair", "amount": 1000, "quantity": 1}],
        "total_amount": 1000.0,
        "validation_passed": True,
    }

    result = asyncio.run(analyze_quotation_node(state))
    assert result["risk_assessment"] is None
    assert "TimeoutError" in result["llm_error"]


# ------------------------------------------------------------------
# TC-C3-AI-12 — No LLM credentials -> workflow continues
# ------------------------------------------------------------------
def test_no_llm_credentials_continues_without_assessment(patch_chat_model):
    patch_chat_model(None)

    state = {
        "quotation_id": 1,
        "items": [{"description": "Repair", "amount": 1000, "quantity": 1}],
        "total_amount": 1000.0,
        "validation_passed": True,
    }

    result = asyncio.run(analyze_quotation_node(state))
    assert result["risk_assessment"] is None
    assert result["llm_error"] is not None
    assert "No LLM credentials" in result["llm_error"] or "credentials" in result["llm_error"].lower()


# ------------------------------------------------------------------
# TC-C3-AI-13 — Failed validation skips LLM entirely
# ------------------------------------------------------------------
def test_failed_validation_skips_llm(patch_chat_model, raising_model):
    patch_chat_model(raising_model)

    state = {
        "quotation_id": 1,
        "items": [{"description": "Repair", "amount": 0, "quantity": 1}],
        "validation_passed": False,
    }

    result = asyncio.run(analyze_quotation_node(state))
    assert result == {}


# ------------------------------------------------------------------
# TC-C3-AI-14 — Backend unreachable does not break the workflow
# ------------------------------------------------------------------
def test_backend_unreachable_does_not_break_validate(patch_get_quotation):
    patch_get_quotation(raise_exc=RuntimeError("connection refused"))

    from app.agent import validate_node

    state = {
        "quotation_id": 1,
        "items": [{"description": "Repair", "amount": 1000, "quantity": 1}],
    }

    result = asyncio.run(validate_node(state))
    assert result["validation_passed"] is True
    assert result["total_amount"] == 1000.0