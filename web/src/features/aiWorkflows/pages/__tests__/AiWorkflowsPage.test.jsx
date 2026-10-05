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

describe("AI workflow monitoring", () => {
  it("shows loading without fabricated summary values", () => {
    service.getMetrics.mockReturnValue(new Promise(() => {}));
    render(<AiWorkflowsPage />);
    expect(screen.getByRole("status")).toHaveTextContent("Loading AI workflow metrics...");
    expect(screen.queryByText("Total Executions")).not.toBeInTheDocument();
    expect(screen.queryByRole("table")).not.toBeInTheDocument();
  });
  it("shows an API error and retries", async () => {
    service.getMetrics.mockRejectedValueOnce(new Error("offline"));
    const user = userEvent.setup();
    render(<AiWorkflowsPage />);
    expect(await screen.findByRole("alert")).toHaveTextContent("Failed to load AI workflow metrics. Please try again.");
    await user.click(screen.getByRole("button", { name: "Retry" }));
    expect(await screen.findByRole("table")).toBeInTheDocument();
    expect(service.getMetrics).toHaveBeenCalledTimes(2);
  });
  it.each([[401, "Your session has expired"], [403, "You do not have permission"]])("handles HTTP %s errors", async (status, message) => {
    service.getMetrics.mockRejectedValue({ response: { status } });
    render(<AiWorkflowsPage />);
    expect(await screen.findByRole("alert")).toHaveTextContent(message);
  });
  it("handles an empty response with zero counts and no invented duration", async () => {
    service.getMetrics.mockResolvedValue([]);
    await renderLoaded();
    expect(screen.getByText("No AI workflow executions have been recorded yet.")).toBeInTheDocument();
    for (const label of ["Total Executions", "Successful Executions", "Failed Executions", "Total Tool Calls"]) expect(summaryValue(label)).toHaveTextContent(/^0$/);
    expect(summaryValue("Average Duration")).toHaveTextContent("—");
  });
  it("does not disguise a malformed response as empty telemetry", async () => {
    service.getMetrics.mockResolvedValue({ unexpected: [] });
    render(<AiWorkflowsPage />);
    expect(await screen.findByRole("alert")).toBeInTheDocument();
    expect(screen.queryByText("No AI workflow executions have been recorded yet.")).not.toBeInTheDocument();
  });
  it("renders returned executions in backend order with full accessible IDs", async () => {
    await renderLoaded();
    expect(rows()).toHaveLength(3);
    expect(rows()[0]).toHaveTextContent("11111111");
    expect(rows()[1]).toHaveTextContent("bbbbbbbb");
    expect(rows()[2]).toHaveTextContent("RecordedTestAgent");
    expect(screen.getByLabelText(metrics[0].workflowId)).toHaveAttribute("title", metrics[0].workflowId);
    expect(screen.getByLabelText(metrics[0].executionId)).toHaveAttribute("title", metrics[0].executionId);
  });
  it.each([["Total Executions", "3"], ["Successful Executions", "2"], ["Failed Executions", "1"], ["Average Duration", "600 ms"], ["Total Tool Calls", "5"]])("calculates %s from returned records", async (label, value) => {
    await renderLoaded();
    expect(summaryValue(label).textContent).toBe(value);
  });
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
  it("renders Completed and Failed with distinct theme badges", async () => {
    await renderLoaded();
    expect(within(rows()[0]).getByText("Completed")).toHaveStyle({ color: colors.success, backgroundColor: colors.successLight });
    expect(within(rows()[1]).getByText("Failed")).toHaveStyle({ color: colors.error, backgroundColor: colors.errorLight });
  });
  it("preserves unrecognized statuses without counting them as success or failure", async () => {
    service.getMetrics.mockResolvedValue([{ ...metrics[0], status: "RecordedOtherStatus" }]);
    await renderLoaded();
    expect(within(rows()[0]).getByText("RecordedOtherStatus")).toBeInTheDocument();
    expect(summaryValue("Successful Executions")).toHaveTextContent(/^0$/);
    expect(summaryValue("Failed Executions")).toHaveTextContent(/^0$/);
  });
  it("handles null executionId and zero tool calls", async () => {
    await renderLoaded();
    const cells = within(rows()[1]).getAllByRole("cell");
    expect(cells[0]).toHaveTextContent("—");
    expect(cells[5]).toHaveTextContent(/^0$/);
  });
  it("formats milliseconds, seconds and local recorded timestamps", async () => {
    await renderLoaded();
    expect(rows()[0]).toHaveTextContent("145 ms");
    expect(rows()[1]).toHaveTextContent("1.2 s");
    expect(rows()[0]).toHaveTextContent(new Date(metrics[0].createdAtUtc).toLocaleString());
  });
  it("handles zero duration and invalid timestamps safely", async () => {
    service.getMetrics.mockResolvedValue([{ ...metrics[0], executionTimeMs: 0, createdAtUtc: "invalid" }]);
    await renderLoaded();
    expect(summaryValue("Average Duration")).toHaveTextContent("0 ms");
    expect(within(rows()[0]).getAllByRole("cell")[6]).toHaveTextContent("—");
  });
  it("filters by status locally while summary retains all-record totals", async () => {
    const user = userEvent.setup();
    await renderLoaded();
    await user.selectOptions(screen.getByLabelText("Status"), "Failed");
    expect(rows()).toHaveLength(1);
    expect(rows()[0]).toHaveTextContent("Failed");
    expect(summaryValue("Total Executions")).toHaveTextContent(/^3$/);
    expect(service.getMetrics).toHaveBeenCalledTimes(1);
  });
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
  it("does not expose unsupported metadata even if extra fields are supplied", async () => {
    service.getMetrics.mockResolvedValue([{ ...metrics[0], provider: "Gemini", degraded: true, errorSummary: "private error", prompt: "private prompt", memory: "private memory", startedAt: "private start", workflow: { provider: "OpenAI" } }]);
    await renderLoaded();
    expect(document.body).not.toHaveTextContent(/Gemini|OpenAI|degraded|private error|private prompt|private memory|private start/);
  });
  it("contains no mutation controls and explains measured telemetry", async () => {
    await renderLoaded();
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
    expect(screen.queryByRole("textbox")).not.toBeInTheDocument();
    expect(screen.getByText(/Tool Calls represent deterministic tools/)).toHaveTextContent("Duration is the backend-measured end-to-end execution time.");
  });
});
