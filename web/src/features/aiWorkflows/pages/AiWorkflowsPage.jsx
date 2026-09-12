import { useEffect, useState } from "react";
import AppCard from "../../../shared/components/AppCard";
import AppButton from "../../../shared/components/AppButton";
import LoadingSpinner from "../../../shared/components/LoadingSpinner";
import ErrorMessage from "../../../shared/components/ErrorMessage";
import StatusBadge from "../../../shared/components/StatusBadge";
import { colors, spacing, typography, inputStyles } from "../../../shared/theme";
import agentMonitoringService from "../services/agentMonitoringService";
import { formatDuration, formatRecordedAt, isValidMetricNumber, summarizeMetrics } from "../utils/metricFormatters";
import "./AiWorkflowsPage.css";

function Identifier({ value }) {
  return value ? <span title={value} aria-label={value}>{value.slice(0, 8)}</span> : "—";
}

function ExecutionStatus({ status }) {
  const style = status === "Completed" ? { color: colors.success, backgroundColor: colors.successLight }
    : status === "Failed" ? { color: colors.error, backgroundColor: colors.errorLight } : {};
  return <StatusBadge status={status} style={{ ...style, whiteSpace: "normal" }} />;
}

export default function AiWorkflowsPage() {
  const [result, setResult] = useState({ loading: true, data: [], error: "" });
  const [attempt, setAttempt] = useState(0);
  const [status, setStatus] = useState("");
  const [agent, setAgent] = useState("");

  useEffect(() => {
    let ignore = false;
    agentMonitoringService.getMetrics().then((data) => {
      if (!Array.isArray(data)) throw new Error("Invalid monitoring response");
      if (!ignore) setResult({ loading: false, data, error: "" });
    }).catch((error) => {
      const message = error.response?.status === 401 ? "Your session has expired. Please log in again."
        : error.response?.status === 403 ? "You do not have permission to view AI workflow metrics."
          : "Failed to load AI workflow metrics. Please try again.";
      if (!ignore) setResult({ loading: false, data: [], error: message });
    });
    return () => { ignore = true; };
  }, [attempt]);

  const summary = summarizeMetrics(result.data);
  const agents = [...new Set(result.data.map((metric) => metric.agentName).filter(Boolean))].sort();
  const filtered = result.data.filter((metric) => (!status || metric.status === status) && (!agent || metric.agentName === agent));
  const cards = [
    ["Total Executions", summary.total],
    ["Successful Executions", summary.completed],
    ["Failed Executions", summary.failed],
    ["Average Duration", formatDuration(summary.averageDuration)],
    ["Total Tool Calls", summary.toolCalls],
  ];

  return <div className="ai-workflows" style={{ ...typography.body, color: colors.textPrimary,
    "--workflow-border": colors.border, "--workflow-muted": colors.textSecondary,
    "--workflow-focus": colors.primary, "--workflow-gap": `${spacing.md}px` }}>
    <header>
      <h1 style={{ ...typography.pageTitle, color: colors.textPrimary }}>AI Workflow Monitoring</h1>
      <p className="workflow-muted">Monitor AssistLK AI agent executions, workflow outcomes, deterministic tool usage, and execution performance.</p>
    </header>
    {result.loading ? <LoadingSpinner message="Loading AI workflow metrics..." /> : result.error ? <div>
      <ErrorMessage message={result.error} />
      <AppButton variant="outline" onClick={() => { setResult({ loading: true, data: [], error: "" }); setAttempt((value) => value + 1); }}>Retry</AppButton>
    </div> : <>
      <section aria-labelledby="workflow-summary-heading">
        <h2 id="workflow-summary-heading" style={typography.sectionHeading}>Execution summary</h2>
        <p className="workflow-muted">Based on all returned records. History filters do not change these totals.</p>
        <div className="workflow-summary">
          {cards.map(([label, value]) => <AppCard key={label}>
            <dl><dt>{label}</dt><dd style={typography.pageTitle}>{value}</dd></dl>
          </AppCard>)}
        </div>
      </section>
      {result.data.length === 0 ? <AppCard><p>No AI workflow executions have been recorded yet.</p></AppCard> : <section aria-labelledby="workflow-history-heading">
        <h2 id="workflow-history-heading" style={typography.sectionHeading}>Execution history</h2>
        <AppCard>
          <div className="workflow-filters">
            <label htmlFor="workflow-status">Status
              <select id="workflow-status" value={status} style={inputStyles} onChange={(event) => setStatus(event.target.value)}>
                <option value="">All</option><option>Completed</option><option>Failed</option>
              </select>
            </label>
            <label htmlFor="workflow-agent">Agent
              <select id="workflow-agent" value={agent} style={inputStyles} onChange={(event) => setAgent(event.target.value)}>
                <option value="">All</option>{agents.map((name) => <option key={name}>{name}</option>)}
              </select>
            </label>
            {(status || agent) && <AppButton variant="outline" onClick={() => { setStatus(""); setAgent(""); }}>Clear Filters</AppButton>}
          </div>
          {filtered.length === 0 ? <p>No executions match the selected filters.</p> : <table className="workflow-table">
            <caption>{filtered.length} of {result.data.length} recorded executions · newest first</caption>
            <thead><tr>{["Execution", "Workflow", "Agent", "Status", "Duration", "Tool Calls", "Recorded At"].map((label) => <th scope="col" key={label}>{label}</th>)}</tr></thead>
            <tbody>{filtered.map((metric) => <tr key={metric.id}>
              <td data-label="Execution"><Identifier value={metric.executionId} /></td>
              <td data-label="Workflow"><Identifier value={metric.workflowId} /></td>
              <td data-label="Agent">{metric.agentName || "—"}</td>
              <td data-label="Status"><ExecutionStatus status={metric.status} /></td>
              <td data-label="Duration">{formatDuration(metric.executionTimeMs)}</td>
              <td data-label="Tool Calls">{isValidMetricNumber(metric.toolCalls) ? metric.toolCalls : "—"}</td>
              <td data-label="Recorded At">{formatRecordedAt(metric.createdAtUtc)}</td>
            </tr>)}</tbody>
          </table>}
        </AppCard>
      </section>}
    </>}
    <p className="workflow-muted">AssistLK records workflow execution metrics for operational monitoring. Tool Calls represent deterministic tools used during an agent execution. Duration is the backend-measured end-to-end execution time.</p>
  </div>;
}
