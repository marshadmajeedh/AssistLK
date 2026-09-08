import {
  Link,
  NavLink,
  Outlet,
  useNavigate,
} from "react-router-dom";
import { useAuthStore } from "../auth/authStore";
import AppButton from "../components/AppButton";
import {
  colors,
  radius,
  spacing,
  typography,
} from "../theme";

function CustomerLayout() {
  const navigate = useNavigate();
  const user = useAuthStore((state) => state.user);
  const logout = useAuthStore((state) => state.logout);

  const handleLogout = () => {
    logout();
    navigate("/login", { replace: true });
  };

  const navLinkStyle = ({ isActive }) => ({
    ...typography.body,
    fontSize: typography.body.fontSize,
    fontWeight: isActive ? 600 : 500,
    color: isActive ? colors.primary : colors.textSecondary,
    textDecoration: "none",
    padding: `${spacing.xs}px ${spacing.sm}px`,
    borderRadius: radius.small,
    borderBottom: isActive
      ? `2px solid ${colors.primary}`
      : "2px solid transparent",
    transition: "color 0.15s ease, border-bottom-color 0.15s ease",
  });

  return (
    <div
      style={{
        minHeight: "100vh",
        display: "flex",
        flexDirection: "column",
        backgroundColor: colors.background,
        fontFamily: typography.fontFamily,
      }}
    >
      <header
        style={{
          backgroundColor: colors.surface,
          borderBottom: `1px solid ${colors.border}`,
          padding: `${spacing.sm}px ${spacing.md}px`,
          position: "sticky",
          top: 0,
          zIndex: 100,
          boxShadow: "0 1px 3px rgba(0, 0, 0, 0.04)",
        }}
      >
        <div
          style={{
            maxWidth: 1120,
            margin: "0 auto",
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            flexWrap: "wrap",
            gap: spacing.md,
          }}
        >
          {/* Brand & Portal Label */}
          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: spacing.sm,
            }}
          >
            <Link
              to="/service-requests"
              style={{
                ...typography.sectionHeading,
                textDecoration: "none",
                color: colors.primary,
                fontWeight: 700,
                letterSpacing: "-0.5px",
              }}
            >
              AssistLK
            </Link>
            <span
              style={{
                ...typography.small,
                backgroundColor: colors.primaryLight,
                color: colors.primary,
                padding: `2px ${spacing.sm}px`,
                borderRadius: radius.pill,
                fontWeight: 600,
              }}
            >
              Customer
            </span>
          </div>

          {/* Navigation Links */}
          <nav
            style={{
              display: "flex",
              alignItems: "center",
              gap: spacing.md,
              flexWrap: "wrap",
            }}
          >
            <NavLink to="/service-requests" end style={navLinkStyle}>
              My Requests
            </NavLink>
            <NavLink to="/service-requests/new" style={navLinkStyle}>
              Create Request
            </NavLink>
          </nav>

          {/* User Profile & Logout */}
          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: spacing.md,
              flexWrap: "wrap",
            }}
          >
            <span
              style={{
                ...typography.body,
                color: colors.textPrimary,
                fontWeight: 600,
              }}
            >
              {user?.fullName ?? "Customer"}
            </span>

            <AppButton
              variant="outline"
              onClick={handleLogout}
              style={{
                minHeight: 36,
                padding: `0 ${spacing.sm}px`,
                fontSize: typography.small.fontSize,
              }}
            >
              Logout
            </AppButton>
          </div>
        </div>
      </header>

      {/* Main Content Area */}
      <main
        style={{
          flex: 1,
          maxWidth: 1120,
          width: "100%",
          margin: "0 auto",
          padding: `${spacing.lg}px ${spacing.md}px`,
          boxSizing: "border-box",
        }}
      >
        <Outlet />
      </main>
    </div>
  );
}

export default CustomerLayout;
