import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import AppTextArea from "../AppTextArea";

describe("AppTextArea", () => {
  it("renders label and associates it with the textarea", () => {
    render(<AppTextArea label="Description" />);

    const textarea = screen.getByLabelText("Description");
    expect(textarea).toBeInTheDocument();
  });

  it("respects explicit id and associates with label", () => {
    render(<AppTextArea label="Notes" id="special-notes-id" />);

    const textarea = screen.getByLabelText("Notes");
    expect(textarea).toHaveAttribute("id", "special-notes-id");
  });

  it("supports maxLength attribute", () => {
    render(<AppTextArea label="Feedback" maxLength={500} />);

    const textarea = screen.getByLabelText("Feedback");
    expect(textarea).toHaveAttribute("maxLength", "500");
  });

  it("displays validation error when provided", () => {
    render(<AppTextArea label="Feedback" error="Feedback cannot be empty." />);

    expect(screen.getByText("Feedback cannot be empty.")).toBeInTheDocument();
  });
});
