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
