import { badgeStyles } from "../theme";

const STATUS_LABELS = {
  Created: "Created",
  Analyzing: "Analyzing",
  AwaitingInformation: "Awaiting Information",
  Analyzed: "Analyzed",
  ReadyForMatching: "Ready for Matching",
  Cancelled: "Cancelled",
};

function StatusBadge({
  status,
  style = {},
  ...props
}) {
  const variant =
    badgeStyles[status] ??
    badgeStyles.default;

  const label =
    STATUS_LABELS[status] ??
    status ??
    "Unknown";

  return (
    <span
      style={{
        ...badgeStyles.base,
        ...variant,
        ...style,
      }}
      {...props}
    >
      {label}
    </span>
  );
}

export default StatusBadge;
