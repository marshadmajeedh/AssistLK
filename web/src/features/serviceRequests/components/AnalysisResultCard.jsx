import AppCard from "../../../shared/components/AppCard";
import StatusBadge from "../../../shared/components/StatusBadge";
import { colors, radius, spacing, typography } from "../../../shared/theme";

function formatConfidence(confidence) {
  if (confidence === null || confidence === undefined || confidence === "") {
    return "Not available";
  }
  const num = Number(confidence);
  if (Number.isNaN(num) || num < 0) {
    return "Not available";
  }
  // Expected in 0.0 to 1.0 range (e.g., 0.82 -> 82%)
  const percentage = num <= 1 ? Math.round(num * 100) : Math.round(num);
  return `${percentage}%`;
}

function AnalysisResultCard({
  category,
  urgency,
  problemSummary,
  confidence,
  status = "Analyzed",
}) {
  const displayCategory = category || "Unclassified";
  const displayUrgency = urgency || "Unknown";
  const displayConfidence = formatConfidence(confidence);
  const displaySummary =
    typeof problemSummary === "string" && problemSummary.trim().length > 0
      ? problemSummary.trim()
      : null;

  return (
    <AppCard>
      {/* Informational Review Banner */}
      <div
        style={{
          padding: `${spacing.sm}px ${spacing.md}px`,
          backgroundColor: colors.primaryLight,
          borderRadius: radius.medium,
          border: `1px solid ${colors.border}`,
          marginBottom: spacing.md,
        }}
      >
        <p
          style={{
            ...typography.body,
            color: colors.primaryDark,
            margin: 0,
            fontWeight: 500,
          }}
        >
          Your request has been analyzed. Review the details before continuing.
        </p>
      </div>

      {/* Header with Title and StatusBadge */}
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: spacing.sm,
          marginBottom: spacing.md,
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
        {status && <StatusBadge status={status} />}
      </div>

      {/* Structured Analysis Output Grid */}
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
            Suggested Service Category
          </span>
          <span
            style={{
              ...typography.body,
              fontWeight: 600,
              color: colors.textPrimary,
            }}
          >
            {displayCategory}
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
              fontWeight: 600,
              color: colors.textPrimary,
            }}
          >
            {displayUrgency}
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
            Analysis confidence
          </span>
          <span
            style={{
              ...typography.body,
              fontWeight: 600,
              color: colors.textPrimary,
            }}
          >
            {displayConfidence}
          </span>
        </div>
      </div>

      {/* Problem Summary (Likely Problem) */}
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
          Likely Problem
        </span>
        {displaySummary ? (
          <p
            style={{
              ...typography.body,
              color: colors.textPrimary,
              margin: 0,
              whiteSpace: "pre-wrap",
              wordBreak: "break-word",
            }}
          >
            {displaySummary}
          </p>
        ) : (
          <p
            style={{
              ...typography.body,
              color: colors.textSecondary,
              fontStyle: "italic",
              margin: 0,
            }}
          >
            Not available
          </p>
        )}
      </div>

      {/* Safe AI Advisory Footnote */}
      <div
        style={{
          marginTop: spacing.md,
          paddingTop: spacing.sm,
          borderTop: `1px solid ${colors.border}`,
        }}
      >
        <p
          style={{
            ...typography.small,
            color: colors.textSecondary,
            margin: 0,
          }}
        >
          Based on the information provided. The suggested service category and
          urgency help identify suitable service providers.
        </p>
      </div>
    </AppCard>
  );
}

export default AnalysisResultCard;
