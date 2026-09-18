import { colors } from "./colors";
import { spacing } from "./spacing";
import { radius } from "./radius";
import { typography } from "./typography";

export const buttonStyles = {
  base: {
    minHeight: 50,
    padding: `0 ${spacing.md}px`,
    borderRadius: radius.medium,
    border: "1px solid transparent",
    cursor: "pointer",
    fontFamily: typography.fontFamily,
    fontSize: typography.button.fontSize,
    fontWeight: typography.button.fontWeight,
    letterSpacing: typography.button.letterSpacing,
    transition: "transform 160ms ease, box-shadow 160ms ease, border-color 160ms ease, background-color 160ms ease",
    boxShadow: "0 12px 30px rgba(22, 34, 53, 0.10)",
  },

  primary: {
    backgroundColor: colors.primary,
    color: colors.surface,
    boxShadow: "0 18px 40px rgba(232, 93, 63, 0.22)",
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
    backgroundColor: "rgba(255, 253, 249, 0.78)",
    color: colors.textPrimary,
    border: `1px solid ${colors.border}`,
    boxShadow: "none",
  },

  disabled: {
    backgroundColor: colors.disabled,
    color: colors.surface,
    cursor: "not-allowed",
    boxShadow: "none",
  },
};

export const inputStyles = {
  display: "block",
  width: "100%",
  minHeight: 54,

  padding: `0 ${spacing.md}px`,

  backgroundColor: "rgba(255, 253, 249, 0.88)",

  color: colors.textPrimary,

  border: `1px solid ${colors.border}`,

  borderRadius: radius.medium,

  fontFamily: typography.fontFamily,

  fontSize: typography.body.fontSize,

  outline: "none",

  boxSizing: "border-box",
  boxShadow: "inset 0 1px 0 rgba(255,255,255,0.7)",
};

export const cardStyles = {
  backgroundColor: "rgba(255, 253, 249, 0.84)",

  border: `1px solid ${colors.border}`,

  borderRadius: radius.large,

  padding: spacing.lg,

  color: colors.textPrimary,

  boxShadow: "0 24px 60px rgba(22, 34, 53, 0.08)",
  backdropFilter: "blur(18px)",
};

export const pageStyles = {
  backgroundColor: colors.background,

  color: colors.textPrimary,

  minHeight: "100vh",

  padding: spacing.lg,

  fontFamily: typography.fontFamily,
};

export const badgeStyles = {
  base: {
    display: "inline-flex",
    alignItems: "center",
    padding: `${spacing.xs}px ${spacing.sm}px`,
    borderRadius: radius.pill,
    fontFamily: typography.fontFamily,
    fontSize: typography.small.fontSize,
    fontWeight: 600,
    lineHeight: typography.small.lineHeight,
    whiteSpace: "nowrap",
    border: `1px solid rgba(22, 34, 53, 0.08)`,
  },

  Created: {
    backgroundColor: colors.neutralLight,
    color: colors.textPrimary,
  },

  Analyzing: {
    backgroundColor: colors.secondaryLight,
    color: colors.secondary,
  },

  AwaitingInformation: {
    backgroundColor: colors.warningLight,
    color: colors.warning,
  },

  Analyzed: {
    backgroundColor: colors.successLight,
    color: colors.success,
  },

  ReadyForMatching: {
    backgroundColor: colors.primaryLight,
    color: colors.primary,
  },

  Cancelled: {
    backgroundColor: colors.errorLight,
    color: colors.error,
  },

  default: {
    backgroundColor: colors.neutralLight,
    color: colors.textSecondary,
  },
};
