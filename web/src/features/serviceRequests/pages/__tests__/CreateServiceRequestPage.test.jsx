// What this suite tests:
//   The CreateServiceRequestPage React page component where customers create new service requests,
//   covering form field rendering, client-side input validation, manual location input,
//   opt-in browser geolocation capture, permission denial fallbacks, and payload purity.
// Why these tests are needed:
//   Guarantees that customers must provide non-empty descriptions and locations, GPS is never
//   requested unprompted without user consent, geolocation errors do not brick manual submission,
//   and the frontend never attempts to set backend-controlled properties (customerId, category, urgency, status).
// What is real and what is mocked/faked:
//   - Real: CreateServiceRequestPage component rendered with MemoryRouter and @testing-library/react.
//   - Mocked/Faked: navigator.geolocation is mocked with custom vi.fn() implementations to simulate GPS success/denial; serviceRequestService.create and useNavigate are mocked via vi.fn().
// Prerequisites:
//   Node.js dependencies installed in web folder (npm install).
// Run from repository root:
//   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/CreateServiceRequestPage.test.jsx

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

  // Purpose: Checks that form input controls (description textarea, location text input, Use Current Location button, Create Request submit button) are rendered.
  // Why: Confirms all necessary UI elements are available to the customer upon opening the page.
  // Scenario: Rendering CreateServiceRequestPage in its initial state.
  // Pass condition: All labeled form inputs and action buttons are present in the DOM.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/CreateServiceRequestPage.test.jsx -t "renders problem description, location, and Use Current Location button"
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

  // Purpose: Checks that submitting an empty form or whitespace-only inputs blocks submission and renders validation errors.
  // Why: Prevents blank or meaningless service requests from being submitted to the backend.
  // Scenario: User clicks submit with empty inputs, then types whitespace and clicks submit.
  // Pass condition: Validation error messages appear and serviceRequestService.create is never invoked.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/CreateServiceRequestPage.test.jsx -t "rejects empty submission and whitespace-only inputs"
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

  // Purpose: Checks that valid manual location submissions send only permitted fields and exclude customerId, category, urgency, and status.
  // Why: Security and architectural contract: creation payload must only contain customer-authored text; backend sets customer ID from JWT and manages initial lifecycle state.
  // Scenario: User enters description and manual location and submits.
  // Pass condition: serviceRequestService.create is called with { description, locationText, latitude: null, longitude: null } with no extraneous fields, then navigates to /service-requests.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/CreateServiceRequestPage.test.jsx -t "submits valid manual location and excludes customerId, category, urgency, status"
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

  // Purpose: Checks that browser geolocation is NOT queried automatically when the page loads.
  // Why: Privacy-first design: location permissions must never be requested without explicit customer action.
  // Scenario: Rendering CreateServiceRequestPage without interacting with the GPS button.
  // Pass condition: mockGetCurrentPosition has not been called.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/CreateServiceRequestPage.test.jsx -t "does not request GPS on initial render"
  it("does not request GPS on initial render", () => {
    render(
      <MemoryRouter>
        <CreateServiceRequestPage />
      </MemoryRouter>
    );

    expect(mockGetCurrentPosition).not.toHaveBeenCalled();
  });

  // Purpose: Checks that clicking "Use Current Location" prompts the browser for GPS coordinates and includes latitude and longitude in the creation payload.
  // Why: Enables precision location tagging for service provider dispatch.
  // Scenario: User clicks "Use Current Location", GPS coordinates (6.92708, 79.86124) resolve, and user submits the form.
  // Pass condition: Coordinates display on screen and serviceRequestService.create is called with latitude and longitude numbers.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/CreateServiceRequestPage.test.jsx -t "captures GPS coordinates when Use Current Location is clicked and includes them in payload"
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

  // Purpose: Checks that if the user denies geolocation permission, a friendly warning is displayed while manual text entry remains fully usable.
  // Why: Prevents permission denials from blocking users from submitting requests manually.
  // Scenario: Geolocation error with code 1 (PERMISSION_DENIED); user types manual location and submits.
  // Pass condition: Warning text "location permission was denied" appears, and create is called with null latitude/longitude.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/CreateServiceRequestPage.test.jsx -t "displays friendly message on GPS permission denial and leaves manual location usable"
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

  // Purpose: Checks that clicking "Clear GPS Location" removes previously captured coordinates and restores the "Use Current Location" button.
  // Why: Allows the user to retract auto-captured coordinates if they wish to submit manual text location only.
  // Scenario: Capturing GPS location and then clicking the "Clear GPS Location" button.
  // Pass condition: "Current location captured" banner is removed and "Use Current Location" button is re-rendered.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/CreateServiceRequestPage.test.jsx -t "resets coordinates when Clear GPS Location is clicked"
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
