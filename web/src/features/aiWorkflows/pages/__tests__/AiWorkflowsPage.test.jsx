import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import AiWorkflowsPage from "../AiWorkflowsPage";
import service from "../../services/agentMonitoringService";
import { colors } from "../../../../shared/theme";

vi.mock("../../services/agentMonitoringService", () => ({ default: { getMetrics: vi.fn() } }));
const metrics = [
  { id: "metric-1", workflowId: "aaaaaaaa-1234-4567-8901-123456789abc", executionId: "11111111-1234-4567-8901-123456789abc", agentName: "ProblemUnderstandingAgent", status: "Completed", executionTimeMs: 145, toolCalls: 3, createdAtUtc: "2026-09-12T10:00:00Z", workflow: null },
  { id: "metric-2", workflowId: "bbbbbbbb-1234-4567-8901-123456789abc", executionId: null, agentName: "ProblemUnderstandingAgent", status: "Failed", executionTimeMs: 1200, toolCalls: 0, createdAtUtc: "2026-09-11T10:00:00Z", workflow: null },
  { id: "metric-3", workflowId: "cccccccc-1234-4567-8901-123456789abc", executionId: "33333333-1234-4567-8901-123456789abc", agentName: "RecordedTestAgent", status: "Completed", executionTimeMs: 455, toolCalls: 2, createdAtUtc: "2026-09-10T10:00:00Z", workflow: null },
];
beforeEach(() => {
  vi.resetAllMocks();
  service.getMetrics.mockResolvedValue(metrics);
});
async function renderLoaded() {
  render(<AiWorkflowsPage />);
  await screen.findByRole("region", { name: "Execution summary" });
}
function summaryValue(label) {
  return within(screen.getByRole("region", { name: "Execution summary" })).getByText(label).nextElementSibling;
}
function rows() {
  return within(screen.getByRole("table")).getAllByRole("row").slice(1);
}

/**
 * TEST SUITE: AI Workflow Monitoring Page
 *
 * 1. What this class/suite tests:
 *    - Verifies the administrator AI workflow monitoring dashboard page (`AiWorkflowsPage`),
 *      including telemetry metric aggregations (total executions, success/failure counts, average duration,
 *      tool calls), client-side filtering by status and agent name, handling of HTTP errors (401, 403),
 *      safe timestamp formatting, theme badges, and data sanitization (omitting private AI prompts/providers).
 * 2. Why these tests are needed:
 *    - Administrators require authoritative transparency into AI agent runs across the platform
 *      without exposing confidential LLM provider internals or crashing on malformed telemetry.
 * 3. What is real and what is mocked/faked:
 *    - Real: Full React component rendering with React Testing Library, local filtering state, DOM queries,
 *      and CSS theme styling evaluations.
 *    - Mocked: `agentMonitoringService.getMetrics` is mocked via Vitest `vi.fn()`.
 * 4. Prerequisites:
 *    - Node.js dependencies installed (`npm install` inside `/web`).
 * 5. PowerShell command to run this suite from repository root:
 *    npm --prefix web test -- src/features/aiWorkflows/pages/__tests__/AiWorkflowsPage.test.jsx
 */
