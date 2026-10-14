// What this suite tests:
//   The ServiceRequestListPage React page component where customers view their service requests list,
//   covering initial loading spinners, empty-state placeholders with navigation prompts, card list rendering,
//   network/API failure banners, defensive handling of malformed API payloads, and status badge labels.
// Why these tests are needed:
//   Validates that customers receive a smooth, informative overview of their requests, can easily create
//   a new request when none exist, and that network or backend data inconsistencies do not crash the view.
// What is real and what is mocked/faked:
//   - Real: ServiceRequestListPage component rendered inside MemoryRouter using @testing-library/react.
//   - Mocked/Faked: serviceRequestService.getMyRequests and useNavigate are mocked using Vitest vi.fn().
// Prerequisites:
//   Node.js dependencies installed in web folder (npm install).
// Run from repository root:
//   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/ServiceRequestListPage.test.jsx

import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { describe, it, expect, vi, beforeEach } from "vitest";
import ServiceRequestListPage from "../ServiceRequestListPage";
import serviceRequestService from "../../services/serviceRequestService";

const mockNavigate = vi.fn();
vi.mock("react-router-dom", async () => {
  const actual = await vi.importActual("react-router-dom");
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  };
});

vi.mock("../../services/serviceRequestService", () => ({
  default: {
    getMyRequests: vi.fn(),
  },
}));

describe("ServiceRequestListPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // Purpose: Checks that a loading message/spinner is displayed while the API request is in-flight.
  // Why: Provides visual feedback to the customer that data is actively loading.
  // Scenario: getMyRequests returns an unresolved promise.
  // Pass condition: "Loading your service requests..." text is present in the DOM.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/ServiceRequestListPage.test.jsx -t "displays loading spinner while requests are being fetched"
  it("displays loading spinner while requests are being fetched", () => {
    serviceRequestService.getMyRequests.mockReturnValueOnce(new Promise(() => {}));

    render(
      <MemoryRouter>
        <ServiceRequestListPage />
      </MemoryRouter>
    );

    expect(
      screen.getByText("Loading your service requests...")
    ).toBeInTheDocument();
  });

  // Purpose: Checks that when the customer has no requests, an empty-state message and "Create Request" action button are displayed.
  // Why: Encourages new customers to create their first request and provides clear navigation.
  // Scenario: getMyRequests resolves with an empty array []; user clicks "Create Request".
  // Pass condition: "No service requests yet." displays and mockNavigate is called with "/service-requests/new".
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/ServiceRequestListPage.test.jsx -t "displays empty state with action button when API returns empty array"
  it("displays empty state with action button when API returns empty array", async () => {
    const user = userEvent.setup();
    serviceRequestService.getMyRequests.mockResolvedValueOnce([]);

    render(
      <MemoryRouter>
        <ServiceRequestListPage />
      </MemoryRouter>
    );

    expect(
      await screen.findByText("No service requests yet.")
    ).toBeInTheDocument();
    expect(
      screen.getByText("Create your first request to get started.")
    ).toBeInTheDocument();

    const createButtons = screen.getAllByRole("button", {
      name: /create request/i,
    });
    await user.click(createButtons[0]);
    expect(mockNavigate).toHaveBeenCalledWith("/service-requests/new");
  });

  // Purpose: Checks that an array of requests renders corresponding summary cards with descriptions and locations.
  // Why: Ensures all retrieved requests are visible to the customer with their core details.
  // Scenario: getMyRequests resolves with two request objects (Plumbing and Electrical).
  // Pass condition: Both descriptions ("Kitchen sink clogged", "Power outage in bedroom") and locations are rendered.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/ServiceRequestListPage.test.jsx -t "renders cards for valid requests"
  it("renders cards for valid requests", async () => {
    const requests = [
      {
        serviceRequestId: "req-1",
        description: "Kitchen sink clogged",
        locationText: "Colombo 03",
        status: "Created",
        category: "Plumbing",
        urgency: "Medium",
      },
      {
        serviceRequestId: "req-2",
        description: "Power outage in bedroom",
        locationText: "Nugegoda",
        status: "Analyzing",
        category: "Electrical",
        urgency: "High",
      },
    ];
    serviceRequestService.getMyRequests.mockResolvedValueOnce(requests);

    render(
      <MemoryRouter>
        <ServiceRequestListPage />
      </MemoryRouter>
    );

    expect(await screen.findByText("Kitchen sink clogged")).toBeInTheDocument();
    expect(screen.getByText("Power outage in bedroom")).toBeInTheDocument();
    expect(screen.getByText("Colombo 03")).toBeInTheDocument();
    expect(screen.getByText("Nugegoda")).toBeInTheDocument();
  });

  // Purpose: Checks that API failures display an ErrorMessage alert directing the customer to try again.
  // Why: Informs the customer when network issues prevent requests from loading.
  // Scenario: getMyRequests rejects with an Error.
  // Pass condition: "Failed to load service requests. Please try again." is displayed in the document.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/ServiceRequestListPage.test.jsx -t "renders ErrorMessage when API request fails"
  it("renders ErrorMessage when API request fails", async () => {
    serviceRequestService.getMyRequests.mockRejectedValueOnce(
      new Error("Network Error")
    );

    render(
      <MemoryRouter>
        <ServiceRequestListPage />
      </MemoryRouter>
    );

    expect(
      await screen.findByText(
        "Failed to load service requests. Please try again."
      )
    ).toBeInTheDocument();
  });

  // Purpose: Checks that an unexpected non-array API response does not crash the page and falls back to empty state.
  // Why: Defensive UI resilience against unexpected API payload shapes.
  // Scenario: getMyRequests resolves with an object { unexpected: "data structure" } instead of an array.
  // Pass condition: "No service requests yet." is displayed without throwing an exception.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/ServiceRequestListPage.test.jsx -t "safely handles unexpected non-array API results without crashing"
  it("safely handles unexpected non-array API results without crashing", async () => {
    serviceRequestService.getMyRequests.mockResolvedValueOnce({
      unexpected: "data structure",
    });

    render(
      <MemoryRouter>
        <ServiceRequestListPage />
      </MemoryRouter>
    );

    expect(
      await screen.findByText("No service requests yet.")
    ).toBeInTheDocument();
  });

  // Purpose: Checks that a request in ReadyForMatching status renders with the user-friendly "Ready for Matching" badge text.
  // Why: Verifies that backend enum strings map to polished, readable status labels.
  // Scenario: getMyRequests returns a request with status="ReadyForMatching".
  // Pass condition: "Ready for Matching" text badge is visible in the document.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/ServiceRequestListPage.test.jsx -t "displays ReadyForMatching badge correctly"
  it("displays ReadyForMatching badge correctly", async () => {
    const requests = [
      {
        serviceRequestId: "req-3",
        description: "Roof leak fixed inspection",
        locationText: "Kandy",
        status: "ReadyForMatching",
        category: "Roofing",
        urgency: "Low",
      },
    ];
    serviceRequestService.getMyRequests.mockResolvedValueOnce(requests);

    render(
      <MemoryRouter>
        <ServiceRequestListPage />
      </MemoryRouter>
    );

    expect(
      await screen.findByText("Ready for Matching")
    ).toBeInTheDocument();
  });
});
