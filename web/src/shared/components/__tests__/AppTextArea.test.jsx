import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import AppTextArea from "../AppTextArea";

/**
 * TEST SUITE: Shared AppTextArea Component
 *
 * 1. What this class/suite tests:
 *    - Verifies the shared multi-line text input component (`AppTextArea`), ensuring proper HTML `<label>`
 *      to `<textarea>` association (auto-generated vs explicit ID), `maxLength` character limits,
 *      and validation error display.
 * 2. Why these tests are needed:
 *    - Multiline text fields are central to problem descriptions and clarification responses.
 *      Accessibility and character bounding must be enforced consistently.
 * 3. What is real and what is mocked/faked:
 *    - Real: Full React DOM rendering and attribute inspection via React Testing Library.
 *    - Mocked: None; pure UI component unit test.
 * 4. Prerequisites:
 *    - Node.js dependencies installed (`npm install` inside `/web`).
 * 5. PowerShell command to run this suite from repository root:
 *    npm --prefix web test -- src/shared/components/__tests__/AppTextArea.test.jsx
 */
describe("AppTextArea", () => {
  /**
   * Purpose: What this specific test checks
   * - Verifies that the label text is rendered and correctly associated with the `<textarea>` element.
   *
   * Why: Why this scenario matters
   * - Ensures screen readers announce the field purpose when focused.
   *
   * Scenario: Input or condition being tested
   * - Render `<AppTextArea label="Description" />`.
   *
   * Pass condition: What result or assertion makes it pass
   * - `screen.getByLabelText("Description")` discovers the textarea element.
   *
   * Run from repository root:
   * npm --prefix web test -- src/shared/components/__tests__/AppTextArea.test.jsx -t "renders label and associates it with the textarea"
   */
  it("renders label and associates it with the textarea", () => {
    render(<AppTextArea label="Description" />);

    const textarea = screen.getByLabelText("Description");
    expect(textarea).toBeInTheDocument();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that passing an explicit `id` prop sets the textarea ID and pairs with the label `for`.
   *
   * Why: Why this scenario matters
   * - Supports predictable DOM IDs for forms and automated QA test suites.
   *
   * Scenario: Input or condition being tested
   * - Render `<AppTextArea label="Notes" id="special-notes-id" />`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Textarea element has `id="special-notes-id"`.
   *
   * Run from repository root:
   * npm --prefix web test -- src/shared/components/__tests__/AppTextArea.test.jsx -t "respects explicit id and associates with label"
   */
  it("respects explicit id and associates with label", () => {
    render(<AppTextArea label="Notes" id="special-notes-id" />);

    const textarea = screen.getByLabelText("Notes");
    expect(textarea).toHaveAttribute("id", "special-notes-id");
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that the `maxLength` HTML attribute is passed through to the textarea element.
   *
   * Why: Why this scenario matters
   * - Prevents users from exceeding backend database column limits (e.g. 500 characters).
   *
   * Scenario: Input or condition being tested
   * - Render `<AppTextArea label="Feedback" maxLength={500} />`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Textarea has attribute `maxLength="500"`.
   *
   * Run from repository root:
   * npm --prefix web test -- src/shared/components/__tests__/AppTextArea.test.jsx -t "supports maxLength attribute"
   */
  it("supports maxLength attribute", () => {
    render(<AppTextArea label="Feedback" maxLength={500} />);

    const textarea = screen.getByLabelText("Feedback");
    expect(textarea).toHaveAttribute("maxLength", "500");
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that passing an `error` prop displays the error message text in the DOM.
   *
   * Why: Why this scenario matters
   * - User must be alerted when input fails validation rules.
   *
   * Scenario: Input or condition being tested
   * - Render `<AppTextArea label="Feedback" error="Feedback cannot be empty." />`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Text "Feedback cannot be empty." is present in the document.
   *
   * Run from repository root:
   * npm --prefix web test -- src/shared/components/__tests__/AppTextArea.test.jsx -t "displays validation error when provided"
   */
  it("displays validation error when provided", () => {
    render(<AppTextArea label="Feedback" error="Feedback cannot be empty." />);

    expect(screen.getByText("Feedback cannot be empty.")).toBeInTheDocument();
  });
});
