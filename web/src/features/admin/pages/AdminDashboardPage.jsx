import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import AppCard from "../../../shared/components/AppCard";
import AppButton from "../../../shared/components/AppButton";
import StatusBadge from "../../../shared/components/StatusBadge";
import LoadingSpinner from "../../../shared/components/LoadingSpinner";
import ErrorMessage from "../../../shared/components/ErrorMessage";
import { colors, spacing, typography } from "../../../shared/theme";
import adminServiceRequestService from "../services/adminServiceRequestService";
import agentMonitoringService from "../../aiWorkflows/services/agentMonitoringService";
import { formatDuration, formatRecordedAt, isValidMetricNumber, summarizeMetrics } from "../../aiWorkflows/utils/metricFormatters";
import { formatDate } from "../utils/monitoringFormatters";
import { UrgencyBadge } from "../components/RequestMonitoringDetails";
import "./AdminDashboardPage.css";

const lifecycle = [
  ["Created", "Created"], ["Analyzing", "Analyzing"],
  ["AwaitingInformation", "Awaiting Information"], ["Analyzed", "Analyzed"],
  ["ReadyForMatching", "Ready for Matching"], ["Cancelled", "Cancelled"],
];
const boundaries = [
  ["Component 1", "Service Request & Problem Understanding"],
  ["Component 2", "Provider Management & Intelligent Matching"],
  ["Component 3", "Quotation, Booking & Service Coordination"],
  ["Component 4", "Service Tracking, Completion & Safety"],
];

// Independent state and retries keep either API failure from blocking the other.
function useDashboardCollection(load, label) {
  const [result, setResult] = useState({ loading: true, data: [], error: "" });
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    let ignore = false;
    load().then((data) => {
      if (!Array.isArray(data)) throw new Error("Invalid collection response");
      if (!ignore) setResult({ loading: false, data, error: "" });
    }).catch((error) => {
      const message = error.response?.status === 401 ? "Your session has expired. Please log in again."
        : error.response?.status === 403 ? `You do not have permission to view ${label}.`
          : `Failed to load ${label}. Please try again.`;
      if (!ignore) setResult({ loading: false, data: [], error: message });
    });
    return () => { ignore = true; };
  }, [load, label, attempt]);
  return { ...result, retry: () => {
    setResult({ loading: true, data: [], error: "" });
    setAttempt((value) => value + 1);
  } };
}

function Metrics({ cards }) {
  return <div className="dashboard-metrics">{cards.map(([label, value]) => <AppCard key={label}>
    <dl><dt>{label}</dt><dd style={typography.pageTitle}>{value}</dd></dl>
  </AppCard>)}</div>;
}

function CollectionState({ result, loadingMessage, retryLabel, children }) {
  if (result.loading) return <LoadingSpinner message={loadingMessage} />;
  if (result.error) return <div><ErrorMessage message={result.error} /><AppButton variant="outline" onClick={result.retry}>{retryLabel}</AppButton></div>;
  return children;
}

