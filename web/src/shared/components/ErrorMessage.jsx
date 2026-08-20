import {
  colors,
  radius,
  spacing,
  typography,
} from "../theme";

function ErrorMessage({ message }) {
  if (!message) {
    return null;
  }

  return (
    <div
      style={{
        ...typography.body,
        marginTop: spacing.md,
        padding: spacing.md,
        color: colors.error,
        backgroundColor: colors.surface,
        border: `1px solid ${colors.error}`,
        borderRadius: radius.medium,
      }}
    >
      {message}
    </div>
  );
}

export default ErrorMessage;
