import { colors } from "./colors";
import { spacing } from "./spacing";
import { radius } from "./radius";
import { typography } from "./typography";

export const buttonStyles = {
  base: {
    minHeight: 48,
    padding: `0 ${spacing.md}px`,
    borderRadius: radius.medium,
    border: "none",
    cursor: "pointer",
    fontFamily: typography.fontFamily,
    fontSize: typography.button.fontSize,
    fontWeight: typography.button.fontWeight,
  },

  primary: {
    backgroundColor: colors.primary,
    color: colors.surface,
  },

  secondary: {
    backgroundColor: colors.secondary,
    color: colors.surface,
  },

  danger: {
    backgroundColor: colors.error,
    color: colors.surface,
  },

  outline: {
    backgroundColor: colors.surface,
    color: colors.primary,
    border: `1px solid ${colors.primary}`,
  },

  disabled: {
    backgroundColor: colors.disabled,
    color: colors.surface,
    cursor: "not-allowed",
  },
};

export const inputStyles = {
  width: "100%",
  minHeight: 48,

  padding: `0 ${spacing.md}px`,

  backgroundColor: colors.surface,

  color: colors.textPrimary,

  border: `1px solid ${colors.border}`,

  borderRadius: radius.medium,

  fontFamily: typography.fontFamily,

  fontSize: typography.body.fontSize,

  outline: "none",

  boxSizing: "border-box",
};

export const cardStyles = {
  backgroundColor: colors.surface,

  border: `1px solid ${colors.border}`,

  borderRadius: radius.large,

  padding: spacing.md,

  color: colors.textPrimary,

  boxShadow: "0 2px 8px rgba(0, 0, 0, 0.06)",
};

export const pageStyles = {
  backgroundColor: colors.background,

  color: colors.textPrimary,

  minHeight: "100vh",

  padding: spacing.lg,

  fontFamily: typography.fontFamily,
};
