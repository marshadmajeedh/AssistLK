import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, it, expect, vi } from "vitest";
import ClarificationSection from "../ClarificationSection";

describe("ClarificationSection", () => {
  const followUpQuestions = [
    "What is the diameter of the leaking pipe?",
    "Is the main water valve shut off?",
  ];

  it("renders follow-up questions when provided", () => {
    render(
      <ClarificationSection
        followUpQuestions={followUpQuestions}
        onEdit={vi.fn()}
        onAnalyzeAgain={vi.fn()}
      />
    );

    expect(screen.getByText("More information is needed")).toBeInTheDocument();
    expect(
      screen.getByText("What is the diameter of the leaking pipe?")
    ).toBeInTheDocument();
    expect(
      screen.getByText("Is the main water valve shut off?")
    ).toBeInTheDocument();
  });

  it("does not fabricate questions when follow-up questions array is empty", () => {
    render(
      <ClarificationSection
        followUpQuestions={[]}
        onEdit={vi.fn()}
        onAnalyzeAgain={vi.fn()}
      />
    );

    expect(screen.getByText("More information is needed")).toBeInTheDocument();
    expect(
      screen.queryByText("What is the diameter of the leaking pipe?")
    ).not.toBeInTheDocument();
  });

  it("triggers onEdit and onAnalyzeAgain callbacks", async () => {
    const user = userEvent.setup();
    const onEdit = vi.fn();
    const onAnalyzeAgain = vi.fn();

    render(
      <ClarificationSection
        followUpQuestions={followUpQuestions}
        onEdit={onEdit}
        onAnalyzeAgain={onAnalyzeAgain}
        isAnalyzing={false}
      />
    );

    const editBtn = screen.getByRole("button", { name: /edit request/i });
    await user.click(editBtn);
    expect(onEdit).toHaveBeenCalledTimes(1);

    const analyzeBtn = screen.getByRole("button", { name: /analyze again/i });
    await user.click(analyzeBtn);
    expect(onAnalyzeAgain).toHaveBeenCalledTimes(1);
  });

  it("disables Analyze Again button when isAnalyzing is true", () => {
    render(
      <ClarificationSection
        followUpQuestions={followUpQuestions}
        onEdit={vi.fn()}
        onAnalyzeAgain={vi.fn()}
        isAnalyzing={true}
      />
    );

    const analyzeBtn = screen.getByRole("button", {
      name: /analyzing problem\.\.\./i,
    });
    expect(analyzeBtn).toBeDisabled();
  });
});
