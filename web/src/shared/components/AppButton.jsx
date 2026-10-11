import {
  buttonStyles,
} from "../theme";

function AppButton({
  children,
  variant = "primary",
  type = "button",
  disabled = false,
  onClick,
  style = {},
}) {
  const variantStyle =
    buttonStyles[variant] ??
    buttonStyles.primary;

  return (
    <button
      type={type}
      disabled={disabled}
      onClick={onClick}
      className={`app-button app-button-${variant}`}
      style={{
        ...buttonStyles.base,
        ...variantStyle,
        ...(disabled
          ? buttonStyles.disabled
          : {}),
        ...style,
      }}
    >
      {children}
    </button>
  );
}

export default AppButton;
