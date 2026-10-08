import unittest
from datetime import datetime, timedelta, timezone

from pydantic import ValidationError

from app import main
from app.models.request import ValidationRequest

START = datetime(2026, 10, 4, 10, 0, tzinfo=timezone.utc)


class Agent4BoundaryCases(unittest.IsolatedAsyncioTestCase):
    async def test_exactly_120_seconds_is_not_suspicious(self):
        r = await main.validate_completion(
            main.CompletionValidationRequest(
                job_started_at=START,
                job_completed_at=START + timedelta(seconds=120),
            )
        )
        self.assertFalse(r.is_suspicious)
        self.assertEqual(r.duration_seconds, 120)

    async def test_completion_before_start_is_rejected(self):
        with self.assertRaises(ValidationError):
            main.CompletionValidationRequest(
                job_started_at=START,
                job_completed_at=START - timedelta(seconds=5),
            )

    async def test_positive_review_is_not_escalated(self):
        r = await main.analyze_review_sentiment(
            main.SentimentAnalysisRequest(rating=5, review_text="Great and quick service.")
        )
        self.assertFalse(r.should_escalate)
        self.assertEqual(r.sentiment, "POSITIVE")

    async def test_rating_boundary_three_without_negative_terms(self):
        r = await main.analyze_review_sentiment(
            main.SentimentAnalysisRequest(rating=3, review_text="It was okay.")
        )
        self.assertFalse(r.should_escalate)

    async def test_rating_out_of_range_is_rejected(self):
        with self.assertRaises(ValidationError):
            main.SentimentAnalysisRequest(rating=6, review_text="fine")

    async def test_status_validation_is_case_insensitive(self):
        req = ValidationRequest(
            job_id="j1", current_status="  InProgress ", target_status="COMPLETED", elapsed_minutes=5
        )
        self.assertEqual(req.current_status, "InProgress")


if __name__ == "__main__":
    unittest.main()