import React, { useState, useEffect, useCallback } from "react";
import { getMatchApprovals, resumeMatch } from "./providersApi";
import LoadingSpinner from "../../shared/components/LoadingSpinner";
import ErrorMessage from "../../shared/components/ErrorMessage";
import AppButton from "../../shared/components/AppButton";
import { colors, spacing, typography, radius } from "../../shared/theme";

// ─── Trade category badge colours ────────────────────────────────────────────
const CATEGORY_COLORS = {
  Plumbing:            { bg: "#e0f2fe", text: "#0369a1", border: "#bae6fd" },
  Electrical:          { bg: "#fef3c7", text: "#b45309", border: "#fde68a" },
  "Vehicle Assistance":{ bg: "#fee2e2", text: "#b91c1c", border: "#fecaca" },
  "Appliance Repair":  { bg: "#ede9fe", text: "#6d28d9", border: "#ddd6fe" },
  "General Cleaning":  { bg: "#d1fae5", text: "#065f46", border: "#6ee7b7" },
  "HVAC":              { bg: "#e0e7ff", text: "#3730a3", border: "#c7d2fe" },
};

const URGENCY_COLORS = {
  High:   { bg: "#fee2e2", text: "#b91c1c", border: "#fecaca" },
  Medium: { bg: "#fef3c7", text: "#b45309", border: "#fde68a" },
  Low:    { bg: "#d1fae5", text: "#065f46", border: "#6ee7b7" },
};

// ─── Star rating display ──────────────────────────────────────────────────────
function StarRating({ value }) {
  const full  = Math.floor(value ?? 0);
  const half  = (value ?? 0) - full >= 0.5;
  const empty = 5 - full - (half ? 1 : 0);
  return (
    <span
      aria-label={`${value ?? 0} out of 5 stars`}
      style={{ fontSize: 16, letterSpacing: 1 }}
    >
      {"★".repeat(full)}
      {half ? "½" : ""}
      {"☆".repeat(empty)}
    </span>
  );
}

// ─── Inline badge ─────────────────────────────────────────────────────────────
function Badge({ label, palette }) {
  const { bg, text, border } = palette ?? {
    bg: colors.neutralLight,
    text: colors.textSecondary,
    border: colors.border,
  };
  return (
    <span
      style={{
        display: "inline-flex",
        alignItems: "center",
        padding: `${spacing.xs - 2}px ${spacing.sm}px`,
        borderRadius: radius.pill,
        border: `1px solid ${border}`,
        backgroundColor: bg,
        color: text,
        fontSize: typography.small.fontSize,
        fontWeight: 600,
        lineHeight: typography.small.lineHeight,
        whiteSpace: "nowrap",
      }}
    >
      {label}
    </span>
  );
}

// ─── Toast notification ───────────────────────────────────────────────────────
function Toast({ message, type }) {
  const isSuccess = type === "success";
  return (
    <div
      role="status"
      aria-live="polite"
      style={{
        position: "fixed",
        bottom: spacing.xl,
        right: spacing.xl,
        zIndex: 9999,
        display: "flex",
        alignItems: "center",
        gap: spacing.sm,
        padding: `${spacing.sm}px ${spacing.lg}px`,
        borderRadius: radius.medium,
        backgroundColor: isSuccess ? colors.success : colors.error,
        color: "#fff",
        boxShadow: "0 20px 50px rgba(22,34,53,0.22)",
        fontFamily: typography.fontFamily,
        fontSize: typography.body.fontSize,
        fontWeight: 600,
        maxWidth: 420,
        animation: "fadeSlideIn 220ms ease",
      }}
    >
      <span style={{ fontSize: 20 }}>{isSuccess ? "✓" : "✕"}</span>
      {message}
      <style>{`
        @keyframes fadeSlideIn {
          from { opacity: 0; transform: translateY(12px); }
          to   { opacity: 1; transform: translateY(0);    }
        }
      `}</style>
    </div>
  );
}

