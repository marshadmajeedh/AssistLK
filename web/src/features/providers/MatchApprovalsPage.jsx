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

// ─── AI Rationale Panel ───────────────────────────────────────────────────────
function RationalePanel({ rationale, tokenUsage }) {
  const hasRationale = Boolean(rationale && rationale.trim() && rationale.trim() !== "No rationale available.");
  const hasTokens = tokenUsage != null && Number(tokenUsage) > 0;

  return (
    <div
      aria-label="Gemini AI Rationale"
      style={{
        marginTop: spacing.md,
        padding: spacing.md,
        borderRadius: radius.medium,
        backgroundColor: "rgba(30, 138, 129, 0.06)",
        border: `1px solid rgba(30, 138, 129, 0.22)`,
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
          background: `linear-gradient(180deg, ${colors.secondary}, #0d6e66)`,
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
              color: colors.secondary,
            }}
          >
            <span style={{ fontSize: 15 }}>✦</span>
            Gemini AI Rationale
          </span>

          {/* Token usage chip - only display when valid and > 0 */}
          {hasTokens && (
            <span
              aria-label={`Total tokens used: ${tokenUsage}`}
              style={{
                display: "inline-flex",
                alignItems: "center",
                gap: 4,
                padding: `3px ${spacing.sm}px`,
                borderRadius: radius.pill,
                backgroundColor: "rgba(30, 138, 129, 0.12)",
                border: `1px solid rgba(30, 138, 129, 0.30)`,
                fontSize: 11,
                fontWeight: 700,
                color: colors.secondary,
                letterSpacing: "0.05em",
              }}
            >
              <span style={{ opacity: 0.7 }}>⬡</span>
              {Number(tokenUsage).toLocaleString()} tokens
            </span>
          )}
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
          {hasRationale ? rationale : "Automated qualification based on verified technician skills and operational radius."}
        </p>
      </div>
    </div>
  );
}

// ─── Match Recommendation Card ────────────────────────────────────────────────
function MatchCard({ match, onDecision }) {
  const [processing, setProcessing] = useState(false);
  const [localError, setLocalError] = useState(null);

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
        backgroundColor: "rgba(255, 253, 249, 0.90)",
        border: `1px solid ${colors.border}`,
        borderRadius: radius.large,
        padding: spacing.lg,
        boxShadow: "0 24px 60px rgba(22, 34, 53, 0.08)",
        backdropFilter: "blur(18px)",
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
            backgroundColor: hasValidCandidate ? "rgba(22, 34, 53, 0.03)" : "rgba(182, 58, 43, 0.05)",
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
              </div>
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

      {/* ── Gemini AI Rationale ── */}
      <RationalePanel
        rationale={match.aiRationale}
        tokenUsage={match.tokenUsage?.total_tokens}
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
          variant="secondary"
          disabled={processing || !hasValidCandidate}
          onClick={() => handleAction("Approve")}
          style={{
            minWidth: 160,
            opacity: !hasValidCandidate ? 0.5 : 1,
            cursor: !hasValidCandidate ? "not-allowed" : "pointer",
          }}
        >
          {processing ? "Processing…" : "✓  Approve Match"}
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
        backgroundColor: "rgba(255, 253, 249, 0.84)",
        border: `1px solid ${colors.border}`,
        boxShadow: "0 24px 60px rgba(22, 34, 53, 0.06)",
      }}
    >
      <div style={{ fontSize: 48, marginBottom: spacing.md }}>✦</div>
      <h2
        style={{
          ...typography.sectionHeading,
          color: colors.textPrimary,
          marginBottom: spacing.sm,
        }}
      >
        All caught up
      </h2>
      <p style={{ ...typography.body, color: colors.textSecondary, maxWidth: 380, margin: "0 auto" }}>
        There are no AI match recommendations waiting for approval right now.
        New recommendations will appear here when the LangGraph dispatch
        pipeline pauses at the interrupt gate.
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
            AI dispatch recommendations paused at the LangGraph interrupt gate —
            review each match and approve or reject.
          </p>
        </div>

        <AppButton
          variant="outline"
          disabled={loading}
          onClick={fetchQueue}
          style={{ minWidth: 110, minHeight: 44 }}
        >
          {loading ? "Refreshing…" : "↻  Refresh"}
        </AppButton>
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
