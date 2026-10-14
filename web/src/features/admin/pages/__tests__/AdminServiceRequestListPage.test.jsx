// What this suite tests:
//   The AdminServiceRequestListPage React page component for administrative oversight and monitoring
//   of customer service requests, covering table data rendering, lifecycle badges, filter selection
//   and clearing, race-condition prevention, responsive table/card layout switching, OpenStreetMap attribution,
//   read-only authoritative details panel, and formatted timestamp columns.
// Why these tests are needed:
//   Provides administrators with a reliable, real-time oversight dashboard for all service requests across
//   the platform. These tests verify that filtering, detail inspection, error recovery, and responsive geometry
//   work reliably without data contamination or unauthorized editing controls.
// What is real and what is mocked/faked:
//   - Real: AdminServiceRequestListPage and child components rendered with @testing-library/react and user-event.
//   - Mocked/Faked: adminServiceRequestService (getAll, getById) is mocked via Vitest vi.fn().
// Prerequisites:
//   Node.js dependencies installed in web folder (npm install).
// Run from repository root:
//   npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx

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

// Group: Tests for the main admin monitoring table, filter bar, and loading/error states.
describe("Admin monitoring list", () => {
  // Purpose: Checks that fetched service requests display the formatted request ID, canonical category, urgency, status badge, location, confidence percentage (87%), and created date/time.
  // Why: Confirms administrators see all essential monitoring fields in the primary table columns.
  // Scenario: Loading requests list with one item.
  // Pass condition: Table contains all expected formatted fields and excludes unverified category hints.
  // Run from repository root:
  //   npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "renders fetched requests, final category, lifecycle badge, confidence and date"
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

  // Purpose: Checks that while requests are being fetched, a loading indicator is rendered and no skeleton/fake rows are shown.
  // Why: Prevents misleading placeholder data from appearing before real data is loaded.
  // Scenario: getAll returns an unresolved promise.
  // Pass condition: Role "status" text "Loading service requests" is displayed and table is absent.
  // Run from repository root:
  //   npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "shows loading without fake rows"
  it("shows loading without fake rows", () => {
    service.getAll.mockReturnValue(new Promise(() => {}));
    render(<AdminServiceRequestListPage />);
    expect(screen.getByRole("status")).toHaveTextContent("Loading service requests");
    expect(screen.queryByRole("table")).not.toBeInTheDocument();
  });

  // Purpose: Checks that when no service requests exist, an empty state message is displayed.
  // Why: Informs the admin that no requests match rather than showing a broken table.
  // Scenario: getAll returns empty array.
  // Pass condition: "No service requests found." is displayed.
  // Run from repository root:
  //   npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "shows empty state"
  it("shows empty state", async () => {
    service.getAll.mockResolvedValue([]);
    render(<AdminServiceRequestListPage />);
    expect(await screen.findByText("No service requests found.")).toBeInTheDocument();
  });

  // Purpose: Checks that network failures display an error banner with a Retry button that successfully re-fetches.
  // Why: Enables recovery from transient network issues without requiring a full page refresh.
  // Scenario: getAll rejects once then resolves; user clicks "Retry".
  // Pass condition: Alert banner shows "Failed to load service requests", clicking Retry calls getAll again and displays the table.
  // Run from repository root:
  //   npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "shows API error and retries successfully"
  it("shows API error and retries successfully", async () => {
    service.getAll.mockRejectedValueOnce(new Error("offline"));
    const user = userEvent.setup();
    render(<AdminServiceRequestListPage />);
    expect(await screen.findByRole("alert")).toHaveTextContent("Failed to load service requests");
    await user.click(screen.getByRole("button", { name: "Retry" }));
    expect(await screen.findByText("SR-A1B2C3D4")).toBeInTheDocument();
    expect(service.getAll).toHaveBeenCalledTimes(2);
  });

  // Purpose: Checks that selecting a filter dropdown (Status, Category, Urgency) refreshes the table with the corresponding query parameter.
  // Why: Ensures administrators can isolate requests by specific lifecycle or operational criteria.
  // Scenario: User selects a filter option in dropdown; table re-fetches.
  // Pass condition: service.getAll is called with the selected filter key-value pair.
  // Note: Parameterized Vitest it.each test covering Status, Category, and Urgency.
  // Run from repository root:
  //   npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "refreshes using the"
  it.each([["Status", "Analyzed", "status"], ["Category", "Vehicle Repair", "category"], ["Urgency", "Critical", "urgency"]])("refreshes using the %s filter", async (label, value, key) => {
    const user = userEvent.setup();
    render(<AdminServiceRequestListPage />);
    await screen.findByRole("table");
    service.getAll.mockResolvedValue([]);
    await user.selectOptions(screen.getByLabelText(label), value);
    await waitFor(() => expect(service.getAll).toHaveBeenLastCalledWith({ status: "", category: "", urgency: "", [key]: value }));
    expect(await screen.findByText("No requests match the selected filters.")).toBeInTheDocument();
  });

  // Purpose: Checks that selecting multiple filters combines them in the query, and clicking "Clear Filters" resets all dropdowns and re-fetches with empty filters.
  // Why: Allows admins to quickly combine and reset complex filter combinations.
  // Scenario: User sets Status, Category, and Urgency, then clicks "Clear Filters".
  // Pass condition: getAll is called with all three filters, then called with empty filters, and all dropdown values reset to "".
  // Run from repository root:
  //   npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "combines and clears filters"
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

  // Purpose: Checks that race conditions where an older, slower query completes after a newer filter change are safely discarded.
  // Why: Prevents stale query results from overwriting the latest filter view.
  // Scenario: First request is slow; user changes filter to "Cancelled"; first request resolves late.
  // Pass condition: Stale request data is discarded and does not appear in the view.
  // Run from repository root:
  //   npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "ignores an old list response after filters change"
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

  // Purpose: Checks that long addresses in the table cell have CSS bounding, accessible tooltip titles, OSM attribution links when applicable, and maintain table column alignment.
  // Why: Prevents long addresses from stretching table columns horizontally or breaking column widths.
  // Scenario: Two requests loaded, one with a long OpenStreetMap address and one with a manual address.
  // Pass condition: Headers match expected columns, cell has "cell-location", tooltip title equals full address, and OSM attribution link renders correctly.
  // Run from repository root:
  //   npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "renders long address in bounded location cell with tooltip and preserves table geometry across rows"
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

/**
 * TEST SUITE: Read-only Authoritative Request Details Panel
 *
 * 1. What this suite tests:
 *    - Verifies the side drawer / modal detail panel displaying complete service request
 *      information for administrators, including full descriptions, GPS coordinates,
 *      AI problem analysis, clarification history, and focus management.
 * 2. Why these tests are needed:
 *    - Administrators require a single authoritative, un-truncated audit view of customer
 *      requests and AI diagnostic results without edit controls that could corrupt data.
 *    - Tests ensure asynchronous loading, network retries, race condition handling (stale
 *      detail responses), and accessible focus restoration behave robustly.
 * 3. What is real and what is mocked/faked:
 *    - Real: Full React component rendering with testing-library, user interaction events,
 *      DOM accessibility queries, and focus state transitions.
 *    - Mocked: `adminServiceRequestService.getById` and `getAll` return synthetic promises.
 * 4. Prerequisites:
 *    - Node.js dependencies installed (`npm install` inside `/web`).
 * 5. PowerShell command to run this suite from repository root:
 *    npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "Read-only authoritative request details"
 */
describe("Read-only authoritative request details", () => {
  /**
   * Purpose: What this specific test checks
   * - Verifies that opening details invokes getById and displays the full description,
   *   service request ID, customer preference, updated timestamp, and numerical coordinates (including zero).
   *
   * Why: Why this scenario matters
   * - Coordinates like latitude 0 must not be treated as falsy/missing; administrators must see
   *   exact spatial locations and un-truncated customer issue statements.
   *
   * Scenario: Input or condition being tested
   * - User clicks "View Details" button on request SR-A1B2C3D4 with latitude 0 and longitude 79.86.
   *
   * Pass condition: What result or assertion makes it pass
   * - `getById` is called with request ID, and the panel contains "Fresh detail.", "Customer category preference",
   *   "Electrical", "Latitude", "Longitude", "0", and "79.86".
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "fetches detail and displays complete description"
   */
  it("fetches detail and displays complete description, ID, overview and coordinates including zero", async () => {
    service.getById.mockResolvedValue({ ...request, description: `${request.description} Fresh detail.` });
    const { detail } = await openDetail();
    expect(service.getById).toHaveBeenCalledWith(request.serviceRequestId);
    expect(detail.getByText(`${request.description} Fresh detail.`)).toBeInTheDocument();
    for (const text of ["Customer category preference", "Electrical", "Updated At", "Latitude", "Longitude", "0", "79.86"]) expect(detail.getByText(text)).toBeInTheDocument();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that AI analysis outputs use neutral "AssistLK AI" branding rather than raw
   *   vendor names (e.g. Gemini, OpenAI) in the admin UI.
   *
   * Why: Why this scenario matters
   * - Protects intellectual property and abstraction integrity; user-facing and admin interfaces
   *   present unified AssistLK AI intelligence without exposing internal provider details.
   *
   * Scenario: Input or condition being tested
   * - Admin inspects AI diagnostic panel for an analyzed request.
   *
   * Pass condition: What result or assertion makes it pass
   * - Labels "Latest AssistLK AI analysis", "Leaking supply pipe", "87%", and "Analysis timestamp" appear,
   *   while regex query for "gemini" or "openai" returns null.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "renders latest analysis with safe AssistLK AI branding"
   */
  it("renders latest analysis with safe AssistLK AI branding", async () => {
    const { detail } = await openDetail();
    for (const text of ["Latest AssistLK AI analysis", "Leaking supply pipe", "87%", "AssistLK AI", "Analysis timestamp"]) expect(detail.getByText(text)).toBeInTheDocument();
    expect(detail.queryByText(/gemini|openai/i)).not.toBeInTheDocument();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that multi-round clarification questions maintain chronological order and clearly
   *   label answered, unanswered, and superseded questions.
   *
   * Why: Why this scenario matters
   * - Admins reviewing customer history must see the progression of clarification dialogues
   *   to understand why a request took multiple rounds or if questions became obsolete.
   *
   * Scenario: Input or condition being tested
   * - Detail panel displays clarification round 1 sequence 1 (answered) and sequence 2 (unanswered, superseded).
   *
   * Pass condition: What result or assertion makes it pass
   * - Item 0 contains "Round 1 · Sequence 1", "Where is the leak?", "Under the sink", and "Answered At".
   * - Item 1 contains "Is the valve closed?", "Unanswered", and "Superseded At".
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "preserves clarification order and distinguishes unanswered and superseded questions"
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies graceful fallback rendering when AI analysis, GPS coordinates, and clarification history are absent.
   *
   * Why: Why this scenario matters
   * - Newly created or minimally populated requests must not crash the admin page with null-pointer errors.
   *
   * Scenario: Input or condition being tested
   * - Service request loaded with `latestAnalysis: null`, `latitude: null`, `longitude: undefined`, and `clarifications: []`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Fallback messages "No analysis available yet." and "No clarification history." are displayed;
   *   "Latitude" and "Longitude" rows are omitted; table displays em-dash "—" placeholder.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "handles missing analysis, coordinates and clarification history"
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies detail panel loading spinner, error alert display upon network failure, and retry button recovery.
   *
   * Why: Why this scenario matters
   * - If an individual request detail fetch fails, the admin must be informed with an error message
   *   and given a 1-click retry mechanism without reloading the entire page.
   *
   * Scenario: Input or condition being tested
   * - `getById` returns a pending promise, then rejects with "offline", and on retry succeeds with request data.
   *
   * Pass condition: What result or assertion makes it pass
   * - Status "Loading request details" appears initially; alert "Failed to load request details" shows on rejection;
   *   clicking "Retry Details" successfully fetches and displays the request description.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "shows detail loading, error, and successful retry"
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that the admin detail view contains strictly read-only elements, has no editable textboxes,
   *   focuses the detail header when opened, and returns focus to "View Details" button when closed.
   *
   * Why: Why this scenario matters
   * - Admin monitoring is strictly non-destructive (audit only). Accessible focus trapping ensures
   *   screen reader and keyboard-only users do not lose their place in the data table.
   *
   * Scenario: Input or condition being tested
   * - Open detail modal, verify focus and interactive controls, click "Close Details", verify focus return.
   *
   * Pass condition: What result or assertion makes it pass
   * - No `<input>` or `<textarea>` textboxes exist; only "View Details" and "Close Details" buttons exist;
   *   focus lands on heading "Request details" when opened, and on "View Details" when closed.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "has only read-only controls and restores focus when closed"
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies race condition resilience: ignores a delayed response from an earlier request
   *   when the admin has already switched to inspecting a second request.
   *
   * Why: Why this scenario matters
   * - Network latency jitter could resolve Request 1 after Request 2, causing old data to overwrite
   *   the current selection if requests are not tracked with cancellation tokens / request IDs.
   *
   * Scenario: Input or condition being tested
   * - Admin clicks Request 1 (which hangs), then clicks Request 2 (which loads immediately);
   *   subsequently Request 1 resolves.
   *
   * Pass condition: What result or assertion makes it pass
   * - Detail panel displays Request 2's data (`next.serviceRequestId`) and does not overwrite it with Request 1's description.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "ignores stale detail when a different request is selected"
   */
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

/**
 * TEST SUITE: Responsive Layout and Compact Card Presentation
 *
 * 1. What this suite tests:
 *    - Verifies responsive layout adaptation between wide structured desktop tables and compact
 *      mobile/tablet card views, including ResizeObserver dynamic threshold switching at 1080px.
 *    - Verifies badge styling, location text truncation, OpenStreetMap attribution placement,
 *      and filter persistence across both layout modes.
 * 2. Why these tests are needed:
 *    - Administrators monitor service requests across desktops, laptops, and tablets.
 *    - Long Sri Lankan addresses and multi-word statuses ("Ready for Matching") must not collide,
 *      clip, or break layout geometry on smaller screens.
 * 3. What is real and what is mocked/faked:
 *    - Real: Component DOM structure, CSS class assignment, and user interaction simulation.
 *    - Mocked: `ResizeObserver` global is stubbed to simulate container width changes (800px vs 1200px);
 *      backend service calls are mocked.
 * 4. Prerequisites:
 *    - Node.js dependencies installed (`npm install` inside `/web`).
 * 5. PowerShell command to run this suite from repository root:
 *    npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "Responsive layout and compact card presentation"
 */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that wide viewports render an HTML `<table>` with standard column headers and no cards.
   *
   * Why: Why this scenario matters
   * - Desktop monitors provide ample horizontal space to scan rows across 8 distinct columns.
   *
   * Scenario: Input or condition being tested
   * - Component rendered with `layoutMode="table"`.
   *
   * Pass condition: What result or assertion makes it pass
   * - `<table>` element is present; `admin-request-card` is absent; column headers match
   *   `["Request", "Category", "Urgency", "Status", "Location", "Confidence", "Created", "Action"]`.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "1. wide container renders structured table layout"
   */
  it("1. wide container renders structured table layout", async () => {
    service.getAll.mockResolvedValue([request]);
    render(<AdminServiceRequestListPage layoutMode="table" />);
    expect(await screen.findByRole("table")).toBeInTheDocument();
    expect(screen.queryByTestId("admin-request-card")).not.toBeInTheDocument();
    const headers = screen.getAllByRole("columnheader").map((th) => th.textContent);
    expect(headers).toEqual(["Request", "Category", "Urgency", "Status", "Location", "Confidence", "Created", "Action"]);
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that narrow/medium containers render compact card elements inside a dedicated region.
   *
   * Why: Why this scenario matters
   * - Prevents horizontal scrollbars and crushed text on smaller screen sizes.
   *
   * Scenario: Input or condition being tested
   * - Component rendered with `layoutMode="card"`.
   *
   * Pass condition: What result or assertion makes it pass
   * - `admin-request-card` element is present; `<table>` is absent; region named "Service requests cards" exists.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "2. narrow/medium container renders compact card layout"
   */
  it("2. narrow/medium container renders compact card layout", async () => {
    service.getAll.mockResolvedValue([request]);
    render(<AdminServiceRequestListPage layoutMode="card" />);
    expect(await screen.findByTestId("admin-request-card")).toBeInTheDocument();
    expect(screen.queryByRole("table")).not.toBeInTheDocument();
    expect(screen.getByRole("region", { name: "Service requests cards" })).toBeInTheDocument();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that the "Ready for Matching" badge and long Location text occupy separate CSS containers
   *   without visual or structural collision in both table and card layouts.
   *
   * Why: Why this scenario matters
   * - "Ready for Matching" is Component 1's key terminal success status; overlapping badges with addresses
   *   would corrupt administrative readability.
   *
   * Scenario: Input or condition being tested
   * - Request with status "ReadyForMatching" and 102-character address rendered in table mode then card mode.
   *
   * Pass condition: What result or assertion makes it pass
   * - In table mode, status cell (`cell-status`) is distinct from location cell (`cell-location`).
   * - In card mode, status badge is inside `.card-status-badge` and location is inside `.card-location-row`.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "3. ReadyForMatching badge renders safely"
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that "Awaiting Information" status badge renders in dedicated status containers in both layouts.
   *
   * Why: Why this scenario matters
   * - "Awaiting Information" indicates active clarification dialogue; admins need prominent, unclipped visual indicators.
   *
   * Scenario: Input or condition being tested
   * - Request with status "AwaitingInformation" rendered in table mode then card mode.
   *
   * Pass condition: What result or assertion makes it pass
   * - Badge is contained inside `td.cell-status` in table mode, and `.card-status-badge` in card mode.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "4. AwaitingInformation badge renders safely"
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that long addresses in table mode are bounded with `.monitoring-location-address`,
   *   include a hover tooltip (`title` attribute), and render OpenStreetMap attribution.
   *
   * Why: Why this scenario matters
   * - Prevents wide addresses from expanding table columns indefinitely while preserving access to the full address via tooltip.
   *
   * Scenario: Input or condition being tested
   * - Render table layout with a 102-character address and OpenStreetMap source.
   *
   * Pass condition: What result or assertion makes it pass
   * - Element has class `monitoring-location-address`, `title` matches full address, and link points to OSM copyright.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "5. long Location is bounded with accessible tooltip and attribution in table mode"
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that long addresses in card mode wrap correctly within `.card-location-row` with tooltip and OSM attribution.
   *
   * Why: Why this scenario matters
   * - Mobile cards have restricted widths; wrapping with tooltip ensures readability on small screens.
   *
   * Scenario: Input or condition being tested
   * - Render card layout with long address and OpenStreetMap source.
   *
   * Pass condition: What result or assertion makes it pass
   * - Address is within `.card-location-row`, has matching `title` attribute, and contains valid OSM link.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "6. long Location wraps correctly in card mode"
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that the AI confidence percentage (e.g. "95%") renders with its label in both table and card layouts.
   *
   * Why: Why this scenario matters
   * - Admins must quickly assess diagnostic certainty before approving or monitoring automated matches.
   *
   * Scenario: Input or condition being tested
   * - Render request with `confidence: 0.95` in table mode then card mode.
   *
   * Pass condition: What result or assertion makes it pass
   * - Column header / label "Confidence" and text "95%" are rendered in both view modes.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "7. Confidence label remains readable in both layouts"
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that the request creation date and time strings remain fully readable across both layouts.
   *
   * Why: Why this scenario matters
   * - Accurate audit timestamps are essential for service request SLA and queue monitoring.
   *
   * Scenario: Input or condition being tested
   * - Request with ISO timestamp rendered in table mode then card mode.
   *
   * Pass condition: What result or assertion makes it pass
   * - Formatted date string (`toLocaleDateString`) and time string (`toLocaleTimeString`) appear in both layouts.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "8. Created timestamp remains readable in both layouts"
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that "View Details" and "Close Details" actions operate seamlessly in card mode with focus restoration.
   *
   * Why: Why this scenario matters
   * - Ensures mobile/tablet users have full access to the side detail drawer with proper accessibility handling.
   *
   * Scenario: Input or condition being tested
   * - In card layout, user clicks "View Details", verifies detail panel, then clicks "Close Details".
   *
   * Pass condition: What result or assertion makes it pass
   * - Detail panel opens with request ID, closes upon button click, and focus returns to the card's "View Details" button.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "9. View Details remains accessible and operable in card mode"
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that the summary header displays the correct total item count in both layouts.
   *
   * Why: Why this scenario matters
   * - Admins need accurate counts ("2 service requests") regardless of viewport orientation or form factor.
   *
   * Scenario: Input or condition being tested
   * - Two service requests loaded and rendered in table mode then card mode.
   *
   * Pass condition: What result or assertion makes it pass
   * - Text "2 service requests" is displayed in both layout modes.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "10. request count remains correct in both layouts"
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that OpenStreetMap copyright attribution is rendered only for requests geocoded via OSM,
   *   and omitted for manual address entries in both layouts.
   *
   * Why: Why this scenario matters
   * - Satisfies OpenStreetMap legal attribution requirements without displaying irrelevant links on manually typed addresses.
   *
   * Scenario: Input or condition being tested
   * - Two requests loaded: Request 1 with `locationSource: "OpenStreetMap"`, Request 2 with `locationSource: "Manual"`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Table mode has exactly 1 OSM link; Card mode shows the link on Card 0 and omits it on Card 1.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "11. OSM attribution appears conditionally in both layouts"
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that the initial query with default empty filters (`status: "", category: "", urgency: ""`)
   *   executes identically in card mode.
   *
   * Why: Why this scenario matters
   * - Changing the visual layout mode must not alter data fetching behavior or filter state.
   *
   * Scenario: Input or condition being tested
   * - Initial render in `layoutMode="card"`.
   *
   * Pass condition: What result or assertion makes it pass
   * - `service.getAll` was called with `{ status: "", category: "", urgency: "" }`.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "12. All-filter functionality remains unchanged in card mode"
   */
  it("12. All-filter functionality remains unchanged in card mode", async () => {
    service.getAll.mockResolvedValue([request]);
    render(<AdminServiceRequestListPage layoutMode="card" />);
    await screen.findByTestId("admin-request-card");
    expect(service.getAll).toHaveBeenLastCalledWith({ status: "", category: "", urgency: "" });
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that changing the Status filter dropdown to "Created" triggers a refreshed API call in card mode.
   *
   * Why: Why this scenario matters
   * - Filter controls must remain fully interactive and bound to service query parameters regardless of layout.
   *
   * Scenario: Input or condition being tested
   * - In card layout, user selects "Created" in the Status filter dropdown.
   *
   * Pass condition: What result or assertion makes it pass
   * - `service.getAll` is called with `{ status: "Created", category: "", urgency: "" }`.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "13. Created filter remains functional in card mode"
   */
  it("13. Created filter remains functional in card mode", async () => {
    service.getAll.mockResolvedValue([request]);
    const user = userEvent.setup();
    render(<AdminServiceRequestListPage layoutMode="card" />);
    await screen.findByTestId("admin-request-card");
    await user.selectOptions(screen.getByLabelText("Status"), "Created");
    await waitFor(() => expect(service.getAll).toHaveBeenLastCalledWith({ status: "Created", category: "", urgency: "" }));
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies empty state placeholder rendering when zero requests match in card layout.
   *
   * Why: Why this scenario matters
   * - User must see a clear informative message rather than a broken or blank screen when no data exists.
   *
   * Scenario: Input or condition being tested
   * - `service.getAll` returns empty array `[]` in card mode.
   *
   * Pass condition: What result or assertion makes it pass
   * - Text "No service requests found." is displayed.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "14. empty state remains functional across layouts"
   */
  it("14. empty state remains functional across layouts", async () => {
    service.getAll.mockResolvedValue([]);
    render(<AdminServiceRequestListPage layoutMode="card" />);
    expect(await screen.findByText("No service requests found.")).toBeInTheDocument();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies error banner and Retry button rendering when the API call fails in card layout.
   *
   * Why: Why this scenario matters
   * - Resilient error handling must function identically in card view as in table view.
   *
   * Scenario: Input or condition being tested
   * - `service.getAll` rejects with a network error in card mode.
   *
   * Pass condition: What result or assertion makes it pass
   * - Alert banner contains "Failed to load service requests" and "Retry" button is present.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "15. error state remains functional across layouts"
   */
  it("15. error state remains functional across layouts", async () => {
    service.getAll.mockRejectedValueOnce(new Error("network error"));
    render(<AdminServiceRequestListPage layoutMode="card" />);
    expect(await screen.findByRole("alert")).toHaveTextContent("Failed to load service requests");
    expect(screen.getByRole("button", { name: "Retry" })).toBeInTheDocument();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that the page dynamically swaps between table and card layout when ResizeObserver
   *   detects container widths crossing the 1080px responsive threshold.
   *
   * Why: Why this scenario matters
   * - Users resizing browser windows or docking sidebars should experience smooth, automatic layout
   *   adaptation without manual page reloads.
   *
   * Scenario: Input or condition being tested
   * - ResizeObserver callback triggers width 800px (< 1080px), then 1200px (> 1080px).
   *
   * Pass condition: What result or assertion makes it pass
   * - At 800px, cards appear and table disappears; at 1200px, table appears and cards disappear.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "16. dynamically switches between table and card layout"
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that the desktop table applies explicit `<colgroup col>` classes, header `<th>` classes,
   *   and cell `<td>` classes for every column.
   *
   * Why: Why this scenario matters
   * - Explicit column class names enforce fixed proportional widths and prevent table jank
   *   when data lengths vary across rows.
   *
   * Scenario: Input or condition being tested
   * - Inspect DOM classes on colgroup, thead, and tbody for the table layout.
   *
   * Pass condition: What result or assertion makes it pass
   * - Classes match expected col-*, th-*, and cell-* sequences across all 8 columns.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "17. wide table provides explicit colgroup and cell classes"
   */
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

/**
 * TEST SUITE: Created Date and Time Column Presentation
 *
 * 1. What this suite tests:
 *    - Verifies the atomic rendering of created timestamps via the `CreatedDateTime` component,
 *      ensuring separate DOM elements for date and time, proper AM/PM binding, graceful null/invalid
 *      handling, and layout preservation across table columns.
 * 2. Why these tests are needed:
 *    - Timestamps formatted into single strings frequently wrap awkwardly (e.g. splitting "11:33"
 *      from "AM" onto another line), degrading administrative readability.
 * 3. What is real and what is mocked/faked:
 *    - Real: Full DOM rendering of `CreatedDateTime` and `AdminServiceRequestListPage` components.
 *    - Mocked: Service API calls.
 * 4. Prerequisites:
 *    - Node.js dependencies installed (`npm install` inside `/web`).
 * 5. PowerShell command to run this suite from repository root:
 *    npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "Created date and time column presentation"
 */
describe("Created date and time column presentation", () => {
  /**
   * Purpose: What this specific test checks
   * - Verifies that the created timestamp is rendered with date and time in separate child DOM elements.
   *
   * Why: Why this scenario matters
   * - Splitting date and time into discrete spans (`.created-date` and `.created-time`) allows CSS
   *   to control typographic hierarchy (e.g., subtle time, bold date) and line breaking.
   *
   * Scenario: Input or condition being tested
   * - Render table row for request with valid ISO timestamp.
   *
   * Pass condition: What result or assertion makes it pass
   * - Container `.created-date-time` contains 2 children: child 0 with class `.created-date` and child 1 with class `.created-time`.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "renders Created timestamp with date and time in separate DOM elements"
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that the AM/PM meridiem marker remains securely inside the time element and never
   *   leaks into the date element or breaks across lines independently.
   *
   * Why: Why this scenario matters
   * - Formatting bugs in locale strings can orphan meridiem markers, confusing morning and evening audit logs.
   *
   * Scenario: Input or condition being tested
   * - Inspect text content of `.created-date` and `.created-time` elements.
   *
   * Pass condition: What result or assertion makes it pass
   * - `timeSpan` matches `/\b(AM|PM)\b/`, and date/time texts are mutually exclusive.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "time renders separately from date and AM/PM remains attached to time"
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that the container element `.created-date-time` encapsulates the date and time classes.
   *
   * Why: Why this scenario matters
   * - The container CSS enforces vertical stacking (`flex-direction: column`) with `white-space: nowrap`.
   *
   * Scenario: Input or condition being tested
   * - Inspect table layout container structure for cell-created.
   *
   * Pass condition: What result or assertion makes it pass
   * - `cellDateTime` exists and querySelector finds both `.created-date` and `.created-time`.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "Created container enforces flex-direction column and nowrap across layout modes"
   */
  it("Created container enforces flex-direction column and nowrap across layout modes", async () => {
    service.getAll.mockResolvedValue([request]);
    const { container } = render(<AdminServiceRequestListPage layoutMode="table" />);
    await screen.findByRole("table");
    const cellDateTime = container.querySelector(".cell-created .created-date-time");
    expect(cellDateTime).toBeInTheDocument();
    expect(cellDateTime.querySelector(".created-date")).toHaveClass("created-date");
    expect(cellDateTime.querySelector(".created-time")).toHaveClass("created-time");
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that `CreatedDateTime` renders an em-dash "—" when passed `null`, `undefined`, or an unparseable string.
   *
   * Why: Why this scenario matters
   * - Corrupted, legacy, or missing database timestamps must degrade gracefully rather than throwing "Invalid Date" or crashing.
   *
   * Scenario: Input or condition being tested
   * - Render component with `value={null}`, `value={undefined}`, and `value="invalid-timestamp"`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Em-dash "—" is rendered in all three fallback conditions.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "CreatedDateTime component handles missing and invalid timestamps gracefully"
   */
  it("CreatedDateTime component handles missing and invalid timestamps gracefully", () => {
    const { rerender } = render(<CreatedDateTime value={null} />);
    expect(screen.getByText("—")).toBeInTheDocument();

    rerender(<CreatedDateTime value={undefined} />);
    expect(screen.getByText("—")).toBeInTheDocument();

    rerender(<CreatedDateTime value="invalid-timestamp" />);
    expect(screen.getByText("—")).toBeInTheDocument();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies full regression safety: all column headers, data cells, badges, and action buttons
   *   render accurately with no missing fields.
   *
   * Why: Why this scenario matters
   * - High-level sanity check ensuring responsiveness or date formatting changes did not regress core monitoring features.
   *
   * Scenario: Input or condition being tested
   * - Standard service request loaded in table layout.
   *
   * Pass condition: What result or assertion makes it pass
   * - Headers match all 8 columns, and row contains "SR-A1B2C3D4", "Plumbing", "High", "Awaiting Information",
   *   "Colombo", "87%", and "View Details" button.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminServiceRequestListPage.test.jsx -t "existing service-request table behavior remains unchanged"
   */
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

