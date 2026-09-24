import { useEffect, useState } from "react";
import AppButton from "../../../shared/components/AppButton";
import AppCard from "../../../shared/components/AppCard";
import ErrorMessage from "../../../shared/components/ErrorMessage";
import LoadingSpinner from "../../../shared/components/LoadingSpinner";
import StatusBadge from "../../../shared/components/StatusBadge";
import { colors, typography } from "../../../shared/theme";
import serviceTrackingService from "../services/serviceTrackingService";

function formatCreatedDate(value) {
  if (!value) return "—";

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "—" : date.toLocaleString();
}

function getErrorMessage(error) {
  if (error.response?.status === 401) {
    return "Your session has expired. Please log in again.";
  }

  if (error.response?.status === 403) {
    return "You do not have permission to view complaints.";
  }

  return "Failed to load complaints. Please try again.";
}

export default function ServiceTrackingPage() {
  const [complaints, setComplaints] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let ignore = false;

    serviceTrackingService.getComplaints()
      .then((data) => {
        if (!Array.isArray(data)) {
          throw new Error("Invalid complaints response");
        }

        if (!ignore) {
          setComplaints(data);
          setLoading(false);
        }
      })
      .catch((requestError) => {
        if (!ignore) {
          setComplaints([]);
          setError(getErrorMessage(requestError));
          setLoading(false);
        }
      });

    return () => {
      ignore = true;
    };
  }, [attempt]);

  const retry = () => {
    setLoading(true);
    setError("");
    setAttempt((value) => value + 1);
  };

  return (
    <div className="app-page" style={{ ...typography.body, color: colors.textPrimary }}>
      <section className="page-hero">
        <div className="page-hero-content">
          <div>
            <div className="page-kicker">Component 4</div>
            <h1 className="page-hero-title">Service Tracking & Complaints</h1>
            <p className="page-hero-copy">
              Review customer complaints raised during tracked service jobs.
            </p>
          </div>
          <div className="page-hero-meta">
            <div className="page-stat">
              <strong>{loading ? "—" : complaints.length}</strong>
              <span>Complaints loaded</span>
            </div>
          </div>
        </div>
      </section>

      {loading ? <LoadingSpinner message="Loading complaints..." /> : error ? (
        <div>
          <ErrorMessage message={error} />
          <AppButton variant="outline" onClick={retry}>Retry Complaints</AppButton>
        </div>
      ) : (
        <AppCard className="table-shell">
          <h2 style={typography.cardHeading}>Complaints</h2>
          {complaints.length === 0 ? <p>No complaints found.</p> : (
            <table className="dashboard-table">
              <caption>Customer complaints from tracked service jobs</caption>
              <thead>
                <tr>
                  {["Subject", "Type", "Status", "Description", "Created"].map((label) => (
                    <th scope="col" key={label}>{label}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {complaints.map((complaint) => (
                  <tr key={complaint.id}>
                    <td data-label="Subject">{complaint.subject || "—"}</td>
                    <td data-label="Type">{complaint.type || "—"}</td>
                    <td data-label="Status"><StatusBadge status={complaint.status} /></td>
                    <td data-label="Description">{complaint.description || "—"}</td>
                    <td data-label="Created">{formatCreatedDate(complaint.createdAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </AppCard>
      )}
    </div>
  );
}