export default function AdminDashboardPage() {
  const navigate = useNavigate();
  const requests = useDashboardCollection(adminServiceRequestService.getAll, "service requests");
  const executions = useDashboardCollection(agentMonitoringService.getMetrics, "AI execution metrics");
  const requestCards = [["Total Service Requests", requests.data.length], ...lifecycle.map(([status, label]) => [label, requests.data.filter((request) => request.status === status).length])];
  const summary = summarizeMetrics(executions.data);
  const agentCards = [["Total Agent Executions", summary.total], ["Completed Executions", summary.completed],
    ["Failed Executions", summary.failed], ["Average Execution Duration", formatDuration(summary.averageDuration)], ["Total Tool Calls", summary.toolCalls]];

  return <div className="admin-dashboard" style={{ ...typography.body, color: colors.textPrimary,
    "--dashboard-gap": `${spacing.md}px`, "--dashboard-muted": colors.textSecondary,
    "--dashboard-border": colors.border, "--dashboard-focus": colors.primary }}>
    <header><h1 style={{ ...typography.pageTitle, color: colors.textPrimary }}>Dashboard</h1>
      <p className="dashboard-muted">Component 1 service request lifecycle and AssistLK AI execution monitoring.</p>
      <p className="dashboard-muted">Metrics reflect all records returned by the monitoring APIs when this page loads.</p>
    </header>
    <section aria-labelledby="dashboard-requests-heading">
      <div className="dashboard-section-header"><h2 id="dashboard-requests-heading" style={typography.sectionHeading}>Service Request Overview</h2>
        <AppButton variant="outline" onClick={() => navigate("/admin/service-requests")}>View All Requests</AppButton>
      </div>
      <CollectionState result={requests} loadingMessage="Loading service request overview..." retryLabel="Retry Service Requests">
        <Metrics cards={requestCards} />
        <AppCard>
          <h3 style={typography.cardHeading}>Recent Service Requests</h3>
          {requests.data.length === 0 ? <p>No service requests found.</p> : <table className="dashboard-table">
            <caption>Latest {Math.min(5, requests.data.length)} service requests</caption>
            <thead><tr>{["Request", "Category", "Urgency", "Status", "Created"].map((label) => <th scope="col" key={label}>{label}</th>)}</tr></thead>
            <tbody>{requests.data.slice(0, 5).map((request) => <tr key={request.serviceRequestId}>
              <td data-label="Request"><span title={request.serviceRequestId} aria-label={request.serviceRequestId}>SR-{request.serviceRequestId.replaceAll("-", "").slice(0, 8).toUpperCase()}</span></td>
              <td data-label="Category">{request.category || "Unclassified"}</td>
              <td data-label="Urgency"><UrgencyBadge urgency={request.urgency} /></td>
              <td data-label="Status"><StatusBadge status={request.status} /></td>
              <td data-label="Created">{formatDate(request.createdAt)}</td>
            </tr>)}</tbody>
          </table>}
        </AppCard>
      </CollectionState>
    </section>
    <section aria-labelledby="dashboard-ai-heading">
      <div className="dashboard-section-header"><h2 id="dashboard-ai-heading" style={typography.sectionHeading}>AssistLK AI Monitoring</h2>
        <AppButton variant="outline" onClick={() => navigate("/ai-workflows")}>View AI Workflows</AppButton>
      </div>
      <CollectionState result={executions} loadingMessage="Loading AI execution overview..." retryLabel="Retry AI Metrics">
        <Metrics cards={agentCards} />
        <AppCard>
          <h3 style={typography.cardHeading}>Recent AI Executions</h3>
          {executions.data.length === 0 ? <p>No AI workflow executions have been recorded yet.</p> : <table className="dashboard-table">
            <caption>Latest {Math.min(5, executions.data.length)} AI executions</caption>
            <thead><tr>{["Agent", "Status", "Duration", "Tool Calls", "Recorded At"].map((label) => <th scope="col" key={label}>{label}</th>)}</tr></thead>
            <tbody>{executions.data.slice(0, 5).map((metric) => <tr key={metric.id}>
              <td data-label="Agent">{metric.agentName || "—"}</td>
              <td data-label="Status"><StatusBadge status={metric.status} style={metric.status === "Completed"
                ? { color: colors.success, backgroundColor: colors.successLight }
                : metric.status === "Failed" ? { color: colors.error, backgroundColor: colors.errorLight } : {}} /></td>
              <td data-label="Duration">{formatDuration(metric.executionTimeMs)}</td>
              <td data-label="Tool Calls">{isValidMetricNumber(metric.toolCalls) ? metric.toolCalls : "—"}</td>
              <td data-label="Recorded At">{formatRecordedAt(metric.createdAtUtc)}</td>
            </tr>)}</tbody>
          </table>}
        </AppCard>
        <p className="dashboard-muted">Tool Calls count deterministic tools. Duration is the backend-measured end-to-end execution time.</p>
      </CollectionState>
    </section>
    <AppCard><details>
      <summary>Component Boundaries — architecture reference</summary>
      <p className="dashboard-muted">Project responsibilities are listed for reference. This section does not report operational status.</p>
      <dl className="dashboard-boundaries">{boundaries.map(([name, scope]) => <div key={name}><dt>{name}</dt><dd>{scope}</dd></div>)}</dl>
    </details></AppCard>
  </div>;
}
