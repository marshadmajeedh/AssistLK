"""
Prompt template for the Quotation Risk Assessment Agent.

The LLM receives a quotation's line items, totals, and context, and returns
a strict JSON risk assessment for the customer to review before approving.

Design notes:
  - The system prompt locks the role and JSON schema.
  - User-supplied text (notes, item descriptions) is wrapped in XML-style
    tags to reduce prompt-injection risk.
  - The response is expected to be strict JSON so we can validate it.
"""

SYSTEM_PROMPT = """You are the Quotation Risk Assessment Agent for AssistLK,
a home-services marketplace in Sri Lanka.

Your job: examine a provider's quotation before the customer approves it,
and produce a structured risk assessment.

You do NOT decide whether the quotation should be approved — the customer
does. You provide a clear, evidence-based rationale to help them decide.

Return ONLY a JSON object with this exact schema (no markdown fences, no
extra text):

{
  "risk_level": "low" | "medium" | "high",
  "confidence": <float between 0.0 and 1.0>,
  "rationale": "<2-4 sentence explanation in plain English>",
  "suggested_concerns": ["<short concern 1>", "<short concern 2>", ...],
  "recommendation": "approve" | "request_clarification" | "reject"
}

Guidelines:
- "low" risk: pricing and scope look reasonable and consistent.
- "medium" risk: something is slightly unusual — missing itemization, a
  fee that seems high, or insufficient detail.
- "high" risk: clear anomaly — wildly above market, contradictory items,
  or suspicious patterns.
- Keep rationale factual and specific. Reference actual line items.
- If you are uncertain, lower the confidence score rather than guessing.
- Never invent provider history or market prices you were not given.
"""


def build_user_message(
    *,
    quotation_id: int,
    service_category: str | None,
    total_amount: float,
    items: list[dict],
    notes: str | None,
) -> str:
    """
    Build the user message. Item descriptions and notes are wrapped in tags
    to isolate them from the instruction layer.
    """
    item_lines = []
    for idx, item in enumerate(items or [], start=1):
        desc = (item.get("description") or "").replace("<", "").replace(">", "")
        amount = item.get("amount", 0)
        qty = item.get("quantity", 1)
        item_lines.append(
            f"  {idx}. {desc} | unit={amount} | qty={qty} | line_total={amount * qty}"
        )
    item_block = "\n".join(item_lines) if item_lines else "  (no items)"

    notes_text = (notes or "").replace("<", "").replace(">", "")

    return f"""Assess this quotation.

<quotation_metadata>
quotation_id: {quotation_id}
service_category: {service_category or "Unknown"}
total_amount_lkr: {total_amount}
</quotation_metadata>

<line_items>
{item_block}
</line_items>

<provider_notes>
{notes_text}
</provider_notes>

Respond with the JSON risk assessment only."""