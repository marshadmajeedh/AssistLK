import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import StatusBadge from "../StatusBadge";

describe("StatusBadge", () => {
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

  it("renders ReadyForMatching correctly as 'Ready for Matching'", () => {
    render(<StatusBadge status="ReadyForMatching" />);
    expect(screen.getByText("Ready for Matching")).toBeInTheDocument();
  });

  it("uses safe fallback when status is unknown or null", () => {
    const { rerender } = render(<StatusBadge status="NonExistentStatus" />);
    expect(screen.getByText("NonExistentStatus")).toBeInTheDocument();

    rerender(<StatusBadge status={null} />);
    expect(screen.getByText("Unknown")).toBeInTheDocument();

    rerender(<StatusBadge status={undefined} />);
    expect(screen.getByText("Unknown")).toBeInTheDocument();
  });

  it("ensures status meaning is visible as text", () => {
    render(<StatusBadge status="ReadyForMatching" />);
    const badge = screen.getByText("Ready for Matching");
    expect(badge.textContent).toBe("Ready for Matching");
  });
});
