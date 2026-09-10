import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { describe, it, expect, vi } from "vitest";
import ReadyForMatchingSection from "../ReadyForMatchingSection";

const mockNavigate = vi.fn();
vi.mock("react-router-dom", async () => {
  const actual = await vi.importActual("react-router-dom");
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  };
});

describe("ReadyForMatchingSection", () => {
  const sampleRequest = {
    category: "Plumbing",
    urgency: "High",
    locationText: "Colombo 03",
    status: "ReadyForMatching",
  };

  it("renders confirmed category, urgency, location, and status", () => {
    render(
      <MemoryRouter>
        <ReadyForMatchingSection request={sampleRequest} />
      </MemoryRouter>
    );

    expect(
      screen.getByText("Request ready for provider matching")
    ).toBeInTheDocument();
    expect(screen.getByText("Plumbing")).toBeInTheDocument();
    expect(screen.getByText("High")).toBeInTheDocument();
    expect(screen.getByText("Colombo 03")).toBeInTheDocument();
  });

  it("navigates to service requests list when Back to My Requests is clicked", async () => {
    const user = userEvent.setup();
    render(
      <MemoryRouter>
        <ReadyForMatchingSection request={sampleRequest} />
      </MemoryRouter>
    );

    const backBtn = screen.getByRole("button", {
      name: /back to my requests/i,
    });
    await user.click(backBtn);

    expect(mockNavigate).toHaveBeenCalledWith("/service-requests");
  });

  it("handles missing request properties with safe fallbacks", () => {
    render(
      <MemoryRouter>
        <ReadyForMatchingSection request={null} />
      </MemoryRouter>
    );

    expect(screen.getByText("Unclassified")).toBeInTheDocument();
    expect(screen.getByText("Unknown")).toBeInTheDocument();
    expect(screen.getByText("Not provided")).toBeInTheDocument();
  });
});