// ─── Match Recommendation Justification Panel ─────────────────────────────────────────
function RationalePanel({ rationale }) {
  const hasRationale = Boolean(rationale && rationale.trim() && rationale.trim() !== "No rationale available.");

  return (
    <div
      aria-label="Match Recommendation Justification"
      style={{
        marginTop: spacing.md,
        padding: spacing.md,
        borderRadius: radius.medium,
        backgroundColor: "rgba(31, 78, 120, 0.04)",
        border: "1px solid rgba(31, 78, 120, 0.18)",
        position: "relative",
        overflow: "hidden",
      }}
    >
      {/* accent bar */}
      <div
        style={{
          position: "absolute",
          left: 0,
          top: 0,
          bottom: 0,
          width: 4,
          borderRadius: `${radius.medium}px 0 0 ${radius.medium}px`,
          background: `linear-gradient(180deg, ${colors.primary}, ${colors.primaryDark})`,
        }}
      />
      <div style={{ paddingLeft: spacing.md }}>
        {/* header row */}
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            marginBottom: spacing.sm,
            flexWrap: "wrap",
            gap: spacing.xs,
          }}
        >
          <span
            style={{
              display: "flex",
              alignItems: "center",
              gap: spacing.xs,
              fontSize: typography.small.fontSize,
              fontWeight: 700,
              textTransform: "uppercase",
              letterSpacing: "0.07em",
              color: colors.primary,
            }}
          >
            <span style={{ fontSize: 14 }}>ℹ</span>
            Match Recommendation Justification
          </span>
        </div>

        {/* rationale body */}
        <p
          style={{
            margin: 0,
            fontSize: typography.body.fontSize,
            lineHeight: 1.65,
            color: hasRationale ? colors.textPrimary : colors.textSecondary,
            fontStyle: hasRationale ? "normal" : "italic",
          }}
        >
          {hasRationale ? rationale : "Automated qualification based on verified technician trade skills and service radius."}
        </p>
      </div>
    </div>
  );
}

