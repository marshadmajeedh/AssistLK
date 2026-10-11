import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import serviceRequestService from "../services/serviceRequestService";
import ServiceRequestCard from "../components/ServiceRequestCard";
import AppButton from "../../../shared/components/AppButton";
import AppCard from "../../../shared/components/AppCard";
import ErrorMessage from "../../../shared/components/ErrorMessage";
import LoadingSpinner from "../../../shared/components/LoadingSpinner";
import getApiErrorMessage from "../utils/getApiErrorMessage";
import { colors, spacing, typography } from "../../../shared/theme";

function ServiceRequestListPage() {
  const navigate = useNavigate();
  const [requests, setRequests] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let isMounted = true;

    async function fetchRequests() {
      setLoading(true);
      setError(null);

      try {
        const response = await serviceRequestService.getMyRequests();

        if (!isMounted) return;

        // Safely normalize unexpected response shapes without crashing
        let normalizedList = [];
        if (Array.isArray(response)) {
          normalizedList = response;
        } else if (response && Array.isArray(response.data)) {
          normalizedList = response.data;
        } else if (response && Array.isArray(response.items)) {
          normalizedList = response.items;
        } else {
          normalizedList = [];
        }

        setRequests(normalizedList);
      } catch (err) {
        if (!isMounted) return;
        const message = getApiErrorMessage(
          err,
          "Failed to load service requests. Please try again."
        );
        setError(message);
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    }

    fetchRequests();

    return () => {
      isMounted = false;
    };
  }, []);

  return (
    <div className="feature-stack">
      <section className="page-hero">
        <div className="page-hero-content">
          <div>
            <div className="page-kicker">Customer Workspace</div>
            <h1 className="page-hero-title">Manage every request from first report to provider-ready status.</h1>
            <p className="page-hero-copy">Review progress, revisit details, and create the next request without leaving the shared workflow.</p>
          </div>
          <div className="page-hero-meta">
            <div className="page-stat">
              <strong>{requests.length}</strong>
              <span>Requests in your current view</span>
            </div>
            <div className="page-stat">
              <strong>{loading ? "..." : error ? "!" : requests.length === 0 ? "0" : "Live"}</strong>
              <span>{loading ? "Loading portal state" : error ? "Needs attention" : "Customer portal ready"}</span>
            </div>
          </div>
        </div>
      </section>

      <div
        className="section-intro"
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "flex-start",
          flexWrap: "wrap",
          gap: spacing.md,
          marginBottom: spacing.lg,
        }}
      >
        <div>
          <h1
            style={{
              ...typography.pageTitle,
              margin: 0,
              color: colors.textPrimary,
            }}
          >
            My Service Requests
          </h1>
          <p
            style={{
              ...typography.body,
              margin: `${spacing.xs}px 0 0 0`,
              color: colors.textSecondary,
            }}
          >
            Track and manage the service requests you have submitted.
          </p>
        </div>

        <AppButton
          type="button"
          onClick={() => navigate("/service-requests/new")}
        >
          Create Request
        </AppButton>
      </div>

      {/* Loading state */}
      {loading && <LoadingSpinner message="Loading your service requests..." />}

      {/* Error state */}
      {!loading && error && (
        <div style={{ marginBottom: spacing.md }}>
          <ErrorMessage message={error} />
        </div>
      )}

      {/* Empty state */}
      {!loading && !error && requests.length === 0 && (
        <AppCard
          className="section-card"
          style={{
            textAlign: "center",
            padding: `${spacing.xl}px ${spacing.lg}px`,
          }}
        >
          <h2
            style={{
              ...typography.sectionHeading,
              margin: 0,
              color: colors.textPrimary,
            }}
          >
            No service requests yet.
          </h2>
          <p
            style={{
              ...typography.body,
              color: colors.textSecondary,
              marginTop: spacing.sm,
              marginBottom: spacing.lg,
            }}
          >
            Create your first request to get started.
          </p>
          <AppButton
            type="button"
            onClick={() => navigate("/service-requests/new")}
          >
            Create Request
          </AppButton>
        </AppCard>
      )}

      {/* Requests list grid */}
      {!loading && !error && requests.length > 0 && (
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))",
            gap: spacing.md,
          }}
        >
          {requests.map((item, index) => {
            const key = item?.serviceRequestId ?? item?.id ?? index;
            return <ServiceRequestCard key={key} request={item} />;
          })}
        </div>
      )}
    </div>
  );
}

export default ServiceRequestListPage;