describe("AI workflow monitoring", () => {
  /**
   * Purpose: What this specific test checks
   * - Verifies that the page renders a loading status without flashing unconfirmed summary metrics or empty tables.
   *
   * Why: Why this scenario matters
   * - Prevents visual flicker and ensures users know data is actively being fetched.
   *
   * Scenario: Input or condition being tested
   * - `agentMonitoringService.getMetrics` returns a pending promise.
   *
   * Pass condition: What result or assertion makes it pass
   * - Status role contains "Loading AI workflow metrics...", and summary cards and tables are not in the DOM.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/aiWorkflows/pages/__tests__/AiWorkflowsPage.test.jsx -t "shows loading without fabricated summary values"
   */
  it("shows loading without fabricated summary values", () => {
    service.getMetrics.mockReturnValue(new Promise(() => {}));
    render(<AiWorkflowsPage />);
    expect(screen.getByRole("status")).toHaveTextContent("Loading AI workflow metrics...");
    expect(screen.queryByText("Total Executions")).not.toBeInTheDocument();
    expect(screen.queryByRole("table")).not.toBeInTheDocument();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies error banner presentation when the API call fails, and successful data recovery when Retry is clicked.
   *
   * Why: Why this scenario matters
   * - Users experiencing temporary connectivity glitches should be able to recover with a single click.
   *
   * Scenario: Input or condition being tested
   * - Service rejects once with an Error, then succeeds on retry.
   *
   * Pass condition: What result or assertion makes it pass
   * - Alert banner displays "Failed to load AI workflow metrics. Please try again.", retry button is clicked,
   *   table renders, and service is called twice.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/aiWorkflows/pages/__tests__/AiWorkflowsPage.test.jsx -t "shows an API error and retries"
   */
  it("shows an API error and retries", async () => {
    service.getMetrics.mockRejectedValueOnce(new Error("offline"));
    const user = userEvent.setup();
    render(<AiWorkflowsPage />);
    expect(await screen.findByRole("alert")).toHaveTextContent("Failed to load AI workflow metrics. Please try again.");
    await user.click(screen.getByRole("button", { name: "Retry" }));
    expect(await screen.findByRole("table")).toBeInTheDocument();
    expect(service.getMetrics).toHaveBeenCalledTimes(2);
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies specific user-friendly error messages for HTTP 401 (Unauthorized) and HTTP 403 (Forbidden) statuses.
   *
   * Why: Why this scenario matters
   * - Distinguishes session expiration from permission denials so administrators know whether to re-login or request role elevations.
   *
   * Scenario: Input or condition being tested
   * - API rejects with HTTP 401 and HTTP 403 response errors.
   *
   * Pass condition: What result or assertion makes it pass
   * - Alert displays "Your session has expired" for 401, and "You do not have permission" for 403.
   *
   * Run from repository root (parameterized test - runs both status codes):
   * npm --prefix web test -- src/features/aiWorkflows/pages/__tests__/AiWorkflowsPage.test.jsx -t "handles HTTP .* errors"
   */
  it.each([[401, "Your session has expired"], [403, "You do not have permission"]])("handles HTTP %s errors", async (status, message) => {
    service.getMetrics.mockRejectedValue({ response: { status } });
    render(<AiWorkflowsPage />);
    expect(await screen.findByRole("alert")).toHaveTextContent(message);
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that an empty metric collection renders 0 counts, em-dash "—" for Average Duration,
   *   and an empty state explanatory message.
   *
   * Why: Why this scenario matters
   * - Prevents division-by-zero errors (`0 / 0 = NaN`) and confirms a newly initialized system renders cleanly.
   *
   * Scenario: Input or condition being tested
   * - `agentMonitoringService.getMetrics` resolves with empty array `[]`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Informative empty text is displayed, counts display 0, and Average Duration is "—".
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/aiWorkflows/pages/__tests__/AiWorkflowsPage.test.jsx -t "handles an empty response with zero counts and no invented duration"
   */
  it("handles an empty response with zero counts and no invented duration", async () => {
    service.getMetrics.mockResolvedValue([]);
    await renderLoaded();
    expect(screen.getByText("No AI workflow executions have been recorded yet.")).toBeInTheDocument();
    for (const label of ["Total Executions", "Successful Executions", "Failed Executions", "Total Tool Calls"]) expect(summaryValue(label)).toHaveTextContent(/^0$/);
    expect(summaryValue("Average Duration")).toHaveTextContent("—");
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that non-array responses (e.g. `{ unexpected: [] }`) trigger an error alert
   *   rather than silently masking payload bugs as empty telemetry.
   *
   * Why: Why this scenario matters
   * - Schema regressions or API format mismatches must be flagged immediately as errors.
   *
   * Scenario: Input or condition being tested
   * - Service resolves with an object `{ unexpected: [] }` instead of an array.
   *
   * Pass condition: What result or assertion makes it pass
   * - Alert banner appears; empty state message does not appear.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/aiWorkflows/pages/__tests__/AiWorkflowsPage.test.jsx -t "does not disguise a malformed response as empty telemetry"
   */
  it("does not disguise a malformed response as empty telemetry", async () => {
    service.getMetrics.mockResolvedValue({ unexpected: [] });
    render(<AiWorkflowsPage />);
    expect(await screen.findByRole("alert")).toBeInTheDocument();
    expect(screen.queryByText("No AI workflow executions have been recorded yet.")).not.toBeInTheDocument();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that records are rendered in backend order with truncated display IDs and full UUIDs in accessible tooltips (`title`).
   *
   * Why: Why this scenario matters
   * - Truncated IDs keep tables readable on screen while accessible tooltips preserve full UUIDs for auditing.
   *
   * Scenario: Input or condition being tested
   * - Three execution records loaded with unique workflow and execution IDs.
   *
   * Pass condition: What result or assertion makes it pass
   * - Row texts show first 8 chars ("11111111", "bbbbbbbb") and aria-labels/tooltips contain full 36-char GUIDs.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/aiWorkflows/pages/__tests__/AiWorkflowsPage.test.jsx -t "renders returned executions in backend order with full accessible IDs"
   */
  it("renders returned executions in backend order with full accessible IDs", async () => {
    await renderLoaded();
    expect(rows()).toHaveLength(3);
    expect(rows()[0]).toHaveTextContent("11111111");
    expect(rows()[1]).toHaveTextContent("bbbbbbbb");
    expect(rows()[2]).toHaveTextContent("RecordedTestAgent");
    expect(screen.getByLabelText(metrics[0].workflowId)).toHaveAttribute("title", metrics[0].workflowId);
    expect(screen.getByLabelText(metrics[0].executionId)).toHaveAttribute("title", metrics[0].executionId);
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies aggregation math: 3 total, 2 successful, 1 failed, 600ms average duration ((145 + 1200 + 455) / 3 = 600), and 5 tool calls (3 + 0 + 2).
   *
   * Why: Why this scenario matters
   * - Guarantees executive summary numbers match the exact underlying data records.
   *
   * Scenario: Input or condition being tested
   * - Metric collection with 3 records containing known numerical values.
   *
   * Pass condition: What result or assertion makes it pass
   * - Summary card values match expected mathematical totals.
   *
   * Run from repository root (parameterized test - runs all 5 metrics):
   * npm --prefix web test -- src/features/aiWorkflows/pages/__tests__/AiWorkflowsPage.test.jsx -t "calculates .* from returned records"
   */
  it.each([["Total Executions", "3"], ["Successful Executions", "2"], ["Failed Executions", "1"], ["Average Duration", "600 ms"], ["Total Tool Calls", "5"]])("calculates %s from returned records", async (label, value) => {
    await renderLoaded();
    expect(summaryValue(label).textContent).toBe(value);
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that invalid numbers (null, undefined, strings, NaN, Infinity, negative values)
   *   are excluded from duration averages and tool call sums without corrupting valid data.
   *
   * Why: Why this scenario matters
   * - Guards against malformed agent telemetry crashing metric calculations.
   *
   * Scenario: Input or condition being tested
   * - 1 valid metric record (145ms, 3 tools) plus 6 invalid entries.
   *
   * Pass condition: What result or assertion makes it pass
   * - Average Duration is computed solely from the valid record ("145 ms"), tool calls sum to "3",
   *   and raw "null" text is not rendered in data cells.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/aiWorkflows/pages/__tests__/AiWorkflowsPage.test.jsx -t "ignores invalid numeric metrics without coercing missing values to zero"
   */
  it("ignores invalid numeric metrics without coercing missing values to zero", async () => {
    service.getMetrics.mockResolvedValue([
      metrics[0],
      ...[null, undefined, "200", NaN, Infinity, -1].map((value, index) => ({ ...metrics[1], id: `bad-${index}`, executionTimeMs: value, toolCalls: value })),
    ]);
    await renderLoaded();
    expect(summaryValue("Average Duration")).toHaveTextContent("145 ms");
    expect(summaryValue("Total Tool Calls")).toHaveTextContent(/^3$/);
    expect(rows()[1]).not.toHaveTextContent("null");
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that Completed and Failed statuses are styled with the platform's standardized theme badge colors.
   *
   * Why: Why this scenario matters
   * - Consistent visual hierarchy: green for successful runs and red for failures.
   *
   * Scenario: Input or condition being tested
   * - Completed and Failed rows rendered in the data table.
   *
   * Pass condition: What result or assertion makes it pass
   * - "Completed" badge has `colors.success` and `colors.successLight`; "Failed" badge has `colors.error` and `colors.errorLight`.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/aiWorkflows/pages/__tests__/AiWorkflowsPage.test.jsx -t "renders Completed and Failed with distinct theme badges"
   */
  it("renders Completed and Failed with distinct theme badges", async () => {
    await renderLoaded();
    expect(within(rows()[0]).getByText("Completed")).toHaveStyle({ color: colors.success, backgroundColor: colors.successLight });
    expect(within(rows()[1]).getByText("Failed")).toHaveStyle({ color: colors.error, backgroundColor: colors.errorLight });
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that non-standard or custom statuses (e.g. "RecordedOtherStatus") are displayed as-is
   *   without incorrectly incrementing Success or Failure counters.
   *
   * Why: Why this scenario matters
   * - Prevents miscounting when new agent states are introduced.
   *
   * Scenario: Input or condition being tested
   * - Metric returned with `status: "RecordedOtherStatus"`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Row displays "RecordedOtherStatus", while Successful and Failed counts remain 0.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/aiWorkflows/pages/__tests__/AiWorkflowsPage.test.jsx -t "preserves unrecognized statuses without counting them as success or failure"
   */
  it("preserves unrecognized statuses without counting them as success or failure", async () => {
    service.getMetrics.mockResolvedValue([{ ...metrics[0], status: "RecordedOtherStatus" }]);
    await renderLoaded();
    expect(within(rows()[0]).getByText("RecordedOtherStatus")).toBeInTheDocument();
    expect(summaryValue("Successful Executions")).toHaveTextContent(/^0$/);
    expect(summaryValue("Failed Executions")).toHaveTextContent(/^0$/);
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies graceful fallback rendering for null `executionId` (displays em-dash "—") and zero tool calls.
   *
   * Why: Why this scenario matters
   * - Initial agent initialization steps or unassigned workflows may lack execution IDs.
   *
   * Scenario: Input or condition being tested
   * - Metric record with `executionId: null` and `toolCalls: 0`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Cell 0 shows "—" and Cell 5 shows "0".
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/aiWorkflows/pages/__tests__/AiWorkflowsPage.test.jsx -t "handles null executionId and zero tool calls"
   */
  it("handles null executionId and zero tool calls", async () => {
    await renderLoaded();
    const cells = within(rows()[1]).getAllByRole("cell");
    expect(cells[0]).toHaveTextContent("—");
    expect(cells[5]).toHaveTextContent(/^0$/);
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that execution times are formatted intelligently (milliseconds for < 1s, seconds for >= 1s)
   *   and UTC timestamps are converted to local strings.
   *
   * Why: Why this scenario matters
   * - "145 ms" vs "1.2 s" provides human-friendly readability across fast and slow LLM operations.
   *
   * Scenario: Input or condition being tested
   * - Record 0 has 145ms; Record 1 has 1200ms.
   *
   * Pass condition: What result or assertion makes it pass
   * - Row 0 shows "145 ms" and localized timestamp; Row 1 shows "1.2 s".
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/aiWorkflows/pages/__tests__/AiWorkflowsPage.test.jsx -t "formats milliseconds, seconds and local recorded timestamps"
   */
  it("formats milliseconds, seconds and local recorded timestamps", async () => {
    await renderLoaded();
    expect(rows()[0]).toHaveTextContent("145 ms");
    expect(rows()[1]).toHaveTextContent("1.2 s");
    expect(rows()[0]).toHaveTextContent(new Date(metrics[0].createdAtUtc).toLocaleString());
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that zero duration (0ms) formats as "0 ms" and unparseable timestamps degrade to "—".
   *
   * Why: Why this scenario matters
   * - Prevents "NaN s" or broken date strings when data is partially corrupted.
   *
   * Scenario: Input or condition being tested
   * - Metric with `executionTimeMs: 0` and `createdAtUtc: "invalid"`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Average Duration displays "0 ms" and timestamp cell displays "—".
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/aiWorkflows/pages/__tests__/AiWorkflowsPage.test.jsx -t "handles zero duration and invalid timestamps safely"
   */
  it("handles zero duration and invalid timestamps safely", async () => {
    service.getMetrics.mockResolvedValue([{ ...metrics[0], executionTimeMs: 0, createdAtUtc: "invalid" }]);
    await renderLoaded();
    expect(summaryValue("Average Duration")).toHaveTextContent("0 ms");
    expect(within(rows()[0]).getAllByRole("cell")[6]).toHaveTextContent("—");
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that filtering by status operates client-side on table rows without re-fetching from the server
   *   and without altering the top summary totals.
   *
   * Why: Why this scenario matters
   * - Fast, instant UX for table filtering while preserving platform-wide execution KPIs in the summary header.
   *
   * Scenario: Input or condition being tested
   * - User selects "Failed" from Status dropdown.
   *
   * Pass condition: What result or assertion makes it pass
   * - Table narrows to 1 row showing "Failed", summary "Total Executions" remains 3, and service was called only once.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/aiWorkflows/pages/__tests__/AiWorkflowsPage.test.jsx -t "filters by status locally while summary retains all-record totals"
   */
  it("filters by status locally while summary retains all-record totals", async () => {
    const user = userEvent.setup();
    await renderLoaded();
    await user.selectOptions(screen.getByLabelText("Status"), "Failed");
    expect(rows()).toHaveLength(1);
    expect(rows()[0]).toHaveTextContent("Failed");
    expect(summaryValue("Total Executions")).toHaveTextContent(/^3$/);
    expect(service.getMetrics).toHaveBeenCalledTimes(1);
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies dynamic extraction of distinct agent names into the Agent filter dropdown and client-side filtering.
   *
   * Why: Why this scenario matters
   * - Dropdown dynamically adapts to whichever agents have recorded telemetry without hardcoding agent lists.
   *
   * Scenario: Input or condition being tested
   * - User selects "RecordedTestAgent" from Agent dropdown.
   *
   * Pass condition: What result or assertion makes it pass
   * - Dropdown options count is 3 (All + 2 unique agents), table filters to 1 row, and service is not re-queried.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/aiWorkflows/pages/__tests__/AiWorkflowsPage.test.jsx -t "derives unique agent options and filters by agent locally"
   */
  it("derives unique agent options and filters by agent locally", async () => {
    const user = userEvent.setup();
    await renderLoaded();
    const select = screen.getByLabelText("Agent");
    expect(within(select).getAllByRole("option")).toHaveLength(3);
    await user.selectOptions(select, "RecordedTestAgent");
    expect(rows()).toHaveLength(1);
    expect(rows()[0]).toHaveTextContent("RecordedTestAgent");
    expect(service.getMetrics).toHaveBeenCalledTimes(1);
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies combined multi-filter narrowing, filtered-empty state ("No executions match the selected filters."),
   *   and resetting filters via the "Clear Filters" button.
   *
   * Why: Why this scenario matters
   * - Users who filter into an empty intersection must be given a clear message and a quick reset button.
   *
   * Scenario: Input or condition being tested
   * - User filters by Status "Failed" AND Agent "RecordedTestAgent" (0 matches), then clicks "Clear Filters".
   *
   * Pass condition: What result or assertion makes it pass
   * - Filtered-empty message is displayed; clicking "Clear Filters" restores all 3 rows and resets dropdown values.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/aiWorkflows/pages/__tests__/AiWorkflowsPage.test.jsx -t "combines filters, shows filtered-empty state and clears filters"
   */
  it("combines filters, shows filtered-empty state and clears filters", async () => {
    const user = userEvent.setup();
    await renderLoaded();
    await user.selectOptions(screen.getByLabelText("Status"), "Failed");
    await user.selectOptions(screen.getByLabelText("Agent"), "RecordedTestAgent");
    expect(screen.getByText("No executions match the selected filters.")).toBeInTheDocument();
    expect(screen.queryByText("No AI workflow executions have been recorded yet.")).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Clear Filters" }));
    expect(rows()).toHaveLength(3);
    expect(screen.getByLabelText("Status")).toHaveValue("");
    expect(screen.getByLabelText("Agent")).toHaveValue("");
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that sensitive LLM fields (raw prompts, system prompts, agent memory, provider names)
   *   are sanitized and never rendered in the DOM.
   *
   * Why: Why this scenario matters
   * - Privacy, security, and IP protection: raw model prompts and provider names must not leak to monitoring dashboards.
   *
   * Scenario: Input or condition being tested
   * - Metric loaded with extra fields: provider "Gemini", private error, private prompt, memory, and workflow provider "OpenAI".
   *
   * Pass condition: What result or assertion makes it pass
   * - Document body contains no references to Gemini, OpenAI, private prompt, private memory, etc.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/aiWorkflows/pages/__tests__/AiWorkflowsPage.test.jsx -t "does not expose unsupported metadata even if extra fields are supplied"
   */
  it("does not expose unsupported metadata even if extra fields are supplied", async () => {
    service.getMetrics.mockResolvedValue([{ ...metrics[0], provider: "Gemini", degraded: true, errorSummary: "private error", prompt: "private prompt", memory: "private memory", startedAt: "private start", workflow: { provider: "OpenAI" } }]);
    await renderLoaded();
    expect(document.body).not.toHaveTextContent(/Gemini|OpenAI|degraded|private error|private prompt|private memory|private start/);
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that the monitoring page is strictly read-only (no mutation buttons or text inputs)
   *   and includes explanatory help text for tool calls and durations.
   *
   * Why: Why this scenario matters
   * - Observability views must not allow accidental state mutations and must define what metrics mean.
   *
   * Scenario: Input or condition being tested
   * - Page loaded with default metric records.
   *
   * Pass condition: What result or assertion makes it pass
   * - No `<button>` (when unfiltered) or `<textbox>` elements exist; helper text explaining tool calls and duration is present.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/aiWorkflows/pages/__tests__/AiWorkflowsPage.test.jsx -t "contains no mutation controls and explains measured telemetry"
   */
  it("contains no mutation controls and explains measured telemetry", async () => {
    await renderLoaded();
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
    expect(screen.queryByRole("textbox")).not.toBeInTheDocument();
    expect(screen.getByText(/Tool Calls represent deterministic tools/)).toHaveTextContent("Duration is the backend-measured end-to-end execution time.");
  });
});

