// What this suite tests:
//   The ServiceRequestCard React component which displays a summary card for a service request
//   in the customer's request list, including status badge, description, location, category, urgency, and details link.
// Why these tests are needed:
//   Ensures customer list views present accurate status and summary information, clicking "View Details"
//   navigates to the correct detail route, missing category/urgency fields use safe fallbacks, and null props return empty output.
// What is real and what is mocked/faked:
//   - Real: ServiceRequestCard component rendered inside MemoryRouter using @testing-library/react.
//   - Mocked/Faked: react-router-dom useNavigate hook is mocked via Vitest vi.fn() to assert route paths.
// Prerequisites:
//   Node.js dependencies installed in web folder (npm install).
// Run from repository root:
//   npm --prefix web test -- src/features/serviceRequests/components/__tests__/ServiceRequestCard.test.jsx

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

  // Purpose: Checks that status text, description, location, category, and urgency badges are rendered on the card.
  // Why: Confirms customers can review core request attributes directly from list cards.
  // Scenario: Providing a complete request object.
  // Pass condition: All summary strings and status labels appear in the document.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/components/__tests__/ServiceRequestCard.test.jsx -t "renders status text, description, location, category, and urgency"
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

  // Purpose: Checks that clicking the "View Details" button navigates to /service-requests/{serviceRequestId}.
  // Why: Verifies that list items correctly navigate to their specific detail pages.
  // Scenario: User clicks "View Details" on card for req-101.
  // Pass condition: mockNavigate is called with "/service-requests/req-101".
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/components/__tests__/ServiceRequestCard.test.jsx -t "navigates to details page when View Details is clicked"
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

  // Purpose: Checks that missing category and urgency fields fallback to "Unclassified" and "Unknown".
  // Why: Ensures newly created or unanalyzed requests display consistent fallback badges without crashing.
  // Scenario: Passing a request object with omitted category and urgency.
  // Pass condition: "Unclassified" and "Unknown" text elements are rendered.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/components/__tests__/ServiceRequestCard.test.jsx -t "falls back to Unclassified and Unknown when category and urgency are missing"
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

  // Purpose: Checks that passing a null or undefined request prop safely returns an empty DOM element.
  // Why: Prevents runtime exceptions when rendering lists with nullish entries.
  // Scenario: Rendering with request={null}.
  // Pass condition: The rendered container is completely empty.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/components/__tests__/ServiceRequestCard.test.jsx -t "returns null when request is undefined or null"
  it("returns null when request is undefined or null", () => {
    const { container } = render(
      <MemoryRouter>
        <ServiceRequestCard request={null} />
      </MemoryRouter>
    );

    expect(container).toBeEmptyDOMElement();
  });
});
