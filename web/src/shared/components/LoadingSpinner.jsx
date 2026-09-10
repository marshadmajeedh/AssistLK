import {
  colors,
  spacing,
  typography,
} from "../theme";

function LoadingSpinner({
  message = "Loading...",
}) {
  return (
    <div
      role="status"
      aria-live="polite"
      style={{
        padding: spacing.lg,
        textAlign: "center",
        color: colors.textSecondary,
        ...typography.body,
      }}
    >
      {message}
    </div>
  );
}

export default LoadingSpinner;
