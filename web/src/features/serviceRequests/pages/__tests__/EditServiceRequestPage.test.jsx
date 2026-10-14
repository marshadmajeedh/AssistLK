// What this suite tests:
//   The EditServiceRequestPage React page component where customers modify an existing service request,
//   verifying data pre-population, lifecycle status guardrails (editable only in Created and AwaitingInformation),
//   read-only lockout for Analyzed/Cancelled states, and validation before update submission.
// Why these tests are needed:
//   Enforces business rules preventing customers from altering requests that have already been
//   analyzed or cancelled, while allowing refinements during early creation and clarification phases.
// What is real and what is mocked/faked:
//   - Real: EditServiceRequestPage component rendered with MemoryRouter and React Router route params.
//   - Mocked/Faked: serviceRequestService (getById, update) and useNavigate are mocked using Vitest vi.fn().
// Prerequisites:
//   Node.js dependencies installed in web folder (npm install).
// Run from repository root:
//   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/EditServiceRequestPage.test.jsx

import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { describe, it, expect, vi, beforeEach } from "vitest";
import EditServiceRequestPage from "../EditServiceRequestPage";
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
    getById: vi.fn(),
    update: vi.fn(),
  },
}));

function renderEditPage(requestId = "req-100") {
  return render(
    <MemoryRouter initialEntries={[`/service-requests/${requestId}/edit`]}>
      <Routes>
        <Route
          path="/service-requests/:id/edit"
          element={<EditServiceRequestPage />}
        />
      </Routes>
    </MemoryRouter>
  );
}

