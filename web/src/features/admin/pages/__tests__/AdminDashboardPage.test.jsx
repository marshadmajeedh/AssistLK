import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import AdminDashboardPage from "../AdminDashboardPage";
import requestsService from "../../services/adminServiceRequestService";
import agentService from "../../../aiWorkflows/services/agentMonitoringService";

vi.mock("../../services/adminServiceRequestService", () => ({ default: { getAll: vi.fn() } }));
vi.mock("../../../aiWorkflows/services/agentMonitoringService", () => ({ default: { getMetrics: vi.fn() } }));
const statuses = ["Created", "Analyzing", "AwaitingInformation", "Analyzed", "ReadyForMatching", "Cancelled", "Analyzed"];
const requests = statuses.map((status, index) => ({
  serviceRequestId: `a000000${index}-1234-4567-8901-123456789abc`,
  category: `Category ${index}`, urgency: "High", status,
  createdAt: `2026-09-${12 - index}T10:00:00Z`, latestAnalysis: status === "Created" ? { confidence: 0.9 } : null,
}));
const executions = Array.from({ length: 6 }, (_, index) => ({
  id: `metric-${index}`, agentName: `RecordedAgent${index}`,
  status: index === 1 ? "Failed" : "Completed", executionTimeMs: index === 0 ? 1200 : 0,
  toolCalls: index === 0 ? 3 : 0, createdAtUtc: `2026-09-${12 - index}T10:00:00Z`,
}));
beforeEach(() => {
  vi.resetAllMocks();
  requestsService.getAll.mockResolvedValue(requests);
  agentService.getMetrics.mockResolvedValue(executions);
});
function renderDashboard() {
  return render(<MemoryRouter initialEntries={["/dashboard"]}><Routes>
    <Route path="/dashboard" element={<AdminDashboardPage />} />
    <Route path="/admin/service-requests" element={<h1>Request destination</h1>} />
    <Route path="/ai-workflows" element={<h1>Workflow destination</h1>} />
  </Routes></MemoryRouter>);
}
async function loaded() {
  renderDashboard();
  await screen.findByText("Total Service Requests");
  await screen.findByText("Total Agent Executions");
}
function value(label) {
  const term = screen.getAllByText(label).find((element) => element.tagName === "DT");
  return term.nextElementSibling.textContent;
}
function requestRegion() { return within(screen.getByRole("region", { name: "Service Request Overview" })); }
function aiRegion() { return within(screen.getByRole("region", { name: "AssistLK AI Monitoring" })); }

/**
 * TEST SUITE: Admin Dashboard Authoritative Metrics and Aggregations
 *
 * 1. What this class/suite tests:
 *    - Verifies the administrator dashboard landing page (`AdminDashboardPage`), including
 *      independent asynchronous loading of service request and AI workflow metrics,
 *      client-side status and duration aggregations, handling of invalid/corrupted numeric data,
 *      independent error recovery/retry, table preview truncation (first 5 records), and navigation.
 * 2. Why these tests are needed:
 *    - The admin dashboard provides high-level executive visibility over system health and agent
 *      activity. Metrics must be strictly authoritative (derived from genuine backend records,
 *      not hardcoded demo values), resilient to partial API outages, and clean of fabricated stats.
 * 3. What is real and what is mocked/faked:
 *    - Real: React component rendering via React Testing Library, DOM querying, aggregation logic,
 *      and React Router routing transitions.
 *    - Mocked: `adminServiceRequestService.getAll` and `agentMonitoringService.getMetrics` are mocked via Vitest `vi.fn()`.
 * 4. Prerequisites:
 *    - Node.js dependencies installed (`npm install` inside `/web`).
 * 5. PowerShell command to run this suite from repository root:
 *    npm --prefix web test -- src/features/admin/pages/__tests__/AdminDashboardPage.test.jsx
 */
