import { useEffect, useMemo, useState } from "react";
import AppButton from "../../../shared/components/AppButton";
import AppCard from "../../../shared/components/AppCard";
import LoadingSpinner from "../../../shared/components/LoadingSpinner";
import ErrorMessage from "../../../shared/components/ErrorMessage";
import { colors, spacing, typography } from "../../../shared/theme";
import apiClient from "../../../shared/api/apiClient";
import getApiErrorMessage from "../../serviceRequests/utils/getApiErrorMessage";
import trackingService from "../services/trackingService";
import "./ServiceTrackingPage.css";

const DASH = "\u2014";

const TABS = [
  { key: "all", label: "All" },
  { key: "active", label: "Active" },
  { key: "completed", label: "Completed" },
];

const statusOf = (job) => (job.status || "").toLowerCase();
const isCompleted = (job) => statusOf(job) === "completed";
const isCancelled = (job) => statusOf(job) === "cancelled";
const isActive = (job) => !isCompleted(job) && !isCancelled(job);

function shortId(prefix, id) {
  if (!id) return DASH;
  return `${prefix}-${String(id).replaceAll("-", "").slice(0, 8).toUpperCase()}`;
}

function formatDateTime(value) {
  if (!value) return DASH;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return DASH;
  return `${date.toLocaleDateString()} ${date.toLocaleTimeString([], {
    hour: "numeric",
    minute: "2-digit",
  })}`;
}

function formatStatus(status) {
  if (!status) return "Unknown";
  return status.replace(/([a-z])([A-Z])/g, "$1 $2");
}

function resolveProofUrl(url) {
  if (!url) return null;
  if (/^(https?:|data:|blob:)/i.test(url)) return url;
  const base = (apiClient.defaults?.baseURL ?? "").replace(/\/$/, "");
  return `${base}${url.startsWith("/") ? "" : "/"}${url}`;
}

function JobStatusPill({ status }) {
  const key = (status || "").toLowerCase();
  const variant =
    key === "completed" ? "is-completed" : key === "cancelled" ? "is-cancelled" : "is-active";
  return <span className={`tracking-pill ${variant}`}>{formatStatus(status)}</span>;
}

function JobCard({ job }) {
  const proofUrl = resolveProofUrl(job.completion?.proofOfWorkImageUrl);

  return (
    <article className="tracking-card" data-testid="tracking-job-card">
      <div className="tracking-card-header">
        <span className="tracking-card-id" title={job.id}>
          {shortId("JOB", job.id)}
        </span>
        <JobStatusPill status={job.status} />
      </div>

      <div className="tracking-meta-grid">
        <div>
          <span className="tracking-label">Service Request</span>
          <span className="tracking-value" title={job.serviceRequestId}>
            {shortId("SR", job.serviceRequestId)}
          </span>
        </div>
        <div>
          <span className="tracking-label">Provider</span>
          <span className="tracking-value" title={job.providerId}>
            {shortId("PRV", job.providerId)}
          </span>
        </div>
        <div>
          <span className="tracking-label">Started</span>
          <span className="tracking-value">{formatDateTime(job.startedAt)}</span>
        </div>
        <div>
          <span className="tracking-label">Completed</span>
          <span className="tracking-value">{formatDateTime(job.completedAt)}</span>
        </div>
      </div>

      <section className="tracking-section" aria-label="Completion proof">
        <h3 className="tracking-section-title">Completion Proof</h3>
        {job.completion ? (
          <>
            {job.completion.summaryNotes && (
              <p className="tracking-text">{job.completion.summaryNotes}</p>
            )}
            {proofUrl ? (
              <a href={proofUrl} target="_blank" rel="noopener noreferrer" className="tracking-proof-link">
                <img
                  src={proofUrl}
                  alt="Proof of work"
                  className="tracking-proof-image"
                  loading="lazy"
                />
                <span>View proof image</span>
              </a>
            ) : (
              <p className="tracking-muted">No proof image uploaded.</p>
            )}
          </>
        ) : (
          <p className="tracking-muted">No completion record yet.</p>
        )}
      </section>

      <section className="tracking-section" aria-label="Customer feedback">
        <h3 className="tracking-section-title">Feedback</h3>
        {job.feedback ? (
          <>
            <p className="tracking-rating">
              {job.feedback.rating}/5 <span aria-hidden="true">{"\u2605"}</span>
            </p>
            {job.feedback.comment && <p className="tracking-text">{job.feedback.comment}</p>}
          </>
        ) : (
          <p className="tracking-muted">No feedback submitted.</p>
        )}
      </section>

      <div className="tracking-card-footer">
        <span className="tracking-label">Complaints</span>
        <span
          className={`tracking-complaints ${job.complaintCount > 0 ? "has-complaints" : ""}`}
        >
          {job.complaintCount ?? 0}
        </span>
      </div>
    </article>
  );
}

