import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import serviceRequestService from "../services/serviceRequestService";
import { canCancelRequest, canEditRequest } from "../utils/serviceRequestStatus";
import getApiErrorMessage from "../utils/getApiErrorMessage";
import AppButton from "../../../shared/components/AppButton";
import AppCard from "../../../shared/components/AppCard";
import StatusBadge from "../../../shared/components/StatusBadge";
import ErrorMessage from "../../../shared/components/ErrorMessage";
import LoadingSpinner from "../../../shared/components/LoadingSpinner";
import { colors, radius, spacing, typography } from "../../../shared/theme";

function formatDateTime(dateString) {
  if (!dateString) {
    return null;
  }

  try {
    const date = new Date(dateString);
    if (Number.isNaN(date.getTime())) {
      return String(dateString);
    }

    return date.toLocaleDateString(undefined, {
      year: "numeric",
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });
  } catch {
    return String(dateString);
  }
}

function formatCoordinate(coord) {
  if (coord === null || coord === undefined) {
    return null;
  }
  const num = Number(coord);
  if (Number.isNaN(num)) {
    return String(coord);
  }
  return num.toFixed(5);
}

function ServiceRequestDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();

  const [request, setRequest] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // Cancellation states
  const [showCancelConfirm, setShowCancelConfirm] = useState(false);
  const [isCancelling, setIsCancelling] = useState(false);
  const [cancelError, setCancelError] = useState(null);

  useEffect(() => {
    let isMounted = true;

    async function fetchRequest() {
      setLoading(true);
      setError(null);

      try {
        const data = await serviceRequestService.getById(id);
        if (isMounted) {
          setRequest(data);
        }
      } catch (err) {
        if (isMounted) {
          const message = getApiErrorMessage(
            err,
            "Failed to load service request details."
          );
          setError(message);
        }
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    }

    if (id) {
      fetchRequest();
    }

    return () => {
      isMounted = false;
    };
  }, [id]);

  const handleCancel = async () => {
    if (isCancelling || !request) return;

    setIsCancelling(true);
    setCancelError(null);

    try {
      const requestId = request.serviceRequestId ?? request.id ?? id;
      const updatedRequest = await serviceRequestService.cancel(requestId);
      setRequest(updatedRequest);
      setShowCancelConfirm(false);
    } catch (err) {
      const message = getApiErrorMessage(
        err,
        "Failed to cancel service request. Please try again."
      );
      setCancelError(message);
    } finally {
      setIsCancelling(false);
    }
  };

  // Loading State
  if (loading) {
    return (
      <div style={{ maxWidth: 760, margin: "0 auto", width: "100%" }}>
        <LoadingSpinner message="Loading service request details..." />
      </div>
    );
  }

  // Error / Not Found State
  if (error || !request) {
    return (
      <div style={{ maxWidth: 760, margin: "0 auto", width: "100%" }}>
        <AppCard>
          <h2
            style={{
              ...typography.sectionHeading,
              margin: 0,
              color: colors.textPrimary,
            }}
          >
            Unable to View Request
          </h2>
          <ErrorMessage
            message={
              error ||
              "The requested service request was not found or you do not have permission to view it."
            }
          />
          <div style={{ marginTop: spacing.lg }}>
            <AppButton
              variant="outline"
              type="button"
              onClick={() => navigate("/service-requests")}
            >
              ← Back to My Requests
            </AppButton>
          </div>
        </AppCard>
      </div>
    );
  }

  const requestId = request.serviceRequestId ?? request.id ?? id;
  const createdDate = formatDateTime(request.createdAt);
  const updatedDate = formatDateTime(request.updatedAt);

  const hasGps =
    request.latitude !== null &&
    request.latitude !== undefined &&
    request.longitude !== null &&
    request.longitude !== undefined;

  const latDisplay = hasGps ? formatCoordinate(request.latitude) : null;
  const lngDisplay = hasGps ? formatCoordinate(request.longitude) : null;

  // Deriving persisted analysis display values directly from the actual API response
  const categoryDisplay = request.category || "Unclassified";
  const urgencyDisplay = request.urgency || "Unknown";
  const problemSummaryDisplay =
    request.problemSummary ||
    request.detectedProblem ||
    (Array.isArray(request.problemAnalyses) &&
      request.problemAnalyses[0]?.detectedProblem) ||
    null;

  const editable = canEditRequest(request.status);
  const cancellable = canCancelRequest(request.status);

  return (
    <div
      style={{
        maxWidth: 760,
        margin: "0 auto",
        width: "100%",
        boxSizing: "border-box",
        display: "flex",
        flexDirection: "column",
        gap: spacing.lg,
      }}
    >
      {/* Top Navigation & Back Action */}
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: spacing.sm,
        }}
      >
        <AppButton
          variant="outline"
          type="button"
          onClick={() => navigate("/service-requests")}
          style={{
            minHeight: 38,
            padding: `0 ${spacing.md}px`,
            fontSize: typography.small.fontSize,
          }}
        >
          ← Back to My Requests
        </AppButton>
      </div>

      {/* Details Page Header Card */}
      <AppCard>
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "flex-start",
            flexWrap: "wrap",
            gap: spacing.md,
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
              Service Request
            </h1>
            <div
              style={{
                ...typography.small,
                color: colors.textSecondary,
                marginTop: spacing.xs,
                wordBreak: "break-all",
              }}
            >
              ID: {requestId}
            </div>
          </div>

          <StatusBadge status={request.status} />
        </div>

        {/* Timestamp metadata */}
        <div
          style={{
            display: "flex",
            flexWrap: "wrap",
            gap: spacing.lg,
            marginTop: spacing.md,
            paddingTop: spacing.md,
            borderTop: `1px solid ${colors.border}`,
          }}
        >
          {createdDate && (
            <div style={{ ...typography.small, color: colors.textSecondary }}>
              <span style={{ fontWeight: 600, color: colors.textPrimary }}>
                Created:
              </span>{" "}
              {createdDate}
            </div>
          )}

          {updatedDate && updatedDate !== createdDate && (
            <div style={{ ...typography.small, color: colors.textSecondary }}>
              <span style={{ fontWeight: 600, color: colors.textPrimary }}>
                Last Updated:
              </span>{" "}
              {updatedDate}
            </div>
          )}
        </div>
      </AppCard>

      {/* Request Information Card */}
      <AppCard>
        <h2
          style={{
            ...typography.cardHeading,
            margin: `0 0 ${spacing.md}px 0`,
            color: colors.textPrimary,
          }}
        >
          Request Information
        </h2>

        <div
          style={{
            display: "flex",
            flexDirection: "column",
            gap: spacing.md,
          }}
        >
          {/* Problem Description */}
          <div>
            <span
              style={{
                ...typography.small,
                fontWeight: 600,
                color: colors.textSecondary,
                display: "block",
                marginBottom: spacing.xs,
              }}
            >
              Problem Description
            </span>
            <p
              style={{
                ...typography.body,
                color: colors.textPrimary,
                margin: 0,
                whiteSpace: "pre-wrap",
                wordBreak: "break-word",
              }}
            >
              {request.description}
            </p>
          </div>

          {/* Location Text */}
          <div>
            <span
              style={{
                ...typography.small,
                fontWeight: 600,
                color: colors.textSecondary,
                display: "block",
                marginBottom: spacing.xs,
              }}
            >
              Location
            </span>
            <p
              style={{
                ...typography.body,
                color: colors.textPrimary,
                margin: 0,
                wordBreak: "break-word",
              }}
            >
              {request.locationText}
            </p>
          </div>

          {/* GPS Coordinates Section */}
          {hasGps && (
            <div
              style={{
                padding: spacing.md,
                backgroundColor: colors.background,
                borderRadius: radius.medium,
                border: `1px solid ${colors.border}`,
              }}
            >
              <div
                style={{
                  ...typography.small,
                  fontWeight: 600,
                  color: colors.primary,
                  marginBottom: spacing.xs,
                }}
              >
                GPS Location Available
              </div>
              <div
                style={{
                  ...typography.small,
                  color: colors.textPrimary,
                  display: "flex",
                  flexDirection: "column",
                  gap: 2,
                }}
              >
                <div>
                  <span style={{ fontWeight: 600 }}>Latitude:</span> {latDisplay}
                </div>
                <div>
                  <span style={{ fontWeight: 600 }}>Longitude:</span> {lngDisplay}
                </div>
              </div>
            </div>
          )}
        </div>
      </AppCard>

      {/* Analysis Information Card */}
      <AppCard>
        <h2
          style={{
            ...typography.cardHeading,
            margin: `0 0 ${spacing.md}px 0`,
            color: colors.textPrimary,
          }}
        >
          Analysis Information
        </h2>

        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))",
            gap: spacing.md,
            padding: spacing.md,
            backgroundColor: colors.background,
            borderRadius: radius.medium,
            border: `1px solid ${colors.border}`,
          }}
        >
          <div>
            <span
              style={{
                ...typography.small,
                fontWeight: 600,
                color: colors.textSecondary,
                display: "block",
                marginBottom: spacing.xs,
              }}
            >
              Category
            </span>
            <span
              style={{
                ...typography.body,
                fontWeight: 500,
                color: colors.textPrimary,
              }}
            >
              {categoryDisplay}
            </span>
          </div>

          <div>
            <span
              style={{
                ...typography.small,
                fontWeight: 600,
                color: colors.textSecondary,
                display: "block",
                marginBottom: spacing.xs,
              }}
            >
              Urgency
            </span>
            <span
              style={{
                ...typography.body,
                fontWeight: 500,
                color: colors.textPrimary,
              }}
            >
              {urgencyDisplay}
            </span>
          </div>

          <div>
            <span
              style={{
                ...typography.small,
                fontWeight: 600,
                color: colors.textSecondary,
                display: "block",
                marginBottom: spacing.xs,
              }}
            >
              Status
            </span>
            <StatusBadge status={request.status} />
          </div>
        </div>

        {/* Display Problem Summary if available from prior analysis */}
        {problemSummaryDisplay && (
          <div style={{ marginTop: spacing.md }}>
            <span
              style={{
                ...typography.small,
                fontWeight: 600,
                color: colors.textSecondary,
                display: "block",
                marginBottom: spacing.xs,
              }}
            >
              Problem Summary
            </span>
            <p
              style={{
                ...typography.body,
                color: colors.textPrimary,
                margin: 0,
              }}
            >
              {problemSummaryDisplay}
            </p>
          </div>
        )}
      </AppCard>

      {/* Actions & Cancellation Card */}
      {(editable || cancellable || showCancelConfirm) && (
        <AppCard>
          <h2
            style={{
              ...typography.cardHeading,
              margin: `0 0 ${spacing.md}px 0`,
              color: colors.textPrimary,
            }}
          >
            Actions
          </h2>

          {/* Cancellation error message */}
          {cancelError && (
            <div style={{ marginBottom: spacing.md }}>
              <ErrorMessage message={cancelError} />
            </div>
          )}

          {/* Inline Cancellation Confirmation */}
          {showCancelConfirm ? (
            <div
              style={{
                padding: spacing.md,
                backgroundColor: colors.background,
                borderRadius: radius.medium,
                border: `1px solid ${colors.error}`,
              }}
            >
              <h3
                style={{
                  ...typography.cardHeading,
                  margin: 0,
                  color: colors.error,
                }}
              >
                Are you sure you want to cancel this service request?
              </h3>
              <p
                style={{
                  ...typography.body,
                  color: colors.textSecondary,
                  marginTop: spacing.xs,
                  marginBottom: spacing.md,
                }}
              >
                This action cannot be undone. Once cancelled, this service
                request will no longer be eligible for editing or processing.
              </p>

              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  flexWrap: "wrap",
                  gap: spacing.sm,
                }}
              >
                <AppButton
                  type="button"
                  variant="outline"
                  onClick={() => {
                    setShowCancelConfirm(false);
                    setCancelError(null);
                  }}
                  disabled={isCancelling}
                >
                  Keep Request
                </AppButton>

                <AppButton
                  type="button"
                  variant="danger"
                  onClick={handleCancel}
                  disabled={isCancelling}
                >
                  {isCancelling ? "Cancelling..." : "Cancel Request"}
                </AppButton>
              </div>
            </div>
          ) : (
            <div
              style={{
                display: "flex",
                alignItems: "center",
                flexWrap: "wrap",
                gap: spacing.md,
              }}
            >
              {editable && (
                <AppButton
                  type="button"
                  variant="primary"
                  onClick={() => navigate(`/service-requests/${requestId}/edit`)}
                >
                  Edit Request
                </AppButton>
              )}

              {cancellable && (
                <AppButton
                  type="button"
                  variant="danger"
                  onClick={() => {
                    setShowCancelConfirm(true);
                    setCancelError(null);
                  }}
                >
                  Cancel Request
                </AppButton>
              )}
            </div>
          )}
        </AppCard>
      )}
    </div>
  );
}

export default ServiceRequestDetailPage;
