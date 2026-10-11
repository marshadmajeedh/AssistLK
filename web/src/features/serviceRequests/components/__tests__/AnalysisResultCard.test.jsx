import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import AnalysisResultCard from "../AnalysisResultCard";

describe("AnalysisResultCard", () => {
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

  it("falls back to Unclassified and Unknown when category and urgency are missing", () => {
    render(<AnalysisResultCard />);

    expect(screen.getByText("Unclassified")).toBeInTheDocument();
    expect(screen.getByText("Unknown")).toBeInTheDocument();
  });
});
