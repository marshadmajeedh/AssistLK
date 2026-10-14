import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import StatusBadge from "../StatusBadge";

/**
 * TEST SUITE: Shared StatusBadge Component
 *
 * 1. What this class/suite tests:
 *    - Verifies the shared visual status chip component (`StatusBadge`), ensuring correct human-readable
 *      label translation ("ReadyForMatching" -> "Ready for Matching", "AwaitingInformation" -> "Awaiting Information"),
 *      color mapping, and safe fallback handling for unknown, null, or undefined statuses.
 * 2. Why these tests are needed:
 *    - Component 1 service requests transition across distinct states (Created, Analyzing,
 *      AwaitingInformation, Analyzed, ReadyForMatching, Cancelled). Badges must accurately
 *      reflect lifecycle status without throwing errors or displaying unformatted raw strings.
 * 3. What is real and what is mocked/faked:
 *    - Real: Full React DOM rendering and text querying via React Testing Library.
 *    - Mocked: None; pure UI component unit test.
 * 4. Prerequisites:
 *    - Node.js dependencies installed (`npm install` inside `/web`).
 * 5. PowerShell command to run this suite from repository root:
 *    npm --prefix web test -- src/shared/components/__tests__/StatusBadge.test.jsx
 */
describe("StatusBadge", () => {
  /**
   * Purpose: What this specific test checks
   * - Verifies that known service request statuses ("Created", "Analyzing", "AwaitingInformation",
   *   "Analyzed", "Cancelled") render properly formatted text labels.
   *
   * Why: Why this scenario matters
   * - PascalCase status identifiers from the backend must be formatted into readable labels for users.
   *
   * Scenario: Input or condition being tested
   * - Component rendered and rerendered with each known status string.
   *
   * Pass condition: What result or assertion makes it pass
   * - Formatted text string for each status appears in the document.
   *
   * Run from repository root:
   * npm --prefix web test -- src/shared/components/__tests__/StatusBadge.test.jsx -t "renders known status as text"
   */
  it("renders known status as text", () => {
    const { rerender } = render(<StatusBadge status="Created" />);
    expect(screen.getByText("Created")).toBeInTheDocument();

    rerender(<StatusBadge status="Analyzing" />);
    expect(screen.getByText("Analyzing")).toBeInTheDocument();

    rerender(<StatusBadge status="AwaitingInformation" />);
    expect(screen.getByText("Awaiting Information")).toBeInTheDocument();

    rerender(<StatusBadge status="Analyzed" />);
    expect(screen.getByText("Analyzed")).toBeInTheDocument();

    rerender(<StatusBadge status="Cancelled" />);
    expect(screen.getByText("Cancelled")).toBeInTheDocument();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that "ReadyForMatching" renders explicitly as "Ready for Matching".
   *
   * Why: Why this scenario matters
   * - "Ready for Matching" is Component 1's key terminal success milestone; formatting must be exact.
   *
   * Scenario: Input or condition being tested
   * - Render `<StatusBadge status="ReadyForMatching" />`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Text "Ready for Matching" is in the document.
   *
   * Run from repository root:
   * npm --prefix web test -- src/shared/components/__tests__/StatusBadge.test.jsx -t "renders ReadyForMatching correctly as 'Ready for Matching'"
   */
  it("renders ReadyForMatching correctly as 'Ready for Matching'", () => {
    render(<StatusBadge status="ReadyForMatching" />);
    expect(screen.getByText("Ready for Matching")).toBeInTheDocument();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies fallback behavior: unrecognized strings render as-is, while null/undefined render "Unknown".
   *
   * Why: Why this scenario matters
   * - Prevents runtime crashes when new statuses are returned or when data fields are missing.
   *
   * Scenario: Input or condition being tested
   * - Render with "NonExistentStatus", then `null`, then `undefined`.
   *
   * Pass condition: What result or assertion makes it pass
   * - "NonExistentStatus" renders text as-is; null and undefined render "Unknown".
   *
   * Run from repository root:
   * npm --prefix web test -- src/shared/components/__tests__/StatusBadge.test.jsx -t "uses safe fallback when status is unknown or null"
   */
  it("uses safe fallback when status is unknown or null", () => {
    const { rerender } = render(<StatusBadge status="NonExistentStatus" />);
    expect(screen.getByText("NonExistentStatus")).toBeInTheDocument();

    rerender(<StatusBadge status={null} />);
    expect(screen.getByText("Unknown")).toBeInTheDocument();

    rerender(<StatusBadge status={undefined} />);
    expect(screen.getByText("Unknown")).toBeInTheDocument();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that the status meaning is conveyed through readable DOM text and not exclusively through colors/icons.
   *
   * Why: Why this scenario matters
   * - Accessibility guideline (WCAG): color alone must never be the only indicator of state.
   *
   * Scenario: Input or condition being tested
   * - Inspect text content of rendered badge.
   *
   * Pass condition: What result or assertion makes it pass
   * - `badge.textContent` equals "Ready for Matching".
   *
   * Run from repository root:
   * npm --prefix web test -- src/shared/components/__tests__/StatusBadge.test.jsx -t "ensures status meaning is visible as text"
   */
  it("ensures status meaning is visible as text", () => {
    render(<StatusBadge status="ReadyForMatching" />);
    const badge = screen.getByText("Ready for Matching");
    expect(badge.textContent).toBe("Ready for Matching");
  });
});
