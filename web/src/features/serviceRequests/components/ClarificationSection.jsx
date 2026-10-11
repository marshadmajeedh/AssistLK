import AppCard from "../../../shared/components/AppCard";
import AppButton from "../../../shared/components/AppButton";
import StatusBadge from "../../../shared/components/StatusBadge";
import { colors, radius, spacing, typography } from "../../../shared/theme";

function ClarificationSection({
  followUpQuestions = [],
  onEdit,
  onAnalyzeAgain,
  isAnalyzing = false,
  status = "AwaitingInformation",
}) {
  const hasQuestions =
    Array.isArray(followUpQuestions) &&
    followUpQuestions.filter(
      (q) => typeof q === "string" && q.trim().length > 0
    ).length > 0;

  const validQuestions = hasQuestions
    ? followUpQuestions.filter(
        (q) => typeof q === "string" && q.trim().length > 0
      )
    : [];

  return (
    <AppCard
      style={{
        border: `1px solid ${colors.warning}`,
      }}
    >
      {/* Header with Title and Status */}
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
        <div style={{ display: "flex", alignItems: "center", gap: spacing.sm }}>
          <span
            style={{
              display: "inline-block",
              width: 10,
              height: 10,
              borderRadius: "50%",
              backgroundColor: colors.warning,
            }}
            aria-hidden="true"
          />
          <h2
            style={{
              ...typography.cardHeading,
              margin: 0,
              color: colors.textPrimary,
            }}
          >
            More information is needed
          </h2>
        </div>

        {status && <StatusBadge status={status} />}
      </div>

      {/* Explanation Banner */}
      <div
        style={{
          padding: `${spacing.sm}px ${spacing.md}px`,
          backgroundColor: colors.warningLight,
          borderRadius: radius.medium,
          marginBottom: spacing.md,
        }}
      >
        <p
          style={{
            ...typography.body,
            color: colors.warning,
            margin: 0,
            fontWeight: 500,
          }}
        >
          AssistLK needs a little more information before completing the
          analysis.
        </p>
      </div>

      {/* Follow-up Questions or Generic Guidance */}
      {hasQuestions ? (
        <div style={{ marginBottom: spacing.md }}>
          <span
            style={{
              ...typography.small,
              fontWeight: 600,
              color: colors.textSecondary,
              display: "block",
              marginBottom: spacing.xs,
            }}
          >
            Please clarify the following details in your request:
          </span>
          <ol
            style={{
              margin: 0,
              paddingLeft: spacing.lg,
              display: "flex",
              flexDirection: "column",
              gap: spacing.xs,
            }}
          >
            {validQuestions.map((question, index) => (
              <li
                key={`question-${index}`}
                style={{
                  ...typography.body,
                  color: colors.textPrimary,
                  lineHeight: 1.5,
                }}
              >
                {question}
              </li>
            ))}
          </ol>
        </div>
      ) : (
        <div style={{ marginBottom: spacing.md }}>
          <p
            style={{
              ...typography.body,
              color: colors.textPrimary,
              margin: 0,
            }}
          >
            More information is needed to complete the analysis. Update your
            request with any relevant missing details, then analyze it again.
          </p>
        </div>
      )}

      {/* Guidance Text */}
      <p
        style={{
          ...typography.small,
          color: colors.textSecondary,
          marginBottom: spacing.md,
        }}
      >
        Update your request with the requested details, then run the analysis
        again.
      </p>

      {/* Clarification Actions */}
      <div
        style={{
          display: "flex",
          alignItems: "center",
          flexWrap: "wrap",
          gap: spacing.md,
          paddingTop: spacing.md,
          borderTop: `1px solid ${colors.border}`,
        }}
      >
        {onEdit && (
          <AppButton
            type="button"
            variant="outline"
            onClick={onEdit}
            disabled={isAnalyzing}
          >
            Edit Request
          </AppButton>
        )}

        {onAnalyzeAgain && (
          <AppButton
            type="button"
            variant="primary"
            onClick={onAnalyzeAgain}
            disabled={isAnalyzing}
          >
            {isAnalyzing ? "Analyzing problem..." : "Analyze Again"}
          </AppButton>
        )}
      </div>
    </AppCard>
  );
}

export default ClarificationSection;
