import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import serviceRequestService from "../services/serviceRequestService";
import {
  canCancelRequest,
  canEditRequest,
  canAnalyzeRequest,
} from "../utils/serviceRequestStatus";
import getApiErrorMessage from "../utils/getApiErrorMessage";
import AppButton from "../../../shared/components/AppButton";
import AppCard from "../../../shared/components/AppCard";
import StatusBadge from "../../../shared/components/StatusBadge";
import ErrorMessage from "../../../shared/components/ErrorMessage";
import LoadingSpinner from "../../../shared/components/LoadingSpinner";
import AnalysisResultCard from "../components/AnalysisResultCard";
import ClarificationSection from "../components/ClarificationSection";
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

  // Transient session analysis state
  // Architectural limitation note: GET /api/service-requests/{id} returns ServiceRequestResponse,
  // which persists status, category, and urgency, but does not expose transient ProblemAnalysis
  // properties (problemSummary, confidence, followUpQuestions). Transient fields are retained
  // in analysisResult for the current session and never override newer persisted status.
  const [analysisResult, setAnalysisResult] = useState(null);
  const [isAnalyzing, setIsAnalyzing] = useState(false);
  const [analysisError, setAnalysisError] = useState(null);

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

  const handleAnalyze = async () => {
    if (isAnalyzing || !request) return;

    setIsAnalyzing(true);
    setAnalysisError(null);

    try {
      const requestId = request.serviceRequestId ?? request.id ?? id;
      const result = await serviceRequestService.analyze(requestId);

      // Retain returned ProblemUnderstandingResponseDto in transient state
      setAnalysisResult(result);

      // Refresh persisted ServiceRequest as authoritative lifecycle state
      const refreshed = await serviceRequestService.getById(requestId);
      setRequest(refreshed);
    } catch (err) {
      const message = getApiErrorMessage(
        err,
        "Failed to analyze problem. Please check your information and try again."
      );
      setAnalysisError(message);
    } finally {
      setIsAnalyzing(false);
    }
  };

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

  // Persisted lifecycle status is authoritative
  const currentStatus = request.status;
  const isCancelled = currentStatus === "Cancelled";
  const editable = canEditRequest(currentStatus);
  const cancellable = canCancelRequest(currentStatus);
  const analyzable = canAnalyzeRequest(currentStatus);

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
      {/* 1. Top Navigation & Back Action */}
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

      {/* 2. Details Page Header Card */}
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

          <StatusBadge status={currentStatus} />
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

      {/* 3. Request Information Card */}
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
                  <span style={{ fontWeight: 600 }}>Longitude:</span>{" "}
                  {lngDisplay}
                </div>
              </div>
            </div>
          )}
        </div>
      </AppCard>

      {/* 4. Analysis Section - Organized primarily around authoritative request.status */}
      {!isCancelled && (
        <div style={{ display: "flex", flexDirection: "column", gap: spacing.md }}>
          {/* Analysis Error Notification */}
          {analysisError && <ErrorMessage message={analysisError} />}

          {/* State A: Created - Prompt to Analyze */}
          {currentStatus === "Created" && (
            <AppCard>
              <div
                style={{
                  display: "flex",
                  justifyContent: "space-between",
                  alignItems: "center",
                  flexWrap: "wrap",
                  gap: spacing.sm,
                  marginBottom: spacing.sm,
                }}
              >
                <h2
                  style={{
                    ...typography.cardHeading,
                    margin: 0,
                    color: colors.textPrimary,
                  }}
                >
                  Problem Analysis
                </h2>
                <StatusBadge status={currentStatus} />
              </div>

              <p
                style={{
                  ...typography.body,
                  color: colors.textSecondary,
                  margin: `0 0 ${spacing.md}px 0`,
                }}
              >
                AssistLK will analyze your description to identify the likely
                service category and urgency.
              </p>

              <div>
                <AppButton
                  type="button"
                  variant="primary"
                  onClick={handleAnalyze}
                  disabled={isAnalyzing || !analyzable}
                >
                  {isAnalyzing ? "Analyzing problem..." : "Analyze Problem"}
                </AppButton>
              </div>
            </AppCard>
          )}

          {/* State B: Analyzing - Informational State */}
          {currentStatus === "Analyzing" && (
            <AppCard>
              <div
                style={{
                  display: "flex",
                  justifyContent: "space-between",
                  alignItems: "center",
                  flexWrap: "wrap",
                  gap: spacing.sm,
                  marginBottom: spacing.sm,
                }}
              >
                <h2
                  style={{
                    ...typography.cardHeading,
                    margin: 0,
                    color: colors.textPrimary,
                  }}
                >
                  Problem Analysis
                </h2>
                <StatusBadge status="Analyzing" />
              </div>

              <div
                style={{
                  padding: spacing.md,
                  backgroundColor: colors.secondaryLight,
                  borderRadius: radius.medium,
                  border: `1px solid ${colors.border}`,
                }}
              >
                <p
                  style={{
                    ...typography.body,
                    color: colors.secondary,
                    margin: 0,
                    fontWeight: 500,
                  }}
                >
                  Your request is currently being analyzed.
                </p>
              </div>
            </AppCard>
          )}

          {/* State C: AwaitingInformation - ClarificationSection */}
          {currentStatus === "AwaitingInformation" && (
            <ClarificationSection
              followUpQuestions={
                analysisResult &&
                (analysisResult.status === "AwaitingInformation" ||
                  analysisResult.needsMoreInformation)
                  ? analysisResult.followUpQuestions
                  : []
              }
              onEdit={() => navigate(`/service-requests/${requestId}/edit`)}
              onAnalyzeAgain={handleAnalyze}
              isAnalyzing={isAnalyzing}
              status={currentStatus}
            />
          )}

          {/* State D: Analyzed - AnalysisResultCard */}
          {currentStatus === "Analyzed" && (
            <AnalysisResultCard
              category={
                (analysisResult?.status === "Analyzed"
                  ? analysisResult.category
                  : null) || request.category
              }
              urgency={
                (analysisResult?.status === "Analyzed"
                  ? analysisResult.urgency
                  : null) || request.urgency
              }
              problemSummary={
                analysisResult?.status === "Analyzed"
                  ? analysisResult.problemSummary
                  : null
              }
              confidence={
                analysisResult?.status === "Analyzed"
                  ? analysisResult.confidence
                  : null
              }
              status={currentStatus}
            />
          )}
        </div>
      )}

      {/* 5. Actions & Cancellation Card - Hidden when Cancelled */}
      {!isCancelled && (editable || cancellable || showCancelConfirm) && (
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
              {/* Only show Edit Request here for Created status; AwaitingInformation already has Edit Request in ClarificationSection to avoid duplication */}
              {editable && currentStatus === "Created" && (
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
