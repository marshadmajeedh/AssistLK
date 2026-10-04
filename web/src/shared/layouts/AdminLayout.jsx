import {
  NavLink,
  Outlet,
  useNavigate,
} from "react-router-dom";
import { useAuthStore } from "../auth/authStore";
import assistLkLogo from "../../assets/assistlk-logo.png";
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
          <div className="admin-brand-identity">
            <img
              src={assistLkLogo}
              alt="AssistLK Logo"
              className="admin-brand-logo"
              width={36}
              height={36}
            />
            <div className="admin-brand-text">
              <span className="admin-brand-name">AssistLK</span>
              <span className="admin-brand-subtitle">Admin Operations</span>
            </div>
          </div>
          <p className="admin-brand-tagline">Trusted service coordination</p>
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

        <div className="admin-sidebar-spacer" aria-hidden="true" />

        <div className="admin-brand-footer">
          <div className="admin-brand-footer-text">
            <span className="admin-brand-footer-name">AssistLK</span>
            <span className="admin-brand-footer-version">Admin Console v1.0</span>
          </div>
          <div className="admin-brand-footer-waves" aria-hidden="true">
            <span className="admin-brand-wave admin-brand-wave-1" />
            <span className="admin-brand-wave admin-brand-wave-2" />
          </div>
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
