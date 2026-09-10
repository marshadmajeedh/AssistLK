import { useNavigate } from "react-router-dom";
import AppCard from "../../../shared/components/AppCard";
import AppButton from "../../../shared/components/AppButton";
import StatusBadge from "../../../shared/components/StatusBadge";
import { colors, radius, spacing, typography } from "../../../shared/theme";

function ReadyForMatchingSection({ request }) {
  const navigate = useNavigate();

  const displayCategory = request?.category || "Unclassified";
  const displayUrgency = request?.urgency || "Unknown";
  const displayLocation = request?.locationText || "Not provided";

  return (
    <AppCard>
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
          Request ready for provider matching
        </h2>
        <StatusBadge status="ReadyForMatching" />
      </div>

      {/* Confirmation Notice Banner */}
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
          Your service request has been confirmed and is ready for the next step.
        </p>
      </div>

      {/* Confirmed Details Summary */}
      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))",
          gap: spacing.md,
          padding: spacing.md,
          backgroundColor: colors.background,
          borderRadius: radius.medium,
          border: `1px solid ${colors.border}`,
          marginBottom: spacing.md,
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
            Confirmed Category
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
            Confirmed Urgency
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
            Service Location
          </span>
          <span
            style={{
              ...typography.body,
              color: colors.textPrimary,
              wordBreak: "break-word",
            }}
          >
            {displayLocation}
          </span>
        </div>
      </div>

      {/* Supporting Guidance */}
      <p
        style={{
          ...typography.body,
          color: colors.textSecondary,
          margin: `0 0 ${spacing.lg}px 0`,
        }}
      >
        Provider matching will use your service category and location to identify
        suitable providers.
      </p>

      {/* Actions */}
      <div
        style={{
          display: "flex",
          justifyContent: "flex-start",
          alignItems: "center",
          gap: spacing.sm,
        }}
      >
        <AppButton
          type="button"
          variant="outline"
          onClick={() => navigate("/service-requests")}
        >
          ← Back to My Requests
        </AppButton>
      </div>
    </AppCard>
  );
}

export default ReadyForMatchingSection;
