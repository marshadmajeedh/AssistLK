import {
  NavLink,
  Outlet,
  useNavigate,
} from "react-router-dom";
import { useAuthStore } from "../auth/authStore";
import "./AdminLayout.css";

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
      className="admin-layout"
      style={{
        minHeight: "100vh",
        display: "flex",
        backgroundColor: "transparent",
        fontFamily: typography.fontFamily,
      }}
    >
      <aside
        className="admin-sidebar"
        style={{
          width: "290px",
          color: colors.surface,
          padding: spacing.lg,
        }}
      >
        <div className="admin-sidebar-brand">
          <div>
            <div className="admin-eyebrow">AssistLK Operations</div>
            <h2>Trusted service coordination</h2>
          </div>
          <p>Requests, workflows, approvals, and tracking in one workspace.</p>
        </div>

        <nav
          className="admin-nav"
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

          <NavLink to="/admin/service-requests">
            Service Requests
          </NavLink>

          <NavLink to="/admin/providers">
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

        <div className="admin-sidebar-footer" style={{ marginTop: spacing.xl }}>
          <div>
            <div className="admin-eyebrow">Signed in</div>
            <p>{user?.fullName}</p>
          </div>

          <button className="admin-logout" onClick={handleLogout}>
            Logout
          </button>
        </div>
      </aside>

      <main
        className="admin-main"
        style={{
          flex: 1,
          minWidth: 0,
          padding: spacing.lg,
        }}
      >
        <div className="app-shell app-page">
          <Outlet />
        </div>
      </main>
    </div>
  );
}

export default AdminLayout;
