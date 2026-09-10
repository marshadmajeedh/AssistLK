import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { describe, it, expect, vi } from "vitest";
import ServiceRequestCard from "../ServiceRequestCard";

const mockNavigate = vi.fn();
vi.mock("react-router-dom", async () => {
  const actual = await vi.importActual("react-router-dom");
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  };
});

describe("ServiceRequestCard", () => {
  const sampleRequest = {
    serviceRequestId: "req-101",
    description: "Leaking pipe in kitchen sink",
    locationText: "45 Galle Road, Colombo 03",
    category: "Plumbing",
    urgency: "High",
    status: "Created",
    createdAt: "2026-03-01T10:00:00Z",
  };

  it("renders status text, description, location, category, and urgency", () => {
    render(
      <MemoryRouter>
        <ServiceRequestCard request={sampleRequest} />
      </MemoryRouter>
    );

    expect(screen.getByText("Created")).toBeInTheDocument();
    expect(screen.getByText("Leaking pipe in kitchen sink")).toBeInTheDocument();
    expect(screen.getByText("45 Galle Road, Colombo 03")).toBeInTheDocument();
    expect(screen.getByText("Plumbing")).toBeInTheDocument();
    expect(screen.getByText("High")).toBeInTheDocument();
  });

  it("navigates to details page when View Details is clicked", async () => {
    const user = userEvent.setup();
    render(
      <MemoryRouter>
        <ServiceRequestCard request={sampleRequest} />
      </MemoryRouter>
    );

    const button = screen.getByRole("button", { name: /view details/i });
    await user.click(button);

    expect(mockNavigate).toHaveBeenCalledWith("/service-requests/req-101");
  });

  it("falls back to Unclassified and Unknown when category and urgency are missing", () => {
    const requestWithMissingFields = {
      serviceRequestId: "req-102",
      description: "Need electrical wiring checked",
      locationText: "Kandy Road",
      status: "Created",
    };

    render(
      <MemoryRouter>
        <ServiceRequestCard request={requestWithMissingFields} />
      </MemoryRouter>
    );

    expect(screen.getByText("Unclassified")).toBeInTheDocument();
    expect(screen.getByText("Unknown")).toBeInTheDocument();
  });

  it("returns null when request is undefined or null", () => {
    const { container } = render(
      <MemoryRouter>
        <ServiceRequestCard request={null} />
      </MemoryRouter>
    );

    expect(container).toBeEmptyDOMElement();
  });
});
