import AppButton from "../../../shared/components/AppButton";
import AppCard from "../../../shared/components/AppCard";
import StatusBadge from "../../../shared/components/StatusBadge";
import { typography } from "../../../shared/theme";

function formatCreatedDate(value) {
  if (!value) return "—";

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "—" : date.toLocaleString();
}

export default function ComplaintsTable({ complaints, pendingAction, onAction }) {
  return (
    <AppCard className="table-shell">
      <h2 style={typography.cardHeading}>Complaints</h2>
      {complaints.length === 0 ? <p>No complaints found.</p> : (
        <table className="dashboard-table">
          <caption>Customer complaints from tracked service jobs</caption>
          <thead>
            <tr>
              {["Ticket ID", "Job ID", "Customer", "Type", "Description", "AI Sentiment", "Status", "Created", "Actions"].map((label) => (
                <th scope="col" key={label}>{label}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {complaints.map((complaint) => (
              <tr key={complaint.id}>
                <td data-label="Ticket ID">{complaint.ticketId || complaint.id || "—"}</td>
                <td data-label="Job ID">{complaint.jobId || complaint.serviceJobId || "—"}</td>
                <td data-label="Customer">{complaint.customerName || complaint.customerId || "—"}</td>
                <td data-label="Type">{complaint.type || "—"}</td>
                <td data-label="Description">{complaint.customerComment || complaint.description || "—"}</td>
                <td data-label="AI Sentiment">{complaint.aiSentiment || "—"}</td>
                <td data-label="Status"><StatusBadge status={complaint.status} /></td>
                <td data-label="Created">{formatCreatedDate(complaint.createdAt)}</td>
                <td data-label="Actions">
                  {complaint.isSuspicious && complaint.serviceJobId ? (
                    <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
                      {["Approve", "Reject", "Resolve"].map((action) => {
                        const actionKey = `${complaint.id}:${action}`;
                        return (
                          <AppButton
                            key={action}
                            variant={action === "Approve" ? "secondary" : "outline"}
                            disabled={Boolean(pendingAction)}
                            onClick={() => onAction(complaint, action)}
                          >
                            {pendingAction === actionKey ? "Processing..." : action}
                          </AppButton>
                        );
                      })}
                    </div>
                  ) : "—"}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </AppCard>
  );
}
