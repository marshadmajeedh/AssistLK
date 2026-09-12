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

describe("Admin dashboard authoritative metrics", () => {
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
  it.each([
    ["Total Service Requests", "7"], ["Created", "1"], ["Analyzing", "1"],
    ["Awaiting Information", "1"], ["Analyzed", "2"], ["Ready for Matching", "1"], ["Cancelled", "1"],
    ["Total Agent Executions", "6"], ["Completed Executions", "5"], ["Failed Executions", "1"],
    ["Average Execution Duration", "200 ms"], ["Total Tool Calls", "3"],
  ])("calculates %s from the complete returned collection", async (label, expected) => {
    await loaded();
    expect(value(label)).toBe(expected);
  });
  it("does not infer lifecycle from analysis or count unknown statuses as completed", async () => {
    requestsService.getAll.mockResolvedValue([{ ...requests[0], status: "Created", latestAnalysis: { confidence: 1 } }]);
    agentService.getMetrics.mockResolvedValue([{ ...executions[0], status: "Other" }]);
    await loaded();
    expect(value("Created")).toBe("1");
    expect(value("Analyzed")).toBe("0");
    expect(value("Completed Executions")).toBe("0");
    expect(value("Failed Executions")).toBe("0");
  });
  it("ignores invalid numeric values in averages and sums", async () => {
    agentService.getMetrics.mockResolvedValue([executions[0], ...[null, undefined, NaN, Infinity, "600", -1].map((number, index) => ({ ...executions[1], id: `invalid-${index}`, executionTimeMs: number, toolCalls: number }))]);
    await loaded();
    expect(value("Average Execution Duration")).toBe("1.2 s");
    expect(value("Total Tool Calls")).toBe("3");
  });
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
  it.each([["View All Requests", "Request destination"], ["View AI Workflows", "Workflow destination"]])("navigates using %s", async (button, heading) => {
    const user = userEvent.setup();
    await loaded();
    await user.click(screen.getByRole("button", { name: button }));
    expect(screen.getByRole("heading", { name: heading })).toBeInTheDocument();
  });
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
  it("shows ready data while the other API is still loading", async () => {
    requestsService.getAll.mockReturnValue(new Promise(() => {}));
    renderDashboard();
    await screen.findByText("Total Agent Executions");
    expect(requestRegion().getByRole("status")).toBeInTheDocument();
    expect(aiRegion().getByRole("table")).toBeInTheDocument();
  });
  it("reports malformed collections as errors rather than false empty results", async () => {
    requestsService.getAll.mockResolvedValue({ unexpected: [] });
    renderDashboard();
    expect(await screen.findByRole("alert")).toBeInTheDocument();
    expect(screen.queryByText("No service requests found.")).not.toBeInTheDocument();
  });
  it("removes demo metrics, banner, pipeline and fabricated chart", async () => {
    await loaded();
    expect(document.body).not.toHaveTextContent(/Demo|Demonstration|Verified Providers|Pending Approvals|Sample Service Requests|Volume Trend|SR-2048|18.7%/i);
    expect(document.querySelector("svg")).toBeNull();
  });
  it("keeps Component Boundaries as architecture only", async () => {
    await loaded();
    expect(screen.getByText("Component Boundaries — architecture reference")).toBeInTheDocument();
    expect(screen.getByText(/does not report operational status/)).toBeInTheDocument();
    expect(screen.queryByText("Operational")).not.toBeInTheDocument();
    expect(screen.queryByText("Ready for Integration")).not.toBeInTheDocument();
  });
  it("renders no unsupported telemetry or mutation controls", async () => {
    agentService.getMetrics.mockResolvedValue([{ ...executions[0], provider: "Gemini", degraded: true, errorSummary: "private failure" }]);
    await loaded();
    expect(document.body).not.toHaveTextContent(/Gemini|degraded|private failure/);
    expect(screen.getAllByRole("button").map((button) => button.textContent)).toEqual(["View All Requests", "View AI Workflows"]);
  });
});