// ─── Match Recommendation Card ────────────────────────────────────────────────
function MatchCard({ match, onDecision }) {
  const [processing, setProcessing] = useState(false);
  const [localError, setLocalError] = useState(null);

  const isOccupied = match.candidate?.occupancyStatus && match.candidate.occupancyStatus !== "Available";
  const isReviewing = match.candidate?.occupancyStatus === "ReviewingDispatch";
  const isBusy = match.candidate?.occupancyStatus === "BusyOnJob";

  const categoryPalette =
    CATEGORY_COLORS[match.serviceRequest?.tradeCategory] ?? null;
  const urgencyPalette =
    URGENCY_COLORS[match.serviceRequest?.urgency] ?? null;

  const handleAction = async (action) => {
    setProcessing(true);
    setLocalError(null);
    try {
      await resumeMatch(match.threadId, action);
      onDecision(match.threadId, action);
    } catch (err) {
      setLocalError(
        err.response?.data?.message ||
          err.message ||
          "Failed to process decision. Please try again."
      );
      setProcessing(false);
    }
  };

  const hasValidCandidate = Boolean(
    match.candidate &&
    match.candidate.technicianName &&
    match.candidate.technicianName.trim() !== "" &&
    match.candidate.technicianName !== "—"
  );

  return (
    <article
      aria-label={`Match recommendation for thread ${match.threadId}`}
      style={{
        backgroundColor: "#ffffff",
        border: `1px solid ${colors.border}`,
        borderRadius: radius.large,
        padding: spacing.lg,
        boxShadow: "0 4px 20px rgba(22, 34, 53, 0.06)",
        display: "flex",
        flexDirection: "column",
        gap: spacing.md,
      }}
    >
      {/* ── Thread ID header ── */}
      <div
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          flexWrap: "wrap",
          gap: spacing.xs,
        }}
      >
        <span
          style={{
            fontSize: typography.small.fontSize,
            fontWeight: 700,
            letterSpacing: "0.07em",
            textTransform: "uppercase",
            color: colors.textSecondary,
          }}
        >
          Thread&nbsp;
          <code
            style={{
              fontFamily: "monospace",
              background: colors.neutralLight,
              padding: "1px 6px",
              borderRadius: radius.small,
              letterSpacing: 0,
            }}
          >
            {match.threadId}
          </code>
        </span>
        <Badge label="Awaiting Approval" palette={{ bg: "#FEF0C8", text: "#AF6A17", border: "#FDE68A" }} />
      </div>

      <hr style={{ border: "none", borderTop: `1px solid ${colors.border}`, margin: 0 }} />

      {/* ── Two-column: Service Request + Candidate ── */}
      <div
        style={{
          display: "grid",
          gridTemplateColumns: "1fr 1fr",
          gap: spacing.md,
        }}
      >
        {/* Service Request */}
        <section aria-label="Service Request Details">
          <p
            style={{
              margin: `0 0 ${spacing.xs}px`,
              fontSize: typography.small.fontSize,
              fontWeight: 700,
              letterSpacing: "0.07em",
              textTransform: "uppercase",
              color: colors.textSecondary,
            }}
          >
            Service Request
          </p>

          <p
            style={{
              margin: `0 0 ${spacing.sm}px`,
              ...typography.cardHeading,
              color: colors.textPrimary,
            }}
          >
            {match.serviceRequest?.problemSummary ?? "—"}
          </p>

          <div style={{ display: "flex", flexWrap: "wrap", gap: spacing.xs }}>
            {match.serviceRequest?.tradeCategory && (
              <Badge
                label={match.serviceRequest.tradeCategory}
                palette={categoryPalette}
              />
            )}
            {match.serviceRequest?.urgency && (
              <Badge
                label={`${match.serviceRequest.urgency} Urgency`}
                palette={urgencyPalette}
              />
            )}
          </div>
        </section>

        {/* Matched Candidate */}
        <section
          aria-label="Matched Technician Details"
          style={{
            padding: spacing.md,
            borderRadius: radius.medium,
            backgroundColor: hasValidCandidate ? "#F8FAFC" : "rgba(182, 58, 43, 0.05)",
            border: `1px solid ${hasValidCandidate ? colors.border : colors.errorLight}`,
          }}
        >
          <p
            style={{
              margin: `0 0 ${spacing.xs}px`,
              fontSize: typography.small.fontSize,
              fontWeight: 700,
              letterSpacing: "0.07em",
              textTransform: "uppercase",
              color: hasValidCandidate ? colors.textSecondary : colors.error,
            }}
          >
            Matched Candidate
          </p>

          {hasValidCandidate ? (
            <>
              <p
                style={{
                  margin: `0 0 2px`,
                  ...typography.cardHeading,
                  color: colors.textPrimary,
                }}
              >
                {match.candidate.technicianName}
              </p>

              {match.candidate?.businessName && (
                <p
                  style={{
                    margin: `0 0 ${spacing.sm}px`,
                    fontSize: typography.body.fontSize,
                    color: colors.textSecondary,
                  }}
                >
                  {match.candidate.businessName}
                </p>
              )}

              <div style={{ display: "flex", flexWrap: "wrap", gap: spacing.sm, alignItems: "center" }}>
                {match.candidate?.rating != null && (
                  <span style={{ display: "flex", alignItems: "center", gap: 4 }}>
                    <StarRating value={match.candidate.rating} />
                    <span
                      style={{
                        fontSize: typography.small.fontSize,
                        color: colors.textSecondary,
                        fontWeight: 600,
                      }}
                    >
                      {Number(match.candidate.rating).toFixed(1)}
                    </span>
                  </span>
                )}
                {match.candidate?.distanceKm != null && (
                  <Badge
                    label={`${match.candidate.distanceKm} km away`}
                    palette={{
                      bg: colors.neutralLight,
                      text: colors.textSecondary,
                      border: colors.border,
                    }}
                  />
                )}
                {isReviewing && (
                  <Badge
                    label="⏳ Reviewing Dispatch"
                    palette={{
                      bg: "#fef3c7",
                      text: "#b45309",
                      border: "#fde68a",
                    }}
                  />
                )}
                {isBusy && (
                  <Badge
                    label="🛠️ Busy On Active Job"
                    palette={{
                      bg: "#fee2e2",
                      text: "#b91c1c",
                      border: "#fecaca",
                    }}
                  />
                )}
              </div>

              {isOccupied && (
                <div
                  style={{
                    marginTop: spacing.sm,
                    padding: `${spacing.xs + 2}px ${spacing.sm}px`,
                    borderRadius: radius.small,
                    backgroundColor: isBusy ? "rgba(185, 28, 28, 0.08)" : "rgba(180, 83, 9, 0.08)",
                    border: `1px solid ${isBusy ? "#fecaca" : "#fde68a"}`,
                    display: "flex",
                    alignItems: "center",
                    gap: spacing.xs,
                  }}
                >
                  <span
                    style={{
                      fontSize: typography.small.fontSize,
                      fontWeight: 600,
                      color: isBusy ? "#b91c1c" : "#b45309",
                      lineHeight: 1.3,
                    }}
                  >
                    {match.candidate?.occupancyReason ||
                      (isBusy
                        ? "Provider is busy on an active job. Approval locked until job is completed."
                        : "Provider is currently reviewing a dispatch. Approval locked until they accept or decline.")}
                  </span>
                </div>
              )}
            </>
          ) : (
            <div>
              <p
                style={{
                  margin: `0 0 4px`,
                  ...typography.cardHeading,
                  color: colors.error,
                }}
              >
                No Candidate Matched
              </p>
              <p
                style={{
                  margin: 0,
                  fontSize: typography.small.fontSize,
                  color: colors.textSecondary,
                  lineHeight: 1.4,
                }}
              >
                No eligible provider was qualified in radius. Please reject this run to return the request to the pool.
              </p>
            </div>
          )}
        </section>
      </div>

      {/* ── Match Recommendation Justification ── */}
      <RationalePanel
        rationale={match.aiRationale}
      />

      {/* ── Error feedback ── */}
      {localError && <ErrorMessage message={localError} />}

      {/* ── Action Controls ── */}
      <div
        style={{
          display: "flex",
          gap: spacing.sm,
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "wrap",
          paddingTop: spacing.xs,
        }}
      >
        <AppButton
          variant="outline"
          disabled={processing}
          onClick={() => handleAction("Reject")}
          style={{ minWidth: 130 }}
        >
          {processing ? "Processing…" : "✕  Reject Match"}
        </AppButton>

        <AppButton
          variant="primary"
          disabled={processing || !hasValidCandidate || isOccupied}
          onClick={() => handleAction("Approve")}
          title={isOccupied ? match.candidate?.occupancyReason : undefined}
          style={{
            minWidth: 160,
            opacity: !hasValidCandidate || isOccupied ? 0.5 : 1,
            cursor: !hasValidCandidate || isOccupied ? "not-allowed" : "pointer",
          }}
        >
          {processing
            ? "Processing…"
            : isReviewing
            ? "⏳ Review Pending"
            : isBusy
            ? "🛠️ Provider Busy"
            : "✓  Approve Match"}
        </AppButton>
      </div>
    </article>
  );
}

