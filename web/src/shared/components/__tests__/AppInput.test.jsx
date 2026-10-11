import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import AppInput from "../AppInput";

describe("AppInput", () => {
  it("renders label and associates it with the input", () => {
    render(<AppInput label="Phone Number" />);

    const input = screen.getByLabelText("Phone Number");
    expect(input).toBeInTheDocument();
  });

  it("respects explicit id prop", () => {
    render(<AppInput label="Email Address" id="custom-email-id" />);

    const input = screen.getByLabelText("Email Address");
    expect(input).toHaveAttribute("id", "custom-email-id");
  });

  it("creates valid association even when id is generated", () => {
    render(<AppInput label="Postal Code" />);

    const label = screen.getByText("Postal Code");
    const input = screen.getByLabelText("Postal Code");
    expect(label).toHaveAttribute("for", input.getAttribute("id"));
    expect(input.getAttribute("id")).toBeTruthy();
  });

  it("renders validation error when provided", () => {
    render(<AppInput label="Full Name" error="Name is required." />);

    expect(screen.getByText("Name is required.")).toBeInTheDocument();
  });

  it("does not render error container when error is absent", () => {
    render(<AppInput label="City" />);

    expect(screen.queryByText(/required/i)).not.toBeInTheDocument();
  });
});
