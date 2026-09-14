import { useEffect, useRef, useState } from "react";
import AppButton from "../../../shared/components/AppButton";
import AppCard from "../../../shared/components/AppCard";
import StatusBadge from "../../../shared/components/StatusBadge";
import LoadingSpinner from "../../../shared/components/LoadingSpinner";
import ErrorMessage from "../../../shared/components/ErrorMessage";
import { colors, spacing, typography, inputStyles } from "../../../shared/theme";
import getApiErrorMessage from "../../serviceRequests/utils/getApiErrorMessage";
import adminServiceRequestService from "../services/adminServiceRequestService";
import RequestMonitoringDetails, { UrgencyBadge } from "../components/RequestMonitoringDetails";
import RequestLocation from "../components/RequestLocation";
import { formatDate, formatConfidence } from "../utils/monitoringFormatters";
import "./AdminServiceRequestListPage.css";

const emptyFilters = { status: "", category: "", urgency: "" };
const options = {
  status: ["Created", "Analyzing", "AwaitingInformation", "Analyzed", "ReadyForMatching", "Cancelled"],
  category: ["Plumbing", "Electrical", "Vehicle Repair", "Appliance Repair", "Unclassified"],
  urgency: ["Unknown", "Low", "Medium", "High", "Critical"],
};

export default function AdminServiceRequestListPage() {
  const [filters, setFilters] = useState(emptyFilters);
  const [attempt, setAttempt] = useState(0);
  const [list, setList] = useState({ loading: true, data: [], error: "" });
  const [selection, setSelection] = useState(null);
  const [detail, setDetail] = useState({ data: null, error: "", loading: true });
  const detailHeading = useRef(null);
  const opener = useRef(null);
  const active = Object.values(filters).some(Boolean);

  useEffect(() => {
    let ignore = false;
    adminServiceRequestService.getAll(filters).then((data) => {
      if (!ignore) setList({ data, error: "", loading: false });
    }).catch((error) => {
      if (!ignore) setList({ data: [], error: getApiErrorMessage(error, "Failed to load service requests. Please try again."), loading: false });
    });
    return () => { ignore = true; };
  }, [filters, attempt]);

  useEffect(() => {
    if (!selection) return;
    let ignore = false;
    detailHeading.current?.focus();
    adminServiceRequestService.getById(selection.id).then((data) => {
      if (!ignore) setDetail({ data, error: "", loading: false });
    }).catch((error) => {
      if (!ignore) setDetail({ data: null, error: getApiErrorMessage(error, "Failed to load request details. Please try again."), loading: false });
    });
    return () => { ignore = true; };
  }, [selection]);

  function changeFilters(next) {
    setList({ loading: true, data: [], error: "" });
    setFilters(next);
  }
  function openDetails(id, event) {
    opener.current = event.currentTarget;
    setDetail({ data: null, error: "", loading: true });
    setSelection({ id });
  }

  return <div className="request-monitoring" style={{ ...typography.body, color: colors.textPrimary,
    "--monitoring-border": colors.border, "--monitoring-muted": colors.textSecondary,
    "--monitoring-background": colors.background, "--monitoring-focus": colors.primary, "--monitoring-gap": `${spacing.md}px` }}>
    <section className="page-hero">
      <div className="page-hero-content">
        <div>
          <div className="page-kicker">Admin Monitoring</div>
          <h1 className="page-hero-title">Inspect every request without leaving the operational workflow.</h1>
          <p className="page-hero-copy">Filter platform requests, review the latest analysis state, and open a read-only monitoring view for detailed inspection.</p>
        </div>
        <div className="page-hero-meta">
          <div className="page-stat">
            <strong>{list.data.length}</strong>
            <span>Requests in current result set</span>
          </div>
        </div>
      </div>
    </section>
    <AppCard className="section-card">
      <div className="monitoring-filters">
        {Object.entries(options).map(([key, values]) => <label key={key} htmlFor={`filter-${key}`}>
          {key[0].toUpperCase() + key.slice(1)}
          <select id={`filter-${key}`} style={inputStyles} value={filters[key]} onChange={(event) => changeFilters({ ...filters, [key]: event.target.value })}>
            <option value="">All</option>
            {values.map((value) => <option key={value} value={value}>{value}</option>)}
          </select>
        </label>)}
        {active && <AppButton variant="outline" onClick={() => changeFilters(emptyFilters)}>Clear Filters</AppButton>}
      </div>
    </AppCard>
    {list.loading ? <LoadingSpinner message="Loading service requests..." /> : list.error ? <div>
      <ErrorMessage message={list.error} />
      <AppButton variant="outline" onClick={() => { setList({ loading: true, data: [], error: "" }); setAttempt(attempt + 1); }}>Retry</AppButton>
    </div> : list.data.length === 0 ? <AppCard className="section-card">
      <p>No service requests found.</p>
      {active && <p className="monitoring-muted">No requests match the selected filters.</p>}
    </AppCard> : <AppCard className="table-shell">
      <table className="monitoring-table">
        <caption>{list.data.length} service request{list.data.length === 1 ? "" : "s"}</caption>
        <thead><tr>{["Request", "Category", "Urgency", "Status", "Location", "Confidence", "Created", "Action"].map((label) => <th key={label} scope="col">{label}</th>)}</tr></thead>
        <tbody>{list.data.map((request) => <tr key={request.serviceRequestId}>
          <td data-label="Request">SR-{request.serviceRequestId.replaceAll("-", "").slice(0, 8).toUpperCase()}</td>
          <td data-label="Category">{request.category || "Unclassified"}</td>
          <td data-label="Urgency"><UrgencyBadge urgency={request.urgency} /></td>
          <td data-label="Status"><StatusBadge status={request.status} /></td>
          <td data-label="Location"><RequestLocation locationText={request.locationText} locationSource={request.locationSource} /></td>
          <td data-label="Confidence">{formatConfidence(request.latestAnalysis)}</td>
          <td data-label="Created">{formatDate(request.createdAt)}</td>
          <td data-label="Action"><AppButton variant="outline" onClick={(event) => openDetails(request.serviceRequestId, event)}>View Details</AppButton></td>
        </tr>)}</tbody>
      </table>
    </AppCard>}
    {selection && <section aria-labelledby="request-details-heading">
      <div className="monitoring-detail-header">
        <h2 id="request-details-heading" tabIndex={-1} ref={detailHeading}>Request details</h2>
        <AppButton variant="outline" onClick={() => { setSelection(null); opener.current?.focus(); }}>Close Details</AppButton>
      </div>
      {detail.loading ? <LoadingSpinner message="Loading request details..." /> : detail.error ? <>
        <ErrorMessage message={detail.error} />
        <AppButton variant="outline" onClick={() => { setDetail({ data: null, error: "", loading: true }); setSelection({ ...selection }); }}>Retry Details</AppButton>
      </> : <RequestMonitoringDetails request={detail.data} />}
    </section>}
  </div>;
}
