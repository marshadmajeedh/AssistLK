import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import CreateServiceRequestPage from "../CreateServiceRequestPage";
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
    create: vi.fn(),
  },
}));

describe("CreateServiceRequestPage", () => {
  let originalGeolocation;
  let mockGetCurrentPosition;

  beforeEach(() => {
    vi.clearAllMocks();
    originalGeolocation = navigator.geolocation;
    mockGetCurrentPosition = vi.fn();
    Object.defineProperty(navigator, "geolocation", {
      value: {
        getCurrentPosition: mockGetCurrentPosition,
      },
      configurable: true,
      writable: true,
    });
  });

  afterEach(() => {
    if (originalGeolocation) {
      Object.defineProperty(navigator, "geolocation", {
        value: originalGeolocation,
        configurable: true,
        writable: true,
      });
    } else {
      delete navigator.geolocation;
    }
  });

  it("renders problem description, location, and Use Current Location button", () => {
    render(
      <MemoryRouter>
        <CreateServiceRequestPage />
      </MemoryRouter>
    );

    expect(
      screen.getByLabelText(/problem description/i)
    ).toBeInTheDocument();
    expect(screen.getByLabelText(/^location/i)).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: /use current location/i })
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: /create request/i })
    ).toBeInTheDocument();
  });

  it("rejects empty submission and whitespace-only inputs", async () => {
    const user = userEvent.setup();
    render(
      <MemoryRouter>
        <CreateServiceRequestPage />
      </MemoryRouter>
    );

    const submitBtn = screen.getByRole("button", { name: /create request/i });
    await user.click(submitBtn);

    expect(
      screen.getByText("Please describe the problem you need assistance with.")
    ).toBeInTheDocument();
    expect(screen.getByText("Please enter your location.")).toBeInTheDocument();
    expect(serviceRequestService.create).not.toHaveBeenCalled();

    // Fill with whitespace only
    const descInput = screen.getByLabelText(/problem description/i);
    const locInput = screen.getByLabelText(/^location/i);
    await user.type(descInput, "    ");
    await user.type(locInput, "    ");
    await user.click(submitBtn);

    expect(serviceRequestService.create).not.toHaveBeenCalled();
  });

  it("submits valid manual location and excludes customerId, category, urgency, status", async () => {
    const user = userEvent.setup();
    serviceRequestService.create.mockResolvedValueOnce({
      serviceRequestId: "req-123",
      status: "Created",
    });

    render(
      <MemoryRouter>
        <CreateServiceRequestPage />
      </MemoryRouter>
    );

    await user.type(
      screen.getByLabelText(/problem description/i),
      "AC unit is leaking water heavily in bedroom"
    );
    await user.type(
      screen.getByLabelText(/^location/i),
      "123 Galle Road, Colombo 03"
    );

    const submitBtn = screen.getByRole("button", { name: /create request/i });
    await user.click(submitBtn);

    await waitFor(() => {
      expect(serviceRequestService.create).toHaveBeenCalledTimes(1);
    });

    const callPayload = serviceRequestService.create.mock.calls[0][0];
    expect(callPayload).toEqual({
      description: "AC unit is leaking water heavily in bedroom",
      locationText: "123 Galle Road, Colombo 03",
      latitude: null,
      longitude: null,
    });

    expect(callPayload.customerId).toBeUndefined();
    expect(callPayload.category).toBeUndefined();
    expect(callPayload.urgency).toBeUndefined();
    expect(callPayload.status).toBeUndefined();

    expect(mockNavigate).toHaveBeenCalledWith("/service-requests");
  });

  it("does not request GPS on initial render", () => {
    render(
      <MemoryRouter>
        <CreateServiceRequestPage />
      </MemoryRouter>
    );

    expect(mockGetCurrentPosition).not.toHaveBeenCalled();
  });

  it("captures GPS coordinates when Use Current Location is clicked and includes them in payload", async () => {
    const user = userEvent.setup();
    mockGetCurrentPosition.mockImplementationOnce((success) => {
      success({
        coords: {
          latitude: 6.92708,
          longitude: 79.86124,
          accuracy: 15,
        },
      });
    });
    serviceRequestService.create.mockResolvedValueOnce({
      serviceRequestId: "req-456",
    });

    render(
      <MemoryRouter>
        <CreateServiceRequestPage />
      </MemoryRouter>
    );

    const gpsBtn = screen.getByRole("button", {
      name: /use current location/i,
    });
    await user.click(gpsBtn);

    expect(mockGetCurrentPosition).toHaveBeenCalledTimes(1);

    expect(
      await screen.findByText("Current location captured")
    ).toBeInTheDocument();
    expect(screen.getByText(/6\.92708/)).toBeInTheDocument();
    expect(screen.getByText(/79\.86124/)).toBeInTheDocument();

    await user.type(
      screen.getByLabelText(/problem description/i),
      "Electrical tripping in living room"
    );
    await user.type(screen.getByLabelText(/^location/i), "Colombo 07");

    const submitBtn = screen.getByRole("button", { name: /create request/i });
    await user.click(submitBtn);

    await waitFor(() => {
      expect(serviceRequestService.create).toHaveBeenCalledTimes(1);
    });

    const callPayload = serviceRequestService.create.mock.calls[0][0];
    expect(callPayload).toEqual({
      description: "Electrical tripping in living room",
      locationText: "Colombo 07",
      latitude: 6.92708,
      longitude: 79.86124,
    });
  });

  it("displays friendly message on GPS permission denial and leaves manual location usable", async () => {
    const user = userEvent.setup();
    mockGetCurrentPosition.mockImplementationOnce((_success, error) => {
      error({ code: 1, message: "User denied Geolocation" });
    });
    serviceRequestService.create.mockResolvedValueOnce({
      serviceRequestId: "req-789",
    });

    render(
      <MemoryRouter>
        <CreateServiceRequestPage />
      </MemoryRouter>
    );

    const gpsBtn = screen.getByRole("button", {
      name: /use current location/i,
    });
    await user.click(gpsBtn);

    expect(
      await screen.findByText(/location permission was denied/i)
    ).toBeInTheDocument();

    // Manual input remains usable
    const descInput = screen.getByLabelText(/problem description/i);
    const locInput = screen.getByLabelText(/^location/i);
    await user.type(descInput, "Water pump failed to start");
    await user.type(locInput, "Kandy Road, Kelaniya");

    const submitBtn = screen.getByRole("button", { name: /create request/i });
    await user.click(submitBtn);

    await waitFor(() => {
      expect(serviceRequestService.create).toHaveBeenCalledTimes(1);
    });

    expect(serviceRequestService.create).toHaveBeenCalledWith({
      description: "Water pump failed to start",
      locationText: "Kandy Road, Kelaniya",
      latitude: null,
      longitude: null,
    });
  });

  it("resets coordinates when Clear GPS Location is clicked", async () => {
    const user = userEvent.setup();
    mockGetCurrentPosition.mockImplementationOnce((success) => {
      success({
        coords: {
          latitude: 6.9271,
          longitude: 79.8612,
          accuracy: 10,
        },
      });
    });

    render(
      <MemoryRouter>
        <CreateServiceRequestPage />
      </MemoryRouter>
    );

    await user.click(
      screen.getByRole("button", { name: /use current location/i })
    );
    expect(
      await screen.findByText("Current location captured")
    ).toBeInTheDocument();

    const clearBtn = screen.getByRole("button", {
      name: /clear gps location/i,
    });
    await user.click(clearBtn);

    expect(
      screen.queryByText("Current location captured")
    ).not.toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: /use current location/i })
    ).toBeInTheDocument();
  });
});
