import unittest
from datetime import datetime, timedelta, timezone
from unittest.mock import Mock, patch

from pydantic import ValidationError

from app import agent, main
from app.agent import ValidationOutput
from app.models.request import ValidationRequest


class Agent4GoldenCases(unittest.IsolatedAsyncioTestCase):
    async def test_rapid_completion_under_two_minutes_is_flagged_suspicious(self):
        started_at = datetime(2026, 10, 4, 10, 0, tzinfo=timezone.utc)
        completion = await main.validate_completion(
            main.CompletionValidationRequest(
                job_started_at=started_at,
                job_completed_at=started_at + timedelta(seconds=119),
            )
        )

        self.assertTrue(completion.is_suspicious)
        self.assertEqual(completion.duration_seconds, 119)

        structured_llm = Mock()
        structured_llm.with_structured_output.return_value.invoke.return_value = (
            ValidationOutput(
                is_valid=False,
                status="SUSPICIOUS",
                reason="Completed in less than two minutes.",
            )
        )
        with patch("app.agent.get_llm", return_value=structured_llm):
            result = agent.run_tracking_validation(
                job_id="job-rapid-completion",
                current_status="InProgress",
                target_status="Completed",
                elapsed_minutes=119 / 60,
            )

        prompt = structured_llm.with_structured_output.return_value.invoke.call_args.args[0]
        self.assertIn("Current Status: InProgress", prompt)
        self.assertIn("Target Status: Completed", prompt)
        self.assertIn("LESS than 2.0 minutes", prompt)
        self.assertEqual(result["status"], "SUSPICIOUS")

    async def test_low_rating_or_rude_or_damaged_review_escalates(self):
        cases = [
            (1, "The service was acceptable."),
            (2, "The repair was completed."),
            (5, "The agent was rude during the visit."),
            (5, "The visit left my property damaged."),
        ]

        for rating, review_text in cases:
            with self.subTest(rating=rating, review_text=review_text):
                result = await main.analyze_review_sentiment(
                    main.SentimentAnalysisRequest(
                        rating=rating,
                        review_text=review_text,
                    )
                )
                self.assertTrue(result.should_escalate)

    async def test_pydantic_guardrail_rejects_unsupported_status(self):
        with self.assertRaises(ValidationError):
            ValidationRequest(
                job_id="job-invalid-status",
                current_status="Assigned",
                target_status="Teleport",
                elapsed_minutes=5,
                note="Invalid status should be rejected.",
            )


if __name__ == "__main__":
    unittest.main()