export default function ServiceTrackingPage() {
  const [state, setState] = useState({ loading: true, data: [], error: "" });
  const [attempt, setAttempt] = useState(0);
  const [tab, setTab] = useState("all");

  useEffect(() => {
    let ignore = false;
    trackingService
      .getJobs()
      .then((data) => {
        if (!ignore) setState({ loading: false, data: Array.isArray(data) ? data : [], error: "" });
      })
      .catch((error) => {
        if (!ignore) {
          setState({
            loading: false,
            data: [],
            error: getApiErrorMessage(error, "Failed to load service jobs. Please try again."),
          });
        }
      });
    return () => {
      ignore = true;
    };
  }, [attempt]);

  const stats = useMemo(
    () => ({
      active: state.data.filter(isActive).length,
      completed: state.data.filter(isCompleted).length,
      complaints: state.data.reduce((sum, job) => sum + (job.complaintCount ?? 0), 0),
    }),
    [state.data]
  );

  const visibleJobs = useMemo(() => {
    if (tab === "active") return state.data.filter(isActive);
    if (tab === "completed") return state.data.filter(isCompleted);
    return state.data;
  }, [state.data, tab]);

  function retry() {
    setState({ loading: true, data: [], error: "" });
    setAttempt((value) => value + 1);
  }

  return (
    <div
      className="service-tracking"
      style={{ ...typography.body, color: colors.textPrimary, "--tracking-gap": `${spacing.md}px` }}
    >
      <section className="tracking-hero">
        <div className="tracking-kicker">Admin Monitoring</div>
        <h1 className="tracking-title">Service Tracking</h1>
        <p className="tracking-copy">
          Follow every service job from start to finish. Review completion proof, customer feedback
          and complaints in one place.
        </p>
        <div className="tracking-stats">
          <div className="tracking-stat">
            <strong>{stats.active}</strong>
            <span>Active jobs</span>
          </div>
          <div className="tracking-stat">
            <strong>{stats.completed}</strong>
            <span>Completed jobs</span>
          </div>
          <div className="tracking-stat">
            <strong>{stats.complaints}</strong>
            <span>Total complaints</span>
          </div>
        </div>
      </section>

      <AppCard className="tracking-toolbar">
        <div className="tracking-tabs" role="tablist" aria-label="Job status filter">
          {TABS.map((item) => (
            <button
              key={item.key}
              type="button"
              role="tab"
              aria-selected={tab === item.key}
              className={`tracking-tab ${tab === item.key ? "is-selected" : ""}`}
              onClick={() => setTab(item.key)}
            >
              {item.label}
            </button>
          ))}
        </div>
      </AppCard>

      {state.loading ? (
        <LoadingSpinner message="Loading service jobs..." />
      ) : state.error ? (
        <div>
          <ErrorMessage message={state.error} />
          <AppButton variant="outline" onClick={retry}>
            Retry
          </AppButton>
        </div>
      ) : visibleJobs.length === 0 ? (
        <AppCard>
          <p>No service jobs found.</p>
          {tab !== "all" && <p className="tracking-muted">No {tab} jobs at the moment.</p>}
        </AppCard>
      ) : (
        <>
          <span className="tracking-count" role="status">
            {visibleJobs.length} job{visibleJobs.length === 1 ? "" : "s"}
          </span>
          <div className="tracking-grid" role="region" aria-label="Service jobs">
            {visibleJobs.map((job) => (
              <JobCard key={job.id} job={job} />
            ))}
          </div>
        </>
      )}
    </div>
  );
}