import { useId } from "react";
import {
  colors,
  inputStyles,
  spacing,
  typography,
} from "../theme";

function AppInput({
  label,
  error,
  id,
  ...inputProps
}) {
  const generatedId = useId();
  const inputId = id ?? inputProps.id ?? generatedId;
  const errorId = error ? `${inputId}-error` : undefined;

  return (
    <div>
      {label && (
        <label
          htmlFor={inputId}
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
        id={inputId}
        aria-invalid={Boolean(error)}
        aria-describedby={errorId}
        {...inputProps}
        style={{
          ...inputStyles,
          ...(inputProps.style ?? {}),
        }}
      />

      {error && (
        <div
          id={errorId}
          role="alert"
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
