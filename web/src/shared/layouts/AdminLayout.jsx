import {
  NavLink,
  Outlet,
  useNavigate,
} from "react-router-dom";
import { useAuthStore } from "../auth/authStore";

import {
  colors,
  spacing,
  typography,
} from "../theme";

function AdminLayout() {
  const navigate = useNavigate();
  const user = useAuthStore((state) => state.user);
  const logout = useAuthStore((state) => state.logout);

  const handleLogout = () => {
    logout();
    navigate("/login", {
      replace: true,
    });
  };

  return (
    <div
      style={{
        minHeight: "100vh",
        display: "flex",
        backgroundColor: colors.background,
        fontFamily: typography.fontFamily,
      }}
    >
      <aside
        style={{
          width: "240px",
          backgroundColor: colors.primaryDark,
          color: colors.surface,
          padding: spacing.lg,
        }}
      >
        <h2>AssistLK</h2>

        <nav
          style={{
            display: "flex",
            flexDirection: "column",
            gap: spacing.md,
            marginTop: spacing.xl,
          }}
        >
          <NavLink to="/dashboard">
            Dashboard
          </NavLink>

          <NavLink to="/service-requests">
            Service Requests
          </NavLink>

          <NavLink to="/providers">
            Providers
          </NavLink>

          <NavLink to="/quotations">
            Quotations
          </NavLink>

          <NavLink to="/service-tracking">
            Service Tracking
          </NavLink>

          <NavLink to="/ai-workflows">
            AI Workflows
          </NavLink>
        </nav>

        <div style={{ marginTop: spacing.xl }}>
          <p>{user?.fullName}</p>

          <button onClick={handleLogout}>
            Logout
          </button>
        </div>
      </aside>

      <main
        style={{
          flex: 1,
          padding: spacing.lg,
        }}
      >
        <Outlet />
      </main>
    </div>
  );
}

export default AdminLayout;
