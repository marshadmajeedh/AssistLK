import { act, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import AdminServiceRequestListPage from "../AdminServiceRequestListPage";
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
    for (const text of ["Plumbing", "High", "Awaiting Information", "Colombo", "87%", new Date(request.createdAt).toLocaleString()]) {
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