describe("EditServiceRequestPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // Purpose: Checks that existing description, location, and GPS coordinates are pre-filled in form inputs for a Created request.
  // Why: Ensures customers see their previously saved request details when editing.
  // Scenario: Loading an existing request in Created status with coordinates (6.9271, 79.8612).
  // Pass condition: Text inputs contain original values and persisted GPS badge is displayed.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/EditServiceRequestPage.test.jsx -t "renders form prepopulated with persisted description, location, and GPS for Created status"
  it("renders form prepopulated with persisted description, location, and GPS for Created status", async () => {
    serviceRequestService.getById.mockResolvedValueOnce({
      serviceRequestId: "req-100",
      status: "Created",
      description: "Original leaking pipe problem",
      locationText: "10 Main Street, Colombo",
      latitude: 6.9271,
      longitude: 79.8612,
    });

    renderEditPage();

    expect(
      await screen.findByRole("heading", {
        name: /refine the details before the workflow moves forward/i,
      })
    ).toBeInTheDocument();

    const descInput = screen.getByLabelText(/problem description/i);
    const locInput = screen.getByLabelText(/^location/i);

    expect(descInput).toHaveValue("Original leaking pipe problem");
    expect(locInput).toHaveValue("10 Main Street, Colombo");

    // Persisted GPS coordinates should be retained
    expect(screen.getByText(/6\.92710/)).toBeInTheDocument();
    expect(screen.getByText(/79\.86120/)).toBeInTheDocument();
  });

  // Purpose: Checks that a service request in AwaitingInformation status allows editing of its details.
  // Why: When the AI agent asks for clarification, the customer must be able to revise their problem description.
  // Scenario: Loading a request with status "AwaitingInformation".
  // Pass condition: Edit form renders with description input accessible and pre-filled.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/EditServiceRequestPage.test.jsx -t "allows editing when status is AwaitingInformation"
  it("allows editing when status is AwaitingInformation", async () => {
    serviceRequestService.getById.mockResolvedValueOnce({
      serviceRequestId: "req-100",
      status: "AwaitingInformation",
      description: "Power cut issue",
      locationText: "Kandy",
      latitude: null,
      longitude: null,
    });

    renderEditPage();

    expect(
      await screen.findByRole("heading", {
        name: /refine the details before the workflow moves forward/i,
      })
    ).toBeInTheDocument();
    expect(screen.getByLabelText(/problem description/i)).toHaveValue(
      "Power cut issue"
    );
  });

  // Purpose: Checks that editing is blocked and a lock message is shown when the request is already in Analyzed status.
  // Why: Once the AI problem analysis is complete, description edits are disabled to prevent stale analysis results.
  // Scenario: Loading a request with status "Analyzed".
  // Pass condition: "Request not editable" heading displays and "Save Changes" button is not rendered.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/EditServiceRequestPage.test.jsx -t "blocks editing when status is Analyzed"
  it("blocks editing when status is Analyzed", async () => {
    serviceRequestService.getById.mockResolvedValueOnce({
      serviceRequestId: "req-100",
      status: "Analyzed",
      category: "Plumbing",
      urgency: "High",
      description: "Sink broken",
      locationText: "Galle",
    });

    renderEditPage();

    expect(
      await screen.findByRole("heading", { name: /request not editable/i })
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        "This request can no longer be edited in its current status."
      )
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: /save changes/i })
    ).not.toBeInTheDocument();
  });

  // Purpose: Checks that editing is blocked when the service request has been Cancelled.
  // Why: Terminal states like Cancelled are immutable and cannot be re-opened via the edit form.
  // Scenario: Loading a request with status "Cancelled".
  // Pass condition: "Request not editable" heading is displayed and save button is absent.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/EditServiceRequestPage.test.jsx -t "blocks editing when status is Cancelled"
  it("blocks editing when status is Cancelled", async () => {
    serviceRequestService.getById.mockResolvedValueOnce({
      serviceRequestId: "req-100",
      status: "Cancelled",
      description: "Cancelled request",
      locationText: "Galle",
    });

    renderEditPage();

    expect(
      await screen.findByRole("heading", { name: /request not editable/i })
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: /save changes/i })
    ).not.toBeInTheDocument();
  });

  // Purpose: Checks that clearing input fields and submitting is rejected by client-side validation.
  // Why: Prevents submitting blank descriptions or blank location text during updates.
  // Scenario: User clears pre-populated inputs and clicks "Save Changes".
  // Pass condition: Validation error strings appear and serviceRequestService.update is not called.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/EditServiceRequestPage.test.jsx -t "rejects blank inputs during validation"
  it("rejects blank inputs during validation", async () => {
    const user = userEvent.setup();
    serviceRequestService.getById.mockResolvedValueOnce({
      serviceRequestId: "req-100",
      status: "Created",
      description: "Some initial text",
      locationText: "Some location",
      latitude: null,
      longitude: null,
    });

    renderEditPage();

    const descInput = await screen.findByLabelText(/problem description/i);
    const locInput = screen.getByLabelText(/^location/i);

    await user.clear(descInput);
    await user.clear(locInput);

    const submitBtn = screen.getByRole("button", { name: /save changes/i });
    await user.click(submitBtn);

    expect(
      screen.getByText("Please describe the problem you need assistance with.")
    ).toBeInTheDocument();
    expect(screen.getByText("Please enter your location.")).toBeInTheDocument();
    expect(serviceRequestService.update).not.toHaveBeenCalled();
  });

  // Purpose: Checks that submitting an update sends only allowed editable fields (description, locationText, latitude, longitude) and navigates to the details page.
  // Why: Prevents the client from modifying category, urgency, or customer ownership during edits.
  // Scenario: User enters new description and location details and clicks "Save Changes".
  // Pass condition: serviceRequestService.update is called with { description, locationText, latitude, longitude } and mockNavigate routes to "/service-requests/req-100".
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/EditServiceRequestPage.test.jsx -t "submits only allowed payload fields on valid update"
  it("submits only allowed payload fields on valid update", async () => {
    const user = userEvent.setup();
    serviceRequestService.getById.mockResolvedValueOnce({
      serviceRequestId: "req-100",
      status: "Created",
      description: "Initial description",
      locationText: "Initial location",
      latitude: null,
      longitude: null,
      category: "Plumbing",
      urgency: "Medium",
    });
    serviceRequestService.update.mockResolvedValueOnce({
      serviceRequestId: "req-100",
      status: "Created",
      description: "Updated description with more details",
      locationText: "Updated location, Negombo",
      latitude: null,
      longitude: null,
    });

    renderEditPage();

    const descInput = await screen.findByLabelText(/problem description/i);
    const locInput = screen.getByLabelText(/^location/i);

    await user.clear(descInput);
    await user.type(descInput, "Updated description with more details");
    await user.clear(locInput);
    await user.type(locInput, "Updated location, Negombo");

    const submitBtn = screen.getByRole("button", { name: /save changes/i });
    await user.click(submitBtn);

    await waitFor(() => {
      expect(serviceRequestService.update).toHaveBeenCalledTimes(1);
    });

    expect(serviceRequestService.update).toHaveBeenCalledWith("req-100", {
      description: "Updated description with more details",
      locationText: "Updated location, Negombo",
      latitude: null,
      longitude: null,
    });

    expect(mockNavigate).toHaveBeenCalledWith("/service-requests/req-100");
  });
});
