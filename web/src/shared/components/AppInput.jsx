import {
  colors,
  inputStyles,
  spacing,
  typography,
} from "../theme";

function AppInput({
  label,
  error,
  ...inputProps
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

      <input
        {...inputProps}
        style={{
          ...inputStyles,
          ...(inputProps.style ?? {}),
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

export default AppInput;
