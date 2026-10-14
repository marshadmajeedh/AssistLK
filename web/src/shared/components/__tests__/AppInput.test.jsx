import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import AppInput from "../AppInput";

/**
 * TEST SUITE: Shared AppInput Component
 *
 * 1. What this class/suite tests:
 *    - Verifies the shared form input component (`AppInput`), ensuring proper HTML `<label>`
 *      to `<input>` association (both auto-generated and explicit IDs), accessibility,
 *      and conditional error message rendering.
 * 2. Why these tests are needed:
 *    - Form inputs across the application (Create Request, Login, Profile) rely on `AppInput`.
 *      Broken `for`/`id` associations harm accessibility for assistive technology and screen readers.
 * 3. What is real and what is mocked/faked:
 *    - Real: React DOM rendering and DOM attribute querying via React Testing Library.
 *    - Mocked: None; pure UI component unit test.
 * 4. Prerequisites:
 *    - Node.js dependencies installed (`npm install` inside `/web`).
 * 5. PowerShell command to run this suite from repository root:
 *    npm --prefix web test -- src/shared/components/__tests__/AppInput.test.jsx
 */
describe("AppInput", () => {
  /**
   * Purpose: What this specific test checks
   * - Verifies that the label text is rendered and correctly associated with the input element.
   *
   * Why: Why this scenario matters
   * - Users clicking the label must focus the input, and screen readers must announce the label.
   *
   * Scenario: Input or condition being tested
   * - Render `<AppInput label="Phone Number" />`.
   *
   * Pass condition: What result or assertion makes it pass
   * - `screen.getByLabelText("Phone Number")` successfully discovers the input.
   *
   * Run from repository root:
   * npm --prefix web test -- src/shared/components/__tests__/AppInput.test.jsx -t "renders label and associates it with the input"
   */
  it("renders label and associates it with the input", () => {
    render(<AppInput label="Phone Number" />);

    const input = screen.getByLabelText("Phone Number");
    expect(input).toBeInTheDocument();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that passing an explicit `id` prop overrides any auto-generated identifier.
   *
   * Why: Why this scenario matters
   * - Forms with strict ID schemas (or automated testing selectors) need deterministic IDs.
   *
   * Scenario: Input or condition being tested
   * - Render `<AppInput label="Email Address" id="custom-email-id" />`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Input element has `id="custom-email-id"`.
   *
   * Run from repository root:
   * npm --prefix web test -- src/shared/components/__tests__/AppInput.test.jsx -t "respects explicit id prop"
   */
  it("respects explicit id prop", () => {
    render(<AppInput label="Email Address" id="custom-email-id" />);

    const input = screen.getByLabelText("Email Address");
    expect(input).toHaveAttribute("id", "custom-email-id");
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that when no explicit ID is provided, an auto-generated ID is created and
   *   the label's `htmlFor` matches the input's `id`.
   *
   * Why: Why this scenario matters
   * - Prevents ID collisions when multiple instances of the same component render on a page.
   *
   * Scenario: Input or condition being tested
   * - Render `<AppInput label="Postal Code" />` without an explicit ID.
   *
   * Pass condition: What result or assertion makes it pass
   * - Label `for` attribute equals input `id` attribute, and input `id` is a truthy non-empty string.
   *
   * Run from repository root:
   * npm --prefix web test -- src/shared/components/__tests__/AppInput.test.jsx -t "creates valid association even when id is generated"
   */
  it("creates valid association even when id is generated", () => {
    render(<AppInput label="Postal Code" />);

    const label = screen.getByText("Postal Code");
    const input = screen.getByLabelText("Postal Code");
    expect(label).toHaveAttribute("for", input.getAttribute("id"));
    expect(input.getAttribute("id")).toBeTruthy();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that providing an `error` prop renders the validation error text.
   *
   * Why: Why this scenario matters
   * - Users must be given clear feedback when an input fails validation.
   *
   * Scenario: Input or condition being tested
   * - Render `<AppInput label="Full Name" error="Name is required." />`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Text "Name is required." is rendered in the DOM.
   *
   * Run from repository root:
   * npm --prefix web test -- src/shared/components/__tests__/AppInput.test.jsx -t "renders validation error when provided"
   */
  it("renders validation error when provided", () => {
    render(<AppInput label="Full Name" error="Name is required." />);

    expect(screen.getByText("Name is required.")).toBeInTheDocument();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that when no `error` prop is passed, no error message or placeholder container is rendered.
   *
   * Why: Why this scenario matters
   * - Prevents unwanted whitespace or ghost elements when the field is in a valid state.
   *
   * Scenario: Input or condition being tested
   * - Render `<AppInput label="City" />` without an error prop.
   *
   * Pass condition: What result or assertion makes it pass
   * - Query for text matching `/required/i` returns null.
   *
   * Run from repository root:
   * npm --prefix web test -- src/shared/components/__tests__/AppInput.test.jsx -t "does not render error container when error is absent"
   */
  it("does not render error container when error is absent", () => {
    render(<AppInput label="City" />);

    expect(screen.queryByText(/required/i)).not.toBeInTheDocument();
  });
});
