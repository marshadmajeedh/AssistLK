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
      await screen.findByRole("heading", { name: /edit service request/i })
    ).toBeInTheDocument();

    const descInput = screen.getByLabelText(/problem description/i);
    const locInput = screen.getByLabelText(/^location/i);

    expect(descInput).toHaveValue("Original leaking pipe problem");
    expect(locInput).toHaveValue("10 Main Street, Colombo");

    // Persisted GPS coordinates should be retained
    expect(screen.getByText(/6\.92710/)).toBeInTheDocument();
    expect(screen.getByText(/79\.86120/)).toBeInTheDocument();
  });

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
      await screen.findByRole("heading", { name: /edit service request/i })
    ).toBeInTheDocument();
    expect(screen.getByLabelText(/problem description/i)).toHaveValue(
      "Power cut issue"
    );
  });

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
