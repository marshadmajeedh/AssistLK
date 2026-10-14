// What this suite tests:
//   The ClarificationSection React component which displays AI-generated follow-up questions
//   when a request is in AwaitingInformation status, providing options to edit the request or re-analyze.
// Why these tests are needed:
//   Validates that questions from the problem understanding agent are rendered clearly, callbacks
//   for editing and re-analysis trigger properly on click, and the re-analyze button disables during active requests.
// What is real and what is mocked/faked:
//   - Real: ClarificationSection component rendered with @testing-library/react and @testing-library/user-event.
//   - Mocked/Faked: onEdit and onAnalyzeAgain event handler callbacks mocked via Vitest vi.fn().
// Prerequisites:
//   Node.js dependencies installed in web folder (npm install).
// Run from repository root:
//   npm --prefix web test -- src/features/serviceRequests/components/__tests__/ClarificationSection.test.jsx

import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, it, expect, vi } from "vitest";
import ClarificationSection from "../ClarificationSection";

describe("ClarificationSection", () => {
  const followUpQuestions = [
    "What is the diameter of the leaking pipe?",
    "Is the main water valve shut off?",
  ];

  // Purpose: Checks that follow-up questions passed via props are rendered into the list.
  // Why: Ensures the customer can read the exact clarification questions posed by the AI agent.
  // Scenario: Providing an array of two clarification questions.
  // Pass condition: Header "More information is needed" and both question strings are visible in document.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/components/__tests__/ClarificationSection.test.jsx -t "renders follow-up questions when provided"
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

  // Purpose: Checks that passing an empty questions array does not fabricate or hallucinate dummy questions.
  // Why: Preserves data integrity when a request needs more info but questions haven't arrived or were cleared.
  // Scenario: Providing empty array for followUpQuestions.
  // Pass condition: Header renders but specific question text is not in the document.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/components/__tests__/ClarificationSection.test.jsx -t "does not fabricate questions when follow-up questions array is empty"
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

  // Purpose: Checks that clicking "Edit Request" and "Analyze Again" fires the corresponding callback props.
  // Why: Guarantees button interactions connect to the parent page workflow logic.
  // Scenario: User clicks "Edit Request" and then clicks "Analyze Again".
  // Pass condition: onEdit and onAnalyzeAgain callbacks are each called exactly once.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/components/__tests__/ClarificationSection.test.jsx -t "triggers onEdit and onAnalyzeAgain callbacks"
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

  // Purpose: Checks that the "Analyze Again" button is disabled and displays a loading label while isAnalyzing is true.
  // Why: Prevents double-submissions and conveys loading progress while the AI workflow runs.
  // Scenario: Rendering with isAnalyzing={true}.
  // Pass condition: Button has accessible name matching /analyzing problem.../i and is disabled.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/components/__tests__/ClarificationSection.test.jsx -t "disables Analyze Again button when isAnalyzing is true"
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
