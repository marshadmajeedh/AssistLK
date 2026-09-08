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
