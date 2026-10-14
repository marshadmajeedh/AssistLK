// What this suite tests:
//   The AnalysisResultCard React component which presents AI problem analysis outputs
//   (category, urgency, problem summary, confidence percentage score, and fallback placeholders)
//   to the customer on the service request details page.
// Why these tests are needed:
//   Guarantees that analysis outputs format properly (e.g. 0.92 rendered as "92%") and that missing
//   transient fields fall back gracefully without crashing the UI.
// What is real and what is mocked/faked:
//   Real component rendered using @testing-library/react in jsdom. No network calls or mocks needed.
// Prerequisites:
//   Node.js dependencies installed in web folder (npm install).
// Run from repository root:
//   npm --prefix web test -- src/features/serviceRequests/components/__tests__/AnalysisResultCard.test.jsx

import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import AnalysisResultCard from "../AnalysisResultCard";

describe("AnalysisResultCard", () => {
  // Purpose: Checks that valid analysis props (category, urgency, summary, confidence) are correctly formatted and rendered.
  // Why: Ensures customers see complete AI problem analysis details with confidence shown as an integer percentage.
  // Scenario: Passing category="Plumbing", urgency="High", summary="Kitchen pipe burst...", confidence=0.92.
  // Pass condition: All text values and "92%" are visible in the document.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/components/__tests__/AnalysisResultCard.test.jsx -t "renders category, urgency, problem summary, and formatted confidence"
  it("renders category, urgency, problem summary, and formatted confidence", () => {
    render(
      <AnalysisResultCard
        category="Plumbing"
        urgency="High"
        problemSummary="Kitchen pipe burst causing flooding"
        confidence={0.92}
        status="Analyzed"
      />
    );

    expect(screen.getByText("Problem Analysis")).toBeInTheDocument();
    expect(screen.getByText("Plumbing")).toBeInTheDocument();
    expect(screen.getByText("High")).toBeInTheDocument();
    expect(screen.getByText("Kitchen pipe burst causing flooding")).toBeInTheDocument();
    expect(screen.getByText("92%")).toBeInTheDocument();
  });

  // Purpose: Checks that null or missing problem summary and confidence props render safe "Not available" fallback labels.
  // Why: Prevents component crashes or blank screens when optional analysis fields are empty.
  // Scenario: Passing null for problemSummary and confidence.
  // Pass condition: Two "Not available" fallback labels are rendered in the DOM.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/components/__tests__/AnalysisResultCard.test.jsx -t "handles missing/null transient fields gracefully with 'Not available' fallback"
  it("handles missing/null transient fields gracefully with 'Not available' fallback", () => {
    render(
      <AnalysisResultCard
        category="Electrical"
        urgency="Medium"
        problemSummary={null}
        confidence={null}
        status="Analyzed"
      />
    );

    expect(screen.getByText("Electrical")).toBeInTheDocument();
    expect(screen.getByText("Medium")).toBeInTheDocument();
    expect(screen.getAllByText("Not available")).toHaveLength(2);
  });

  // Purpose: Checks that omitting props altogether defaults category to "Unclassified" and urgency to "Unknown".
  // Why: Guarantees stable defaults when rendered before initial analysis is completed.
  // Scenario: Rendering <AnalysisResultCard /> with no props.
  // Pass condition: "Unclassified" and "Unknown" text strings are found in the rendered output.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/components/__tests__/AnalysisResultCard.test.jsx -t "falls back to Unclassified and Unknown when category and urgency are missing"
  it("falls back to Unclassified and Unknown when category and urgency are missing", () => {
    render(<AnalysisResultCard />);

    expect(screen.getByText("Unclassified")).toBeInTheDocument();
    expect(screen.getByText("Unknown")).toBeInTheDocument();
  });
});