// ─── Empty state ──────────────────────────────────────────────────────────────
function EmptyState() {
  return (
    <div
      aria-label="No pending approvals"
      style={{
        textAlign: "center",
        padding: `${spacing.xl}px ${spacing.lg}px`,
        borderRadius: radius.large,
        backgroundColor: "#ffffff",
        border: `1px solid ${colors.border}`,
        boxShadow: "0 4px 20px rgba(22, 34, 53, 0.04)",
      }}
    >
      <div style={{ fontSize: 42, marginBottom: spacing.md, color: colors.primary }}>✓</div>
      <h2
        style={{
          ...typography.sectionHeading,
          color: colors.textPrimary,
          marginBottom: spacing.sm,
        }}
      >
        All caught up
      </h2>
      <p style={{ ...typography.body, color: colors.textSecondary, maxWidth: 420, margin: "0 auto" }}>
        There are no dispatch recommendations requiring review at this time.
        New match recommendations will appear here automatically when customer requests are processed.
      </p>
    </div>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────
function MatchApprovalsPage() {
  const [matches,   setMatches]   = useState([]);
  const [loading,   setLoading]   = useState(true);
  const [error,     setError]     = useState(null);
  const [toast,     setToast]     = useState(null); // { message, type }

  const showToast = useCallback((message, type = "success") => {
    setToast({ message, type });
    setTimeout(() => setToast(null), 3500);
  }, []);

  const fetchQueue = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await getMatchApprovals();
      const raw = Array.isArray(data) ? data : [];
      // Filter out ghost runs where no candidate was matched
      const validMatches = raw.filter(
        (m) =>
          m &&
          m.threadId &&
          m.candidate &&
          m.candidate.technicianName &&
          m.candidate.technicianName.trim() !== "" &&
          m.candidate.technicianName !== "—"
      );
      setMatches(validMatches);
    } catch (err) {
      setError(
        err.response?.data?.message ||
          err.message ||
          "Failed to load pending match approvals."
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchQueue();

    const silentPoll = () => {
      getMatchApprovals()
        .then((data) => {
          const raw = Array.isArray(data) ? data : [];
          const validMatches = raw.filter(
            (m) =>
              m &&
              m.threadId &&
              m.candidate &&
              m.candidate.technicianName &&
              m.candidate.technicianName.trim() !== "" &&
              m.candidate.technicianName !== "—"
          );
          setMatches(validMatches);
        })
        .catch(() => {});
    };

    // Auto-poll every 2.5 seconds to keep occupancy status and approvals fresh in real time
    const pollInterval = setInterval(() => {
      if (!document.hidden) {
        silentPoll();
      }
    }, 2500);

    const handleVisibilityChange = () => {
      if (!document.hidden) {
        silentPoll();
      }
    };
    document.addEventListener("visibilitychange", handleVisibilityChange);

    return () => {
      clearInterval(pollInterval);
      document.removeEventListener("visibilitychange", handleVisibilityChange);
    };
  }, [fetchQueue]);

  const handleDecision = useCallback((threadId, action) => {
    // Optimistically remove the acted-upon card from the queue
    setMatches((prev) => prev.filter((m) => m.threadId !== threadId));
    showToast(
      action === "Approve"
        ? "Match approved — technician will be dispatched."
        : "Match rejected — request returned to the dispatch pool.",
      "success"
    );
  }, [showToast]);

  return (
    <div
      style={{
        maxWidth: 900,
        margin: "0 auto",
        fontFamily: typography.fontFamily,
        color: colors.textPrimary,
      }}
    >
      {/* ── Page header ── */}
      <div
        style={{
          display: "flex",
          alignItems: "flex-start",
          justifyContent: "space-between",
          flexWrap: "wrap",
          gap: spacing.md,
          marginBottom: spacing.lg,
        }}
      >
        <div>
          <h1
            style={{
              ...typography.pageTitle,
              color: colors.textPrimary,
              margin: 0,
            }}
          >
            Match Approvals
          </h1>
          <p
            style={{
              ...typography.body,
              color: colors.textSecondary,
              marginTop: spacing.xs,
              marginBottom: 0,
            }}
          >
            Review automated dispatch recommendations and authorize technician assignment.
          </p>
        </div>
      </div>

      {/* ── Pending count chip ── */}
      {!loading && !error && (
        <div
          style={{
            display: "inline-flex",
            alignItems: "center",
            gap: spacing.xs,
            padding: `${spacing.xs}px ${spacing.md}px`,
            borderRadius: radius.pill,
            backgroundColor:
              matches.length > 0 ? colors.primaryLight : colors.neutralLight,
            border: `1px solid ${matches.length > 0 ? "#fcc" : colors.border}`,
            color:
              matches.length > 0 ? colors.primary : colors.textSecondary,
            fontSize: typography.small.fontSize,
            fontWeight: 700,
            marginBottom: spacing.lg,
          }}
          aria-live="polite"
        >
          {matches.length > 0
            ? `${matches.length} pending approval${matches.length !== 1 ? "s" : ""}`
            : "No pending approvals"}
        </div>
      )}

      {/* ── States ── */}
      {loading && <LoadingSpinner message="Loading pending match approvals…" />}
      {!loading && error && <ErrorMessage message={error} />}
      {!loading && !error && matches.length === 0 && <EmptyState />}

      {/* ── Card list ── */}
      {!loading && !error && matches.length > 0 && (
        <div
          style={{ display: "flex", flexDirection: "column", gap: spacing.lg }}
        >
          {matches.map((match) => (
            <MatchCard
              key={match.threadId}
              match={match}
              onDecision={handleDecision}
            />
          ))}
        </div>
      )}

      {/* ── Toast ── */}
      {toast && <Toast message={toast.message} type={toast.type} />}
    </div>
  );
}

export default MatchApprovalsPage;
