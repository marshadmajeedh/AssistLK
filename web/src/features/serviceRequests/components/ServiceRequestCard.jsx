import { useNavigate } from "react-router-dom";
import AppCard from "../../../shared/components/AppCard";
import AppButton from "../../../shared/components/AppButton";
import StatusBadge from "../../../shared/components/StatusBadge";
import { colors, spacing, typography, radius } from "../../../shared/theme";

function formatCreatedAt(dateString) {
  if (!dateString) {
    return "";
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

function ServiceRequestCard({ request }) {
  const navigate = useNavigate();

  if (!request) {
    return null;
  }

  const requestId = request.serviceRequestId ?? request.id;
  const displayCategory = request.category || "Unclassified";
  const displayUrgency = request.urgency || "Unknown";
  const formattedDate = formatCreatedAt(request.createdAt);

  return (
    <AppCard
      style={{
        display: "flex",
        flexDirection: "column",
        justifyContent: "space-between",
        gap: spacing.md,
      }}
    >
      <div>
        {/* Card Header: Status and Created Date */}
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            flexWrap: "wrap",
            gap: spacing.xs,
            marginBottom: spacing.sm,
          }}
        >
          <StatusBadge status={request.status} />

          {formattedDate && (
            <span
              style={{
                ...typography.small,
                color: colors.textSecondary,
              }}
            >
              {formattedDate}
            </span>
          )}
        </div>

        {/* Problem Description Preview */}
        <p
          style={{
            ...typography.body,
            color: colors.textPrimary,
            margin: `0 0 ${spacing.md}px 0`,
            display: "-webkit-box",
            WebkitLineClamp: 3,
            WebkitBoxOrient: "vertical",
            overflow: "hidden",
            textOverflow: "ellipsis",
            wordBreak: "break-word",
          }}
        >
          {request.description}
        </p>

        {/* Metadata Details */}
        <div
          style={{
            display: "flex",
            flexDirection: "column",
            gap: spacing.xs,
            padding: spacing.sm,
            backgroundColor: colors.background,
            borderRadius: radius.small,
          }}
        >
          <div
            style={{
              ...typography.small,
              color: colors.textPrimary,
              display: "flex",
              alignItems: "baseline",
              gap: spacing.xs,
              wordBreak: "break-word",
            }}
          >
            <span style={{ fontWeight: 600, color: colors.textSecondary }}>
              Location:
            </span>
            <span>{request.locationText}</span>
          </div>

          <div
            style={{
              ...typography.small,
              color: colors.textPrimary,
              display: "flex",
              alignItems: "center",
              flexWrap: "wrap",
              gap: spacing.md,
            }}
          >
            <div>
              <span style={{ fontWeight: 600, color: colors.textSecondary }}>
                Category:
              </span>{" "}
              <span>{displayCategory}</span>
            </div>

            <div>
              <span style={{ fontWeight: 600, color: colors.textSecondary }}>
                Urgency:
              </span>{" "}
              <span>{displayUrgency}</span>
            </div>
          </div>
        </div>
      </div>

      {/* Card Action */}
      <div
        style={{
          display: "flex",
          justifyContent: "flex-end",
          paddingTop: spacing.xs,
        }}
      >
        <AppButton
          variant="outline"
          type="button"
          onClick={() => navigate(`/service-requests/${requestId}`)}
          style={{
            minHeight: 36,
            padding: `0 ${spacing.md}px`,
            fontSize: typography.small.fontSize,
          }}
        >
          View Details
        </AppButton>
      </div>
    </AppCard>
  );
}

export default ServiceRequestCard;