describe("Admin dashboard authoritative metrics", () => {
  /**
   * Purpose: What this specific test checks
   * - Verifies that the service request and AI metrics APIs load independently in parallel,
   *   displaying distinct loading indicators rather than flashing unconfirmed "0" values.
   *
   * Why: Why this scenario matters
   * - Flashing "0" during page load causes layout shift and misleads administrators into thinking
   *   the system has zero activity before the data finishes arriving.
   *
   * Scenario: Input or condition being tested
   * - Both `requestsService.getAll` and `agentService.getMetrics` return pending promises.
   *
   * Pass condition: What result or assertion makes it pass
   * - Both regions show their respective loading statuses, metric labels are not yet rendered,
   *   and each service API is called exactly once.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminDashboardPage.test.jsx -t "loads both APIs independently"
   */
  it("loads both APIs independently without displaying unconfirmed zeros", () => {
    requestsService.getAll.mockReturnValue(new Promise(() => {}));
    agentService.getMetrics.mockReturnValue(new Promise(() => {}));
    renderDashboard();
    expect(requestRegion().getByRole("status")).toHaveTextContent("Loading service request overview...");
    expect(aiRegion().getByRole("status")).toHaveTextContent("Loading AI execution overview...");
    expect(screen.queryByText("Total Service Requests")).not.toBeInTheDocument();
    expect(screen.queryByText("Total Agent Executions")).not.toBeInTheDocument();
    expect(requestsService.getAll).toHaveBeenCalledExactlyOnceWith();
    expect(agentService.getMetrics).toHaveBeenCalledExactlyOnceWith();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies aggregation formulas for request counts by status, agent execution totals,
   *   completion/failure rates, average execution duration, and tool call tallies.
   *
   * Why: Why this scenario matters
   * - Summary tiles must mathematically match the raw collection returned by backend APIs.
   *
   * Scenario: Input or condition being tested
   * - 7 service requests across various statuses (1 Created, 1 Analyzing, 1 AwaitingInformation,
   *   2 Analyzed, 1 ReadyForMatching, 1 Cancelled) and 6 agent executions (5 Completed, 1 Failed, 1200ms total, 3 tools).
   *
   * Pass condition: What result or assertion makes it pass
   * - Each metric definition term (`<dt>`) matches the expected computed value (`<dd>`).
   *
   * Run from repository root (parameterized test - runs all 12 combinations):
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminDashboardPage.test.jsx -t "calculates .* from the complete returned collection"
   */
  it.each([
    ["Total Service Requests", "7"], ["Created", "1"], ["Analyzing", "1"],
    ["Awaiting Information", "1"], ["Analyzed", "2"], ["Ready for Matching", "1"], ["Cancelled", "1"],
    ["Total Agent Executions", "6"], ["Completed Executions", "5"], ["Failed Executions", "1"],
    ["Average Execution Duration", "200 ms"], ["Total Tool Calls", "3"],
  ])("calculates %s from the complete returned collection", async (label, expected) => {
    await loaded();
    expect(value(label)).toBe(expected);
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that request status is strictly authoritative from `request.status` (not guessed from
   *   the presence of `latestAnalysis`), and unrecognized execution statuses are not counted as Completed.
   *
   * Why: Why this scenario matters
   * - Prevents bugs where a request with AI analysis is erroneously tallied as "Analyzed" even if
   *   its actual status is still "Created" or "AwaitingInformation".
   *
   * Scenario: Input or condition being tested
   * - Request with status "Created" but containing `latestAnalysis`, and an execution with status "Other".
   *
   * Pass condition: What result or assertion makes it pass
   * - "Created" is 1, "Analyzed" is 0, and "Completed Executions" is 0.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminDashboardPage.test.jsx -t "does not infer lifecycle from analysis"
   */
  it("does not infer lifecycle from analysis or count unknown statuses as completed", async () => {
    requestsService.getAll.mockResolvedValue([{ ...requests[0], status: "Created", latestAnalysis: { confidence: 1 } }]);
    agentService.getMetrics.mockResolvedValue([{ ...executions[0], status: "Other" }]);
    await loaded();
    expect(value("Created")).toBe("1");
    expect(value("Analyzed")).toBe("0");
    expect(value("Completed Executions")).toBe("0");
    expect(value("Failed Executions")).toBe("0");
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that invalid numeric values (null, undefined, NaN, Infinity, strings, negative numbers)
   *   are excluded when computing average execution duration and total tool calls.
   *
   * Why: Why this scenario matters
   * - Telemetry data from third-party agents or network glitches may include corrupted values;
   *   the dashboard must not display "NaN ms" or crash arithmetic calculations.
   *
   * Scenario: Input or condition being tested
   * - 1 valid execution (1200ms, 3 tool calls) plus 6 invalid entries with null, NaN, Infinity, "600", and -1.
   *
   * Pass condition: What result or assertion makes it pass
   * - Average duration computes from the single valid entry ("1.2 s") and tool calls sum to "3".
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminDashboardPage.test.jsx -t "ignores invalid numeric values in averages and sums"
   */
  it("ignores invalid numeric values in averages and sums", async () => {
    agentService.getMetrics.mockResolvedValue([executions[0], ...[null, undefined, NaN, Infinity, "600", -1].map((number, index) => ({ ...executions[1], id: `invalid-${index}`, executionTimeMs: number, toolCalls: number }))]);
    await loaded();
    expect(value("Average Execution Duration")).toBe("1.2 s");
    expect(value("Total Tool Calls")).toBe("3");
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that zero-data scenarios display genuine zeros, em-dash "—" for undefined averages,
   *   informative empty messages, and omit data tables.
   *
   * Why: Why this scenario matters
   * - Prevents division-by-zero errors (`0 / 0 = NaN`) and confirms empty system states render cleanly.
   *
   * Scenario: Input or condition being tested
   * - Both `requestsService.getAll` and `agentService.getMetrics` return empty arrays `[]`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Counts display "0", Average Execution Duration displays "—", empty state copy is shown,
   *   and no `<table>` elements exist.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminDashboardPage.test.jsx -t "shows real zeros and empty messages with no fabricated average"
   */
  it("shows real zeros and empty messages with no fabricated average", async () => {
    requestsService.getAll.mockResolvedValue([]);
    agentService.getMetrics.mockResolvedValue([]);
    await loaded();
    expect(value("Total Service Requests")).toBe("0");
    expect(value("Total Agent Executions")).toBe("0");
    expect(value("Total Tool Calls")).toBe("0");
    expect(value("Average Execution Duration")).toBe("—");
    expect(screen.getByText("No service requests found.")).toBeInTheDocument();
    expect(screen.getByText("No AI workflow executions have been recorded yet.")).toBeInTheDocument();
    expect(screen.queryByRole("table")).not.toBeInTheDocument();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that the service requests preview table renders at most the first five items
   *   in authoritative backend sort order, displaying ID, category, urgency, and timestamp.
   *
   * Why: Why this scenario matters
   * - Dashboard tables are quick previews, not full lists; capping at 5 prevents dashboard clutter
   *   while maintaining exact chronological ordering.
   *
   * Scenario: Input or condition being tested
   * - Collection of 7 requests loaded; preview table is inspected.
   *
   * Pass condition: What result or assertion makes it pass
   * - Exactly 5 data rows exist; rows 0-4 match indices 0-4; request 5 ("Category 5") is omitted.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminDashboardPage.test.jsx -t "shows the first five authoritative requests in backend order"
   */
  it("shows the first five authoritative requests in backend order", async () => {
    await loaded();
    const rows = within(requestRegion().getByRole("table")).getAllByRole("row").slice(1);
    expect(rows).toHaveLength(5);
    rows.forEach((row, index) => {
      expect(row).toHaveTextContent(`SR-A000000${index}`);
      expect(row).toHaveTextContent(`Category ${index}`);
    });
    expect(rows[0]).toHaveTextContent("High");
    expect(rows[0]).toHaveTextContent(new Date(requests[0].createdAt).toLocaleString());
    expect(screen.queryByText("Category 5")).not.toBeInTheDocument();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that the AI workflow executions preview table renders at most five items,
   *   formatting execution duration, timestamps, status badges, and tool call counts.
   *
   * Why: Why this scenario matters
   * - Provides administrators with immediate visual verification of recent agent executions.
   *
   * Scenario: Input or condition being tested
   * - Collection of 6 executions loaded; AI preview table is inspected.
   *
   * Pass condition: What result or assertion makes it pass
   * - Exactly 5 rows exist; agent names match RecordedAgent0-4; duration "1.2 s" and status "Failed" are rendered.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminDashboardPage.test.jsx -t "shows the first five authoritative AI executions and formats their fields"
   */
  it("shows the first five authoritative AI executions and formats their fields", async () => {
    await loaded();
    const rows = within(aiRegion().getByRole("table")).getAllByRole("row").slice(1);
    expect(rows).toHaveLength(5);
    rows.forEach((row, index) => expect(row).toHaveTextContent(`RecordedAgent${index}`));
    expect(rows[0]).toHaveTextContent("1.2 s");
    expect(rows[0]).toHaveTextContent(new Date(executions[0].createdAtUtc).toLocaleString());
    expect(rows[1]).toHaveTextContent("Failed");
    expect(within(rows[1]).getAllByRole("cell")[3]).toHaveTextContent(/^0$/);
    expect(screen.queryByText("RecordedAgent5")).not.toBeInTheDocument();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that clicking "View All Requests" or "View AI Workflows" buttons triggers client-side
   *   navigation to their respective target routes.
   *
   * Why: Why this scenario matters
   * - The dashboard functions as a navigational springboard to dedicated deep-dive monitoring pages.
   *
   * Scenario: Input or condition being tested
   * - User clicks navigation buttons on the dashboard.
   *
   * Pass condition: What result or assertion makes it pass
   * - Target destination heading ("Request destination" or "Workflow destination") is mounted in the DOM.
   *
   * Run from repository root (parameterized test - runs both button cases):
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminDashboardPage.test.jsx -t "navigates using"
   */
  it.each([["View All Requests", "Request destination"], ["View AI Workflows", "Workflow destination"]])("navigates using %s", async (button, heading) => {
    const user = userEvent.setup();
    await loaded();
    await user.click(screen.getByRole("button", { name: button }));
    expect(screen.getByRole("heading", { name: heading })).toBeInTheDocument();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies error isolation: when the service requests API fails, AI metrics remain visible
   *   and interactive, and clicking "Retry Service Requests" fetches only requests.
   *
   * Why: Why this scenario matters
   * - Microservice independence: an outage in one API endpoint must not bring down the entire admin portal.
   *
   * Scenario: Input or condition being tested
   * - `requestsService.getAll` fails once with "offline" while `agentService.getMetrics` succeeds; user retries requests.
   *
   * Pass condition: What result or assertion makes it pass
   * - Alert shown for requests while AI metrics show 6 executions; retry calls `requestsService.getAll` a second time
   *   without re-invoking `agentService.getMetrics`.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminDashboardPage.test.jsx -t "keeps AI metrics available when requests fail and retries only requests"
   */
  it("keeps AI metrics available when requests fail and retries only requests", async () => {
    requestsService.getAll.mockRejectedValueOnce(new Error("offline"));
    const user = userEvent.setup();
    renderDashboard();
    expect(await screen.findByRole("alert")).toHaveTextContent("Failed to load service requests");
    await screen.findByText("Total Agent Executions");
    expect(value("Total Agent Executions")).toBe("6");
    await user.click(screen.getByRole("button", { name: "Retry Service Requests" }));
    await screen.findByText("Total Service Requests");
    expect(requestsService.getAll).toHaveBeenCalledTimes(2);
    expect(agentService.getMetrics).toHaveBeenCalledTimes(1);
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies error isolation in reverse: when AI metrics fail, service requests remain visible
   *   and clicking "Retry AI Metrics" re-fetches only the AI telemetry.
   *
   * Why: Why this scenario matters
   * - Request monitoring is operational while AI telemetry is analytical; requests must remain accessible.
   *
   * Scenario: Input or condition being tested
   * - `agentService.getMetrics` fails once with "offline" while requests succeed; user retries AI metrics.
   *
   * Pass condition: What result or assertion makes it pass
   * - Alert shown for AI metrics while requests show 7 items; retry calls `agentService.getMetrics` a second time
   *   without re-invoking `requestsService.getAll`.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminDashboardPage.test.jsx -t "keeps requests available when AI fails and retries only AI"
   */
  it("keeps requests available when AI fails and retries only AI", async () => {
    agentService.getMetrics.mockRejectedValueOnce(new Error("offline"));
    const user = userEvent.setup();
    renderDashboard();
    expect(await screen.findByRole("alert")).toHaveTextContent("Failed to load AI execution metrics");
    await screen.findByText("Total Service Requests");
    expect(value("Total Service Requests")).toBe("7");
    await user.click(screen.getByRole("button", { name: "Retry AI Metrics" }));
    await screen.findByText("Total Agent Executions");
    expect(agentService.getMetrics).toHaveBeenCalledTimes(2);
    expect(requestsService.getAll).toHaveBeenCalledTimes(1);
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that ready data renders immediately while the other API is still pending.
   *
   * Why: Why this scenario matters
   * - Fast responses should not be blocked waiting on slower network endpoints.
   *
   * Scenario: Input or condition being tested
   * - Requests API hangs indefinitely while AI metrics resolves immediately.
   *
   * Pass condition: What result or assertion makes it pass
   * - Request region shows loading spinner while AI region successfully displays its table.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminDashboardPage.test.jsx -t "shows ready data while the other API is still loading"
   */
  it("shows ready data while the other API is still loading", async () => {
    requestsService.getAll.mockReturnValue(new Promise(() => {}));
    renderDashboard();
    await screen.findByText("Total Agent Executions");
    expect(requestRegion().getByRole("status")).toBeInTheDocument();
    expect(aiRegion().getByRole("table")).toBeInTheDocument();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that non-array responses (e.g. `{ unexpected: [] }`) trigger an error alert
   *   rather than silently displaying a false "0 service requests found".
   *
   * Why: Why this scenario matters
   * - Backend contract breakages or envelope mutations must be surfaced as errors, not false empty states.
   *
   * Scenario: Input or condition being tested
   * - `requestsService.getAll` resolves with an unexpected object format.
   *
   * Pass condition: What result or assertion makes it pass
   * - Alert banner is shown and "No service requests found" is not displayed.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminDashboardPage.test.jsx -t "reports malformed collections as errors rather than false empty results"
   */
  it("reports malformed collections as errors rather than false empty results", async () => {
    requestsService.getAll.mockResolvedValue({ unexpected: [] });
    renderDashboard();
    expect(await screen.findByRole("alert")).toBeInTheDocument();
    expect(screen.queryByText("No service requests found.")).not.toBeInTheDocument();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that placeholder demo stats, dummy charts, and fake providers are completely absent from the DOM.
   *
   * Why: Why this scenario matters
   * - Production-grade dashboards must present genuine operational data without prototype artifacts.
   *
   * Scenario: Input or condition being tested
   * - Inspect DOM for regex matches of demo keywords, fake charts, or SVG placeholders.
   *
   * Pass condition: What result or assertion makes it pass
   * - No matching demo text found, and no SVG charts rendered.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminDashboardPage.test.jsx -t "removes demo metrics, banner, pipeline and fabricated chart"
   */
  it("removes demo metrics, banner, pipeline and fabricated chart", async () => {
    await loaded();
    expect(document.body).not.toHaveTextContent(/Demo|Demonstration|Verified Providers|Pending Approvals|Sample Service Requests|Volume Trend|SR-2048|18.7%/i);
    expect(document.querySelector("svg")).toBeNull();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that the "Component Boundaries" section is presented explicitly as an architectural reference,
   *   disclaiming real-time operational status.
   *
   * Why: Why this scenario matters
   * - Clarifies architectural scope for examiners and prevents confusing architectural diagrams with live heartbeat monitoring.
   *
   * Scenario: Input or condition being tested
   * - Inspect text content of the Component Boundaries section.
   *
   * Pass condition: What result or assertion makes it pass
   * - Text "Component Boundaries — architecture reference" and "does not report operational status" are present,
   *   while fake statuses "Operational" and "Ready for Integration" are absent.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminDashboardPage.test.jsx -t "keeps Component Boundaries as architecture only"
   */
  it("keeps Component Boundaries as architecture only", async () => {
    await loaded();
    expect(screen.getByText("Component Boundaries — architecture reference")).toBeInTheDocument();
    expect(screen.getByText(/does not report operational status/)).toBeInTheDocument();
    expect(screen.queryByText("Operational")).not.toBeInTheDocument();
    expect(screen.queryByText("Ready for Integration")).not.toBeInTheDocument();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that raw telemetry details (e.g. Gemini, degraded flags, private failure summaries)
   *   are sanitized and only navigation buttons are rendered.
   *
   * Why: Why this scenario matters
   * - Sensitive provider details or internal error logs must not leak to top-level overview dashboards.
   *
   * Scenario: Input or condition being tested
   * - Metric returned with `provider: "Gemini"`, `degraded: true`, and `errorSummary: "private failure"`.
   *
   * Pass condition: What result or assertion makes it pass
   * - DOM contains no matches for sensitive keywords, and only ["View All Requests", "View AI Workflows"] buttons exist.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/admin/pages/__tests__/AdminDashboardPage.test.jsx -t "renders no unsupported telemetry or mutation controls"
   */
  it("renders no unsupported telemetry or mutation controls", async () => {
    agentService.getMetrics.mockResolvedValue([{ ...executions[0], provider: "Gemini", degraded: true, errorSummary: "private failure" }]);
    await loaded();
    expect(document.body).not.toHaveTextContent(/Gemini|degraded|private failure/);
    expect(screen.getAllByRole("button").map((button) => button.textContent)).toEqual(["View All Requests", "View AI Workflows"]);
  });
});

