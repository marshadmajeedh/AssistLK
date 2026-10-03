import { useEffect, useState } from "react";
import AppButton from "../../../shared/components/AppButton";
import AppCard from "../../../shared/components/AppCard";
import ErrorMessage from "../../../shared/components/ErrorMessage";
import LoadingSpinner from "../../../shared/components/LoadingSpinner";
import StatusBadge from "../../../shared/components/StatusBadge";
import { colors, typography } from "../../../shared/theme";
import ComplaintsTable from "../components/ComplaintsTable";
import serviceTrackingService from "../services/serviceTrackingService";

function getErrorMessage(error, subject = "complaints") {
  if (error.response?.status === 401) {
    return "Your session has expired. Please log in again.";
  }

  if (error.response?.status === 403) {
    return `You do not have permission to view ${subject}.`;
  }

  return `Failed to load ${subject}. Please try again.`;
}

export default function ServiceTrackingPage() {
  const [complaints, setComplaints] = useState([]);
  const [suspiciousJobs, setSuspiciousJobs] = useState([]);
  const [complaintsLoading, setComplaintsLoading] = useState(true);
  const [suspiciousJobsLoading, setSuspiciousJobsLoading] = useState(true);
  const [complaintsError, setComplaintsError] = useState("");
  const [suspiciousJobsError, setSuspiciousJobsError] = useState("");
  const [pendingAction, setPendingAction] = useState("");
  const [attempt, setAttempt] = useState({ complaints: 0, suspiciousJobs: 0 });

  useEffect(() => {
    let ignore = false;

    serviceTrackingService.getComplaints()
      .then((data) => {
        if (!Array.isArray(data)) {
          throw new Error("Invalid complaints response");
        }

        if (!ignore) {
          setComplaints(data);
          setComplaintsLoading(false);
        }
      })
      .catch((requestError) => {
        if (!ignore) {
          setComplaints([]);
          setComplaintsError(getErrorMessage(requestError, "complaints"));
          setComplaintsLoading(false);
        }
      });

    serviceTrackingService.getSuspiciousJobs()
      .then((data) => {
        if (!Array.isArray(data)) {
          throw new Error("Invalid suspicious jobs response");
        }

        if (!ignore) {
          setSuspiciousJobs(data);
          setSuspiciousJobsLoading(false);
        }
      })
      .catch((requestError) => {
        if (!ignore) {
          setSuspiciousJobs([]);
          setSuspiciousJobsError(getErrorMessage(requestError, "suspicious jobs"));
          setSuspiciousJobsLoading(false);
        }
      });

    return () => {
      ignore = true;
    };
  }, [attempt.complaints, attempt.suspiciousJobs]);

  const retryComplaints = () => {
    setComplaintsLoading(true);
    setComplaintsError("");
    setAttempt((value) => ({ ...value, complaints: value.complaints + 1 }));
  };

  const retrySuspiciousJobs = () => {
    setSuspiciousJobsLoading(true);
    setSuspiciousJobsError("");
    setAttempt((value) => ({ ...value, suspiciousJobs: value.suspiciousJobs + 1 }));
  };

  const handleAction = async (complaint, action) => {
    const ticketId = complaint.ticketId || complaint.id;
    const actionKey = `${ticketId}:${action}`;
    setPendingAction(actionKey);
    setComplaintsError("");

    try {
      const statusByAction = {
        Approve: "Approved",
        Reject: "Rejected",
        Resolve: "Resolved",
      };
      await serviceTrackingService.updateComplaintStatus(ticketId, {
        status: statusByAction[action],
        notes: `${action}d by administrator`,
      });

      setComplaints((currentComplaints) => currentComplaints.map((item) => (
        item.id === complaint.id
          ? { ...item, status: statusByAction[action] }
          : item
      )));
    } catch (error) {
      console.error("Failed to process complaint action", {
        action,
        ticketId,
        error,
      });
      setComplaintsError(
        error.response?.data?.message ||
          error.message ||
          "Failed to process action. Please try again.",
      );
    } finally {
      setPendingAction("");
    }
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
              <strong>{complaintsLoading ? "—" : complaints.length}</strong>
              <span>Complaints loaded</span>
            </div>
          </div>
        </div>
      </section>

      {complaintsLoading ? <LoadingSpinner message="Loading complaints..." /> : complaintsError ? (
        <div>
          <ErrorMessage message={complaintsError} />
          <AppButton variant="outline" onClick={retryComplaints}>Retry Complaints</AppButton>
        </div>
      ) : (
        <ComplaintsTable
          complaints={complaints}
          pendingAction={pendingAction}
          onAction={handleAction}
        />
      )}

      <AppCard className="table-shell">
        <h2 style={typography.cardHeading}>Rapid Completion Flags</h2>
        {suspiciousJobsLoading ? <LoadingSpinner message="Loading suspicious jobs..." /> : suspiciousJobsError ? (
          <div>
            <ErrorMessage message={suspiciousJobsError} />
            <AppButton variant="outline" onClick={retrySuspiciousJobs}>Retry Suspicious Jobs</AppButton>
          </div>
        ) : suspiciousJobs.length === 0 ? <p>No suspicious jobs found.</p> : (
          <table className="dashboard-table">
            <caption>Jobs flagged by Agent 4 for rapid completion review</caption>
            <thead>
              <tr>
                {['Job ID', 'Status', 'Reason', 'Flagged'].map((label) => (
                  <th scope="col" key={label}>{label}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {suspiciousJobs.map((job) => (
                <tr key={job.serviceJobId}>
                  <td data-label="Job ID">{job.serviceJobId || "—"}</td>
                  <td data-label="Status"><StatusBadge status={job.status} /></td>
                  <td data-label="Reason">{job.reason || "Rapid completion flagged for review."}</td>
                  <td data-label="Flagged">{job.flaggedAt ? new Date(job.flaggedAt).toLocaleString() : "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </AppCard>
    </div>
  );
}
