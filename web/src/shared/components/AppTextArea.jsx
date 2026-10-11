import { useId } from "react";
import {
  colors,
  inputStyles,
  spacing,
  typography,
} from "../theme";

function AppTextArea({
  label,
  error,
  id,
  ...textAreaProps
}) {
  const generatedId = useId();
  const textareaId = id ?? textAreaProps.id ?? generatedId;
  const errorId = error ? `${textareaId}-error` : undefined;

  return (
    <div>
      {label && (
        <label
          htmlFor={textareaId}
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
        id={textareaId}
        aria-invalid={Boolean(error)}
        aria-describedby={errorId}
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

export default AppTextArea;
