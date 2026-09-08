import {
  colors,
  inputStyles,
  spacing,
  typography,
} from "../theme";

function AppTextArea({
  label,
  error,
  ...textAreaProps
}) {
  return (
    <div>
      {label && (
        <label
          style={{
            ...typography.body,
            display: "block",
            color: colors.textPrimary,
            marginBottom: spacing.sm,
          }}
        >
          {label}
        </label>
      )}

      <textarea
        {...textAreaProps}
        style={{
          ...inputStyles,
          minHeight: 120,
          resize: "vertical",
          paddingTop: spacing.sm,
          paddingBottom: spacing.sm,
          ...(textAreaProps.style ?? {}),
        }}
      />

      {error && (
        <div
          style={{
            ...typography.small,
            color: colors.error,
            marginTop: spacing.xs,
          }}
        >
          {error}
        </div>
      )}
    </div>
  );
}

export default AppTextArea;
