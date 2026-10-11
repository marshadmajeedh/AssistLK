import { act, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import AdminServiceRequestListPage, { CreatedDateTime } from "../AdminServiceRequestListPage";
import service from "../../services/adminServiceRequestService";

vi.mock("../../services/adminServiceRequestService", () => ({ default: { getAll: vi.fn(), getById: vi.fn() } }));
const request = {
  serviceRequestId: "a1b2c3d4-1234-4567-8901-123456789abc",
  category: "Plumbing", categoryHint: "Electrical", urgency: "High", status: "AwaitingInformation",
  description: "The kitchen pipe leaks continuously. The entire cupboard is wet.",
  locationText: "Colombo", latitude: 0, longitude: 79.86,
  createdAt: "2026-09-10T10:00:00Z", updatedAt: "2026-09-11T10:00:00Z",
  latestAnalysis: { detectedProblem: "Leaking supply pipe", confidence: 0.87, agentName: "Gemini", createdAt: "2026-09-11T09:00:00Z" },
  clarifications: [
    { id: "c1", clarificationRound: 1, sequence: 1, question: "Where is the leak?", answer: "Under the sink", answeredAt: "2026-09-11T08:00:00Z" },
    { id: "c2", clarificationRound: 2, sequence: 1, question: "Is the valve closed?", answer: null, supersededAt: "2026-09-11T09:00:00Z" },
  ],
};
beforeEach(() => {
  vi.resetAllMocks();
  service.getAll.mockResolvedValue([request]);
  service.getById.mockResolvedValue(request);
});
async function openDetail() {
  const user = userEvent.setup();
  render(<AdminServiceRequestListPage />);
  await user.click(await screen.findByRole("button", { name: "View Details" }));
  const panel = screen.getByRole("region", { name: "Request details" });
  await within(panel).findByText(request.serviceRequestId);
  return { user, panel, detail: within(panel) };
}

describe("Admin monitoring list", () => {
  it("renders fetched requests, final category, lifecycle badge, confidence and date", async () => {
    render(<AdminServiceRequestListPage />);
    expect(await screen.findByText("SR-A1B2C3D4")).toBeInTheDocument();
    const table = within(screen.getByRole("table"));
    const created = new Date(request.createdAt);
    const dateText = created.toLocaleDateString();
    const timeText = created.toLocaleTimeString([], {
      hour: "numeric",
      minute: "2-digit",
      second: "2-digit",
    });
    for (const text of ["Plumbing", "High", "Awaiting Information", "Colombo", "87%", dateText, timeText]) {
      expect(table.getByText(text)).toBeInTheDocument();
    }
    expect(table.queryByText("Electrical")).not.toBeInTheDocument();
  });
  it("shows loading without fake rows", () => {
    service.getAll.mockReturnValue(new Promise(() => {}));
    render(<AdminServiceRequestListPage />);
    expect(screen.getByRole("status")).toHaveTextContent("Loading service requests");
    expect(screen.queryByRole("table")).not.toBeInTheDocument();
  });
  it("shows empty state", async () => {
    service.getAll.mockResolvedValue([]);
    render(<AdminServiceRequestListPage />);
    expect(await screen.findByText("No service requests found.")).toBeInTheDocument();
  });
  it("shows API error and retries successfully", async () => {
    service.getAll.mockRejectedValueOnce(new Error("offline"));
    const user = userEvent.setup();
    render(<AdminServiceRequestListPage />);
    expect(await screen.findByRole("alert")).toHaveTextContent("Failed to load service requests");
    await user.click(screen.getByRole("button", { name: "Retry" }));
    expect(await screen.findByText("SR-A1B2C3D4")).toBeInTheDocument();
    expect(service.getAll).toHaveBeenCalledTimes(2);
  });
  it.each([["Status", "Analyzed", "status"], ["Category", "Vehicle Repair", "category"], ["Urgency", "Critical", "urgency"]])("refreshes using the %s filter", async (label, value, key) => {
    const user = userEvent.setup();
    render(<AdminServiceRequestListPage />);
    await screen.findByRole("table");
    service.getAll.mockResolvedValue([]);
    await user.selectOptions(screen.getByLabelText(label), value);
    await waitFor(() => expect(service.getAll).toHaveBeenLastCalledWith({ status: "", category: "", urgency: "", [key]: value }));
    expect(await screen.findByText("No requests match the selected filters.")).toBeInTheDocument();
  });
  it("combines and clears filters", async () => {
    const user = userEvent.setup();
    render(<AdminServiceRequestListPage />);
    await user.selectOptions(screen.getByLabelText("Status"), "Created");
    await user.selectOptions(screen.getByLabelText("Category"), "Plumbing");
    await user.selectOptions(screen.getByLabelText("Urgency"), "Low");
    await waitFor(() => expect(service.getAll).toHaveBeenLastCalledWith({ status: "Created", category: "Plumbing", urgency: "Low" }));
    await user.click(screen.getByRole("button", { name: "Clear Filters" }));
    await waitFor(() => expect(service.getAll).toHaveBeenLastCalledWith({ status: "", category: "", urgency: "" }));
    for (const label of ["Status", "Category", "Urgency"]) expect(screen.getByLabelText(label)).toHaveValue("");
  });
  it("ignores an old list response after filters change", async () => {
    let resolveOld;
    service.getAll.mockReturnValueOnce(new Promise((resolve) => { resolveOld = resolve; })).mockResolvedValue([]);
    const user = userEvent.setup();
    render(<AdminServiceRequestListPage />);
    await user.selectOptions(screen.getByLabelText("Status"), "Cancelled");
    await screen.findByText("No service requests found.");
    await act(async () => resolveOld([request]));
    expect(screen.queryByText("SR-A1B2C3D4")).not.toBeInTheDocument();
  });
  it("renders long address in bounded location cell with tooltip and preserves table geometry across rows", async () => {
    const longAddress = "Ramya Mawatha, Battaramulla North, Battaramulla, Colombo District, Western Province, 10120, Sri Lanka";
    const req1 = { ...request, serviceRequestId: "aaaaaaaa-1234-4567-8901-123456789abc", locationText: longAddress, locationSource: "OpenStreetMap" };
    const req2 = { ...request, serviceRequestId: "bbbbbbbb-1234-4567-8901-123456789abc", category: "Electrical", status: "Created", urgency: "Low", locationText: "Kandy Road, Kiribathgoda", locationSource: "Manual" };
    service.getAll.mockResolvedValue([req1, req2]);
    render(<AdminServiceRequestListPage />);

    expect(await screen.findByText("SR-AAAAAAAA")).toBeInTheDocument();
    expect(screen.getByText("SR-BBBBBBBB")).toBeInTheDocument();

    const table = screen.getByRole("table");
    const headers = within(table).getAllByRole("columnheader").map((th) => th.textContent);
    expect(headers).toEqual(["Request", "Category", "Urgency", "Status", "Location", "Confidence", "Created", "Action"]);

    const rows = within(table).getAllByRole("row").slice(1); // skip header row
    expect(rows).toHaveLength(2);

    // Verify row 1 (long OSM address)
    const row1Cells = within(rows[0]).getAllByRole("cell");
    expect(row1Cells).toHaveLength(8);
    expect(row1Cells[4]).toHaveClass("cell-location");
    const loc1 = within(row1Cells[4]).getByText(longAddress);
    expect(loc1).toHaveClass("monitoring-location-address");
    expect(loc1).toHaveAttribute("title", longAddress);
    const osmLink = within(row1Cells[4]).getByRole("link", { name: "© OpenStreetMap contributors" });
    expect(osmLink).toHaveAttribute("href", "https://www.openstreetmap.org/copyright");

    // Verify row 2 (manual address, no OSM attribution)
    const row2Cells = within(rows[1]).getAllByRole("cell");
    expect(row2Cells).toHaveLength(8);
    expect(row2Cells[4]).toHaveClass("cell-location");
    const loc2 = within(row2Cells[4]).getByText("Kandy Road, Kiribathgoda");
    expect(loc2).toHaveAttribute("title", "Kandy Road, Kiribathgoda");
    expect(within(row2Cells[4]).queryByRole("link")).not.toBeInTheDocument();

    // Verify badges and action buttons across both rows
    expect(within(row1Cells[2]).getByText("High")).toBeInTheDocument();
    expect(within(row1Cells[3]).getByText("Awaiting Information")).toBeInTheDocument();
    expect(within(row2Cells[2]).getByText("Low")).toBeInTheDocument();
    expect(within(row2Cells[3]).getByText("Created")).toBeInTheDocument();
    expect(within(row1Cells[7]).getByRole("button", { name: "View Details" })).toBeInTheDocument();
    expect(within(row2Cells[7]).getByRole("button", { name: "View Details" })).toBeInTheDocument();
  });
});

describe("Read-only authoritative request details", () => {
  it("fetches detail and displays complete description, ID, overview and coordinates including zero", async () => {
    service.getById.mockResolvedValue({ ...request, description: `${request.description} Fresh detail.` });
    const { detail } = await openDetail();
    expect(service.getById).toHaveBeenCalledWith(request.serviceRequestId);
    expect(detail.getByText(`${request.description} Fresh detail.`)).toBeInTheDocument();
    for (const text of ["Customer category preference", "Electrical", "Updated At", "Latitude", "Longitude", "0", "79.86"]) expect(detail.getByText(text)).toBeInTheDocument();
  });
  it("renders latest analysis with safe AssistLK AI branding", async () => {
    const { detail } = await openDetail();
    for (const text of ["Latest AssistLK AI analysis", "Leaking supply pipe", "87%", "AssistLK AI", "Analysis timestamp"]) expect(detail.getByText(text)).toBeInTheDocument();
    expect(detail.queryByText(/gemini|openai/i)).not.toBeInTheDocument();
  });
  it("preserves clarification order and distinguishes unanswered and superseded questions", async () => {
    const { detail } = await openDetail();
    const items = detail.getAllByRole("listitem");
    expect(items[0]).toHaveTextContent("Round 1 · Sequence 1");
    expect(items[0]).toHaveTextContent("Where is the leak?");
    expect(items[0]).toHaveTextContent("Under the sink");
    expect(items[0]).toHaveTextContent("Answered At");
    expect(items[1]).toHaveTextContent("Is the valve closed?");
    expect(items[1]).toHaveTextContent("Unanswered");
    expect(items[1]).toHaveTextContent("Superseded At");
  });
  it("handles missing analysis, coordinates and clarification history", async () => {
    const sparse = { ...request, latestAnalysis: null, latitude: null, longitude: undefined, clarifications: [] };
    service.getAll.mockResolvedValue([sparse]);
    service.getById.mockResolvedValue(sparse);
    const { detail } = await openDetail();
    expect(detail.getByText("No analysis available yet.")).toBeInTheDocument();
    expect(detail.getByText("No clarification history.")).toBeInTheDocument();
    expect(detail.queryByText("Latitude")).not.toBeInTheDocument();
    expect(detail.queryByText("Longitude")).not.toBeInTheDocument();
    expect(within(screen.getByRole("table")).getByText("—")).toBeInTheDocument();
  });
  it("shows detail loading, error, and successful retry", async () => {
    let rejectDetail;
    service.getById.mockReturnValueOnce(new Promise((resolve, reject) => { rejectDetail = reject; }));
    const user = userEvent.setup();
    render(<AdminServiceRequestListPage />);
    await user.click(await screen.findByRole("button", { name: "View Details" }));
    expect(screen.getByRole("status")).toHaveTextContent("Loading request details");
    await act(async () => rejectDetail(new Error("offline")));
    expect(screen.getByRole("alert")).toHaveTextContent("Failed to load request details");
    await user.click(screen.getByRole("button", { name: "Retry Details" }));
    expect(await screen.findByText(request.description)).toBeInTheDocument();
  });
  it("has only read-only controls and restores focus when closed", async () => {
    const { user, detail } = await openDetail();
    expect(detail.getByText(/Read-only Admin monitoring view/)).toBeInTheDocument();
    expect(screen.getAllByRole("button").map((button) => button.textContent)).toEqual(["View Details", "Close Details"]);
    expect(detail.queryByRole("textbox")).not.toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Request details" })).toHaveFocus();
    await user.click(screen.getByRole("button", { name: "Close Details" }));
    expect(screen.queryByRole("region", { name: "Request details" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "View Details" })).toHaveFocus();
  });
  it("ignores stale detail when a different request is selected", async () => {
    let resolveOld;
    const next = { ...request, serviceRequestId: "bbbbbbbb-1234-4567-8901-123456789abc", description: "Second request detail" };
    service.getAll.mockResolvedValue([request, next]);
    service.getById.mockReturnValueOnce(new Promise((resolve) => { resolveOld = resolve; })).mockResolvedValue(next);
    const user = userEvent.setup();
    render(<AdminServiceRequestListPage />);
    const buttons = await screen.findAllByRole("button", { name: "View Details" });
    await user.click(buttons[0]);
    await user.click(buttons[1]);
    await screen.findByText("Second request detail");
    await act(async () => resolveOld(request));
    const detail = within(screen.getByRole("region", { name: "Request details" }));
    expect(detail.getByText(next.serviceRequestId)).toBeInTheDocument();
    expect(detail.queryByText(request.description)).not.toBeInTheDocument();
  });
});

describe("Responsive layout and compact card presentation", () => {
  const longAddress = "Ramya Mawatha, Battaramulla North, Battaramulla, Colombo District, Western Province, 10120, Sri Lanka";
  const rfmRequest = {
    ...request,
    serviceRequestId: "b3b0795e-b89a-422d-ac3f-f186afa06ad4",
    status: "ReadyForMatching",
    locationText: longAddress,
    locationSource: "OpenStreetMap",
    latestAnalysis: { detectedProblem: "Pipe failure", confidence: 0.95 },
  };

  it("1. wide container renders structured table layout", async () => {
    service.getAll.mockResolvedValue([request]);
    render(<AdminServiceRequestListPage layoutMode="table" />);
    expect(await screen.findByRole("table")).toBeInTheDocument();
    expect(screen.queryByTestId("admin-request-card")).not.toBeInTheDocument();
    const headers = screen.getAllByRole("columnheader").map((th) => th.textContent);
    expect(headers).toEqual(["Request", "Category", "Urgency", "Status", "Location", "Confidence", "Created", "Action"]);
  });

  it("2. narrow/medium container renders compact card layout", async () => {
    service.getAll.mockResolvedValue([request]);
    render(<AdminServiceRequestListPage layoutMode="card" />);
    expect(await screen.findByTestId("admin-request-card")).toBeInTheDocument();
    expect(screen.queryByRole("table")).not.toBeInTheDocument();
    expect(screen.getByRole("region", { name: "Service requests cards" })).toBeInTheDocument();
  });

  it("3. ReadyForMatching badge renders safely without colliding with Location in table and card modes", async () => {
    service.getAll.mockResolvedValue([rfmRequest]);

    // Table Mode
    const { unmount } = render(<AdminServiceRequestListPage layoutMode="table" />);
    const table = await screen.findByRole("table");
    const rfmCell = within(table).getByRole("cell", { name: /Ready for Matching/i });
    expect(rfmCell).toHaveClass("cell-status");
    const locCell = within(table).getByText(longAddress).closest("td");
    expect(locCell).toHaveClass("cell-location");
    expect(rfmCell).not.toBe(locCell);
    unmount();

    // Card Mode
    render(<AdminServiceRequestListPage layoutMode="card" />);
    const card = await screen.findByTestId("admin-request-card");
    const badge = within(card).getByText("Ready for Matching");
    expect(badge.closest(".card-status-badge")).toBeInTheDocument();
    expect(within(card).getByText(longAddress).closest(".card-location-row")).toBeInTheDocument();
  });

  it("4. AwaitingInformation badge renders safely in distinct status containers in both layouts", async () => {
    service.getAll.mockResolvedValue([request]);

    // Table Mode
    const { unmount } = render(<AdminServiceRequestListPage layoutMode="table" />);
    const table = await screen.findByRole("table");
    expect(within(table).getByText("Awaiting Information").closest("td")).toHaveClass("cell-status");
    unmount();

    // Card Mode
    render(<AdminServiceRequestListPage layoutMode="card" />);
    const card = await screen.findByTestId("admin-request-card");
    expect(within(card).getByText("Awaiting Information").closest(".card-status-badge")).toBeInTheDocument();
  });

  it("5. long Location is bounded with accessible tooltip and attribution in table mode", async () => {
    service.getAll.mockResolvedValue([rfmRequest]);
    render(<AdminServiceRequestListPage layoutMode="table" />);
    const table = await screen.findByRole("table");
    const locAddress = within(table).getByText(longAddress);
    expect(locAddress).toHaveClass("monitoring-location-address");
    expect(locAddress).toHaveAttribute("title", longAddress);
    const osm = within(table).getByRole("link", { name: "© OpenStreetMap contributors" });
    expect(osm).toHaveAttribute("href", "https://www.openstreetmap.org/copyright");
  });

  it("6. long Location wraps correctly in card mode with title and attribution", async () => {
    service.getAll.mockResolvedValue([rfmRequest]);
    render(<AdminServiceRequestListPage layoutMode="card" />);
    const card = await screen.findByTestId("admin-request-card");
    const locRow = within(card).getByText("Location").closest(".card-location-row");
    const locAddress = within(locRow).getByText(longAddress);
    expect(locAddress).toHaveAttribute("title", longAddress);
    const osm = within(locRow).getByRole("link", { name: "© OpenStreetMap contributors" });
    expect(osm).toHaveAttribute("href", "https://www.openstreetmap.org/copyright");
  });

  it("7. Confidence label remains readable in both layouts", async () => {
    service.getAll.mockResolvedValue([rfmRequest]);

    // Table Mode
    const { unmount } = render(<AdminServiceRequestListPage layoutMode="table" />);
    expect(await screen.findByRole("columnheader", { name: "Confidence" })).toBeInTheDocument();
    expect(screen.getByText("95%")).toBeInTheDocument();
    unmount();

    // Card Mode
    render(<AdminServiceRequestListPage layoutMode="card" />);
    const card = await screen.findByTestId("admin-request-card");
    expect(within(card).getByText("Confidence")).toBeInTheDocument();
    expect(within(card).getByText("95%")).toBeInTheDocument();
  });

  it("8. Created timestamp remains readable in both layouts", async () => {
    service.getAll.mockResolvedValue([request]);
    const created = new Date(request.createdAt);
    const dateText = created.toLocaleDateString();
    const timeText = created.toLocaleTimeString([], {
      hour: "numeric",
      minute: "2-digit",
      second: "2-digit",
    });

    // Table Mode
    const { unmount } = render(<AdminServiceRequestListPage layoutMode="table" />);
    const table = await screen.findByRole("table");
    expect(within(table).getByText(dateText)).toBeInTheDocument();
    expect(within(table).getByText(timeText)).toBeInTheDocument();
    unmount();

    // Card Mode
    render(<AdminServiceRequestListPage layoutMode="card" />);
    const card = await screen.findByTestId("admin-request-card");
    expect(within(card).getByText("Created")).toBeInTheDocument();
    expect(within(card).getByText(dateText)).toBeInTheDocument();
    expect(within(card).getByText(timeText)).toBeInTheDocument();
  });

  it("9. View Details remains accessible and operable in card mode", async () => {
    service.getAll.mockResolvedValue([request]);
    service.getById.mockResolvedValue(request);
    const user = userEvent.setup();
    render(<AdminServiceRequestListPage layoutMode="card" />);
    const card = await screen.findByTestId("admin-request-card");
    const viewButton = within(card).getByRole("button", { name: "View Details" });
    await user.click(viewButton);

    const detailPanel = screen.getByRole("region", { name: "Request details" });
    expect(detailPanel).toBeInTheDocument();
    expect(within(detailPanel).getByText(request.serviceRequestId)).toBeInTheDocument();

    const closeButton = within(detailPanel).getByRole("button", { name: "Close Details" });
    await user.click(closeButton);
    expect(screen.queryByRole("region", { name: "Request details" })).not.toBeInTheDocument();
    expect(viewButton).toHaveFocus();
  });

  it("10. request count remains correct in both layouts", async () => {
    service.getAll.mockResolvedValue([request, rfmRequest]);

    // Table Mode
    const { unmount } = render(<AdminServiceRequestListPage layoutMode="table" />);
    expect(within(await screen.findByRole("table")).getByText("2 service requests")).toBeInTheDocument();
    unmount();

    // Card Mode
    render(<AdminServiceRequestListPage layoutMode="card" />);
    expect(await screen.findByText("2 service requests")).toBeInTheDocument();
  });

  it("11. OSM attribution appears conditionally in both layouts", async () => {
    const manualReq = { ...request, locationSource: "Manual" };
    service.getAll.mockResolvedValue([rfmRequest, manualReq]);

    // Table Mode
    const { unmount } = render(<AdminServiceRequestListPage layoutMode="table" />);
    const table = await screen.findByRole("table");
    expect(within(table).getAllByRole("link", { name: "© OpenStreetMap contributors" })).toHaveLength(1);
    unmount();

    // Card Mode
    render(<AdminServiceRequestListPage layoutMode="card" />);
    const cards = await screen.findAllByTestId("admin-request-card");
    expect(within(cards[0]).getByRole("link", { name: "© OpenStreetMap contributors" })).toBeInTheDocument();
    expect(within(cards[1]).queryByRole("link", { name: "© OpenStreetMap contributors" })).not.toBeInTheDocument();
  });

  it("12. All-filter functionality remains unchanged in card mode", async () => {
    service.getAll.mockResolvedValue([request]);
    render(<AdminServiceRequestListPage layoutMode="card" />);
    await screen.findByTestId("admin-request-card");
    expect(service.getAll).toHaveBeenLastCalledWith({ status: "", category: "", urgency: "" });
  });

  it("13. Created filter remains functional in card mode", async () => {
    service.getAll.mockResolvedValue([request]);
    const user = userEvent.setup();
    render(<AdminServiceRequestListPage layoutMode="card" />);
    await screen.findByTestId("admin-request-card");
    await user.selectOptions(screen.getByLabelText("Status"), "Created");
    await waitFor(() => expect(service.getAll).toHaveBeenLastCalledWith({ status: "Created", category: "", urgency: "" }));
  });

  it("14. empty state remains functional across layouts", async () => {
    service.getAll.mockResolvedValue([]);
    render(<AdminServiceRequestListPage layoutMode="card" />);
    expect(await screen.findByText("No service requests found.")).toBeInTheDocument();
  });

  it("15. error state remains functional across layouts", async () => {
    service.getAll.mockRejectedValueOnce(new Error("network error"));
    render(<AdminServiceRequestListPage layoutMode="card" />);
    expect(await screen.findByRole("alert")).toHaveTextContent("Failed to load service requests");
    expect(screen.getByRole("button", { name: "Retry" })).toBeInTheDocument();
  });

  it("16. dynamically switches between table and card layout via ResizeObserver threshold", async () => {
    let observerCallback;
    class MockResizeObserver {
      constructor(cb) {
        observerCallback = cb;
      }
      observe() {}
      disconnect() {}
    }
    vi.stubGlobal("ResizeObserver", MockResizeObserver);

    service.getAll.mockResolvedValue([request]);
    render(<AdminServiceRequestListPage />);
    expect(await screen.findByRole("table")).toBeInTheDocument();

    // Trigger resize to 800px (below 1080px threshold -> card layout)
    act(() => {
      observerCallback([{ contentRect: { width: 800 } }]);
    });
    expect(await screen.findByTestId("admin-request-card")).toBeInTheDocument();
    expect(screen.queryByRole("table")).not.toBeInTheDocument();

    // Trigger resize to 1200px (above 1080px threshold -> table layout)
    act(() => {
      observerCallback([{ contentRect: { width: 1200 } }]);
    });
    expect(await screen.findByRole("table")).toBeInTheDocument();
    expect(screen.queryByTestId("admin-request-card")).not.toBeInTheDocument();

    vi.unstubAllGlobals();
  });
  it("17. wide table provides explicit colgroup and cell classes for balanced desktop layout", async () => {
    service.getAll.mockResolvedValue([request]);
    const { container } = render(<AdminServiceRequestListPage layoutMode="table" />);
    const table = await screen.findByRole("table");
    expect(table).toHaveClass("monitoring-table");

    const colClasses = Array.from(container.querySelectorAll("colgroup col")).map((col) => col.className);
    expect(colClasses).toEqual([
      "col-request",
      "col-category",
      "col-urgency",
      "col-status",
      "col-location",
      "col-confidence",
      "col-created",
      "col-action",
    ]);

    const thClasses = Array.from(table.querySelectorAll("thead th")).map((th) => th.className);
    expect(thClasses).toEqual([
      "th-request",
      "th-category",
      "th-urgency",
      "th-status",
      "th-location",
      "th-confidence",
      "th-created",
      "th-action",
    ]);

    const firstRowCells = Array.from(table.querySelectorAll("tbody tr:first-child td")).map((td) => td.className);
    expect(firstRowCells).toEqual([
      "cell-request",
      "cell-category",
      "cell-urgency",
      "cell-status",
      "cell-location",
      "cell-confidence",
      "cell-created",
      "cell-action",
    ]);
  });
});

describe("Created date and time column presentation", () => {
  it("renders Created timestamp with date and time in separate DOM elements", async () => {
    service.getAll.mockResolvedValue([request]);
    const { container } = render(<AdminServiceRequestListPage layoutMode="table" />);
    await screen.findByRole("table");
    const containerEl = container.querySelector(".cell-created .created-date-time");
    expect(containerEl).toBeInTheDocument();

    const created = new Date(request.createdAt);
    const expectedDate = created.toLocaleDateString();
    const expectedTime = created.toLocaleTimeString([], {
      hour: "numeric",
      minute: "2-digit",
      second: "2-digit",
    });

    const children = Array.from(containerEl.children);
    expect(children).toHaveLength(2);
    expect(children[0]).toHaveClass("created-date");
    expect(children[1]).toHaveClass("created-time");
    expect(children[0].textContent).toBe(expectedDate);
    expect(children[1].textContent).toBe(expectedTime);
  });

  it("time renders separately from date and AM/PM remains attached to time", async () => {
    service.getAll.mockResolvedValue([request]);
    render(<AdminServiceRequestListPage layoutMode="table" />);
    const table = await screen.findByRole("table");
    const created = new Date(request.createdAt);
    const expectedDate = created.toLocaleDateString();
    const expectedTime = created.toLocaleTimeString([], {
      hour: "numeric",
      minute: "2-digit",
      second: "2-digit",
    });

    const cell = within(table).getByRole("cell", { name: new RegExp(expectedDate) });
    const dateSpan = within(cell).getByText(expectedDate);
    const timeSpan = within(cell).getByText(expectedTime);

    expect(dateSpan).toHaveClass("created-date");
    expect(timeSpan).toHaveClass("created-time");
    // Ensure AM/PM is inside the time span text without breaking off
    expect(expectedTime).toMatch(/\b(AM|PM)\b/);
    expect(timeSpan.textContent).toMatch(/\b(AM|PM)\b/);
    // Ensure date span does not contain time and time span does not contain date
    expect(dateSpan.textContent).not.toContain(expectedTime);
    expect(timeSpan.textContent).not.toContain(expectedDate);
  });

  it("Created container enforces flex-direction column and nowrap across layout modes", async () => {
    service.getAll.mockResolvedValue([request]);
    const { container } = render(<AdminServiceRequestListPage layoutMode="table" />);
    await screen.findByRole("table");
    const cellDateTime = container.querySelector(".cell-created .created-date-time");
    expect(cellDateTime).toBeInTheDocument();
    expect(cellDateTime.querySelector(".created-date")).toHaveClass("created-date");
    expect(cellDateTime.querySelector(".created-time")).toHaveClass("created-time");
  });

  it("CreatedDateTime component handles missing and invalid timestamps gracefully", () => {
    const { rerender } = render(<CreatedDateTime value={null} />);
    expect(screen.getByText("—")).toBeInTheDocument();

    rerender(<CreatedDateTime value={undefined} />);
    expect(screen.getByText("—")).toBeInTheDocument();

    rerender(<CreatedDateTime value="invalid-timestamp" />);
    expect(screen.getByText("—")).toBeInTheDocument();
  });

  it("existing service-request table behavior remains unchanged with all headers, badges, and actions", async () => {
    service.getAll.mockResolvedValue([request]);
    render(<AdminServiceRequestListPage layoutMode="table" />);
    const table = await screen.findByRole("table");

    const headers = within(table).getAllByRole("columnheader").map((th) => th.textContent);
    expect(headers).toEqual(["Request", "Category", "Urgency", "Status", "Location", "Confidence", "Created", "Action"]);

    const rows = within(table).getAllByRole("row").slice(1);
    const row = rows[0];
    expect(within(row).getByText("SR-A1B2C3D4")).toBeInTheDocument();
    expect(within(row).getByText("Plumbing")).toBeInTheDocument();
    expect(within(row).getByText("High")).toBeInTheDocument();
    expect(within(row).getByText("Awaiting Information")).toBeInTheDocument();
    expect(within(row).getByText("Colombo")).toBeInTheDocument();
    expect(within(row).getByText("87%")).toBeInTheDocument();
    expect(within(row).getByRole("button", { name: "View Details" })).toBeInTheDocument();
  });
});
