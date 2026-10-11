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
import { formatConfidence } from "../utils/monitoringFormatters";
import "./AdminServiceRequestListPage.css";

export function CreatedDateTime({ value }) {
  if (!value) return "—";
  const created = new Date(value);
  if (Number.isNaN(created.getTime())) return "—";

  const dateText = created.toLocaleDateString();
  const timeText = created.toLocaleTimeString([], {
    hour: "numeric",
    minute: "2-digit",
    second: "2-digit",
  });

  return (
    <div className="created-date-time">
      <span className="created-date">{dateText}</span>
      <span className="created-time">{timeText}</span>
    </div>
  );
}

const emptyFilters = { status: "", category: "", urgency: "" };
const options = {
  status: ["Created", "Analyzing", "AwaitingInformation", "Analyzed", "ReadyForMatching", "Cancelled"],
  category: ["Plumbing", "Electrical", "Vehicle Repair", "Appliance Repair", "Unclassified"],
  urgency: ["Unknown", "Low", "Medium", "High", "Critical"],
};

export default function AdminServiceRequestListPage({ layoutMode = "auto" }) {
  const [filters, setFilters] = useState(emptyFilters);
  const [attempt, setAttempt] = useState(0);
  const [list, setList] = useState({ loading: true, data: [], error: "" });
  const [selection, setSelection] = useState(null);
  const [detail, setDetail] = useState({ data: null, error: "", loading: true });
  const detailHeading = useRef(null);
  const opener = useRef(null);
  const containerRef = useRef(null);
  const [containerWidth, setContainerWidth] = useState(null);

  useEffect(() => {
    const el = containerRef.current;
    if (!el || typeof ResizeObserver === "undefined") return;

    const ro = new ResizeObserver((entries) => {
      for (const entry of entries) {
        if (entry.contentBoxSize) {
          const size = Array.isArray(entry.contentBoxSize) ? entry.contentBoxSize[0] : entry.contentBoxSize;
          setContainerWidth(size.inlineSize);
        } else if (entry.contentRect) {
          setContainerWidth(entry.contentRect.width);
        }
      }
    });

    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  const isCompact = layoutMode === "card" || (layoutMode === "auto" && containerWidth !== null && containerWidth < 1080);
  const isFilterActive = (val) => typeof val === "string" && val.trim().length > 0 && val.trim().toLowerCase() !== "all";
  const active = Object.values(filters).some(isFilterActive);

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

  return <div ref={containerRef} className={`request-monitoring ${isCompact ? "is-compact" : "is-table"}`} style={{ ...typography.body, color: colors.textPrimary,
    "--monitoring-border": colors.border, "--monitoring-muted": colors.textSecondary,
    "--monitoring-background": colors.background, "--monitoring-focus": colors.primary, "--monitoring-gap": `${spacing.md}px` }}>
    <section className="page-hero">
      <div className="page-hero-content">
        <div>
          <div className="page-kicker">Admin Monitoring</div>
          <h1 className="page-hero-title">Service Request Monitoring</h1>
          <p className="page-hero-copy">Inspect every request without leaving the operational workflow. Filter platform requests, review the latest analysis state, and open a read-only monitoring view for detailed inspection.</p>
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
          <select
            id={`filter-${key}`}
            style={inputStyles}
            value={filters[key] && filters[key].toLowerCase() !== "all" ? filters[key] : ""}
            onChange={(event) => changeFilters({ ...filters, [key]: event.target.value })}
          >
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
    </AppCard> : isCompact ? <div className="monitoring-cards-shell">
      <div className="monitoring-results-bar">
        <span className="monitoring-results-count" role="status">
          {list.data.length} service request{list.data.length === 1 ? "" : "s"}
        </span>
      </div>
      <div className="monitoring-cards-grid" role="region" aria-label="Service requests cards">
        {list.data.map((request) => <article
          key={request.serviceRequestId}
          className="admin-request-card"
          data-testid="admin-request-card"
          aria-labelledby={`card-title-${request.serviceRequestId}`}
        >
          <div className="card-header-row">
            <span
              id={`card-title-${request.serviceRequestId}`}
              className="card-request-id"
              title={request.serviceRequestId}
              aria-label={request.serviceRequestId}
            >
              SR-{request.serviceRequestId.replaceAll("-", "").slice(0, 8).toUpperCase()}
            </span>
            <div className="card-status-badge">
              <StatusBadge status={request.status} />
            </div>
          </div>
          <div className="card-meta-grid">
            <div className="card-meta-item">
              <span className="card-meta-label">Category</span>
              <span className="card-meta-value">{request.category || "Unclassified"}</span>
            </div>
            <div className="card-meta-item">
              <span className="card-meta-label">Urgency</span>
              <div className="card-meta-value"><UrgencyBadge urgency={request.urgency} /></div>
            </div>
            <div className="card-meta-item">
              <span className="card-meta-label">Confidence</span>
              <span className="card-meta-value">{formatConfidence(request.latestAnalysis)}</span>
            </div>
          </div>
          <div className="card-location-row">
            <span className="card-meta-label">Location</span>
            <div className="card-location-content">
              <RequestLocation locationText={request.locationText} locationSource={request.locationSource} />
            </div>
          </div>
          <div className="card-footer-row">
            <div className="card-created-col">
              <span className="card-meta-label">Created</span>
              <div className="card-meta-value card-created-value">
                <CreatedDateTime value={request.createdAt} />
              </div>
            </div>
            <div className="card-action-col">
              <AppButton variant="outline" onClick={(event) => openDetails(request.serviceRequestId, event)}>View Details</AppButton>
            </div>
          </div>
        </article>)}
      </div>
    </div> : <AppCard className="table-shell">
      <table className="monitoring-table">
        <caption>{list.data.length} service request{list.data.length === 1 ? "" : "s"}</caption>
        <colgroup>
          <col className="col-request" />
          <col className="col-category" />
          <col className="col-urgency" />
          <col className="col-status" />
          <col className="col-location" />
          <col className="col-confidence" />
          <col className="col-created" />
          <col className="col-action" />
        </colgroup>
        <thead><tr>{["Request", "Category", "Urgency", "Status", "Location", "Confidence", "Created", "Action"].map((label) => <th key={label} scope="col" className={`th-${label.toLowerCase()}`}>{label}</th>)}</tr></thead>
        <tbody>{list.data.map((request) => <tr key={request.serviceRequestId}>
          <td data-label="Request" className="cell-request"><span className="request-id" title={request.serviceRequestId} aria-label={request.serviceRequestId}>SR-{request.serviceRequestId.replaceAll("-", "").slice(0, 8).toUpperCase()}</span></td>
          <td data-label="Category" className="cell-category">{request.category || "Unclassified"}</td>
          <td data-label="Urgency" className="cell-urgency"><UrgencyBadge urgency={request.urgency} /></td>
          <td data-label="Status" className="cell-status"><StatusBadge status={request.status} /></td>
          <td data-label="Location" className="cell-location"><RequestLocation locationText={request.locationText} locationSource={request.locationSource} /></td>
          <td data-label="Confidence" className="cell-confidence">{formatConfidence(request.latestAnalysis)}</td>
          <td data-label="Created" className="cell-created">
            <CreatedDateTime value={request.createdAt} />
          </td>
          <td data-label="Action" className="cell-action"><AppButton variant="outline" onClick={(event) => openDetails(request.serviceRequestId, event)}>View Details</AppButton></td>
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
