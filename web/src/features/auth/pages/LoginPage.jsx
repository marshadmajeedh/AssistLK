import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuthStore } from "../../../shared/auth/authStore";
import assistLkLogo from "../../../assets/assistlk-logo.png";
import ErrorMessage from "../../../shared/components/ErrorMessage";
import "./LoginPage.css";

function LoginPage() {
  const navigate = useNavigate();

  const login = useAuthStore((state) => state.login);
  const setError = useAuthStore((state) => state.setError);
  const loading = useAuthStore((state) => state.loading);
  const error = useAuthStore((state) => state.error);

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);

  const handleSubmit = async (event) => {
    event.preventDefault();

    try {
      const user = await login(email, password);

      if (user.role === "Admin") {
        navigate("/dashboard");
      } else {
        useAuthStore.getState().logout();
        setError(
          `Role '${user.role}' is not authorized to access this portal.`
        );
      }
    } catch {
      // Error is already stored in authStore.
    }
  };

  return (
    <div className="admin-login-page">
      <div className="admin-login-shell">
        {/* ================================================================
            LEFT BRAND PANEL
            ================================================================ */}
        <section
          className="admin-brand-panel"
          aria-label="AssistLK Portal Branding"
        >
          {/* Ambient Arcs Decoration */}
          <svg
            className="brand-ambient-arcs"
            viewBox="0 0 700 700"
            fill="none"
            xmlns="http://www.w3.org/2000/svg"
            aria-hidden="true"
          >
            <circle
              cx="650"
              cy="80"
              r="200"
              stroke="rgba(255, 255, 255, 0.06)"
              strokeWidth="2"
            />
            <circle
              cx="650"
              cy="80"
              r="340"
              stroke="rgba(255, 255, 255, 0.05)"
              strokeWidth="2"
            />
            <circle
              cx="650"
              cy="80"
              r="480"
              stroke="rgba(255, 255, 255, 0.04)"
              strokeWidth="2"
            />
            <circle
              cx="650"
              cy="80"
              r="620"
              stroke="rgba(255, 255, 255, 0.03)"
              strokeWidth="2"
            />
          </svg>

          <div className="brand-content-top">
            {/* Top Branding: Logo + Wordmark */}
            <div className="brand-identity-row">
              <img
                src={assistLkLogo}
                alt="AssistLK Logo"
                className="brand-logo"
                width={52}
                height={52}
              />
              <span className="brand-logo-text">AssistLK</span>
            </div>

            {/* Accent divider line */}
            <div className="brand-accent-line" aria-hidden="true" />

            {/* Main Heading */}
            <h1 className="brand-hero-title">
              <span className="brand-title-primary">AssistLK</span>
              <span className="brand-title-accent">Admin Portal</span>
            </h1>

            {/* Tagline */}
            <p className="brand-tagline">
              Trusted service operations, smarter coordination.
            </p>

            {/* Short supporting description */}
            <p className="brand-description">
              Manage service requests, provider matching, quotations, and live
              tracking — all in one secure workspace.
            </p>

            {/* Four Compact Feature Items */}
            <div className="brand-feature-row">
              <div className="brand-feature-item">
                <div className="brand-feature-icon-box" aria-hidden="true">
                  <svg
                    width="26"
                    height="26"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  >
                    <rect x="3" y="3" width="18" height="18" rx="2" />
                    <line x1="8" y1="8" x2="16" y2="8" />
                    <line x1="8" y1="12" x2="16" y2="12" />
                    <line x1="8" y1="16" x2="13" y2="16" />
                  </svg>
                </div>
                <span className="brand-feature-label">Service Requests</span>
              </div>

              <div className="brand-feature-item">
                <div className="brand-feature-icon-box" aria-hidden="true">
                  <svg
                    width="26"
                    height="26"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  >
                    <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
                    <circle cx="9" cy="7" r="4" />
                    <path d="M22 21v-2a4 4 0 0 0-3-3.87" />
                    <path d="M16 3.13a4 4 0 0 1 0 7.75" />
                  </svg>
                </div>
                <span className="brand-feature-label">Provider Matching</span>
              </div>

              <div className="brand-feature-item">
                <div className="brand-feature-icon-box" aria-hidden="true">
                  <svg
                    width="26"
                    height="26"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  >
                    <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                    <polyline points="14 2 14 8 20 8" />
                    <line x1="16" y1="13" x2="8" y2="13" />
                    <line x1="16" y1="17" x2="8" y2="17" />
                    <polyline points="10 9 9 9 8 9" />
                  </svg>
                </div>
                <span className="brand-feature-label">
                  Quotations & Approvals
                </span>
              </div>

              <div className="brand-feature-item">
                <div className="brand-feature-icon-box" aria-hidden="true">
                  <svg
                    width="26"
                    height="26"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  >
                    <path d="M12 2a8 8 0 0 0-8 8c0 5.25 8 12 8 12s8-6.75 8-12a8 8 0 0 0-8-8z" />
                    <circle cx="12" cy="10" r="3" />
                  </svg>
                </div>
                <span className="brand-feature-label">Live Tracking</span>
              </div>
            </div>
          </div>

          {/* LOWER OPERATIONS ILLUSTRATION (Decorative only) */}
          <div className="brand-illustration-container" aria-hidden="true">
            {/* Ambient light glow behind cards */}
            <div className="illustration-ambient-glow" />
            <div className="illustration-foliage-left" />
            <div className="illustration-foliage-right" />

            {/* Dashboard Console Card (Tablet perspective) */}
            <div className="illustration-dashboard-card">
              <div className="dashboard-card-header">
                <div className="dashboard-logo-dot" />
                <span className="dashboard-card-title">Service Requests</span>
                <div className="dashboard-header-placeholder" />
              </div>
              <div className="dashboard-card-rows">
                <div className="dashboard-row">
                  <div className="dashboard-line-group">
                    <span className="dash-bar long" />
                    <span className="dash-bar short" />
                  </div>
                  <span className="dash-badge badge-pending">Pending</span>
                  <span className="dash-pill" />
                </div>
                <div className="dashboard-row">
                  <div className="dashboard-line-group">
                    <span className="dash-bar long" />
                    <span className="dash-bar medium" />
                  </div>
                  <span className="dash-badge badge-matched">Matched</span>
                  <span className="dash-pill" />
                </div>
                <div className="dashboard-row">
                  <div className="dashboard-line-group">
                    <span className="dash-bar medium" />
                    <span className="dash-bar short" />
                  </div>
                  <span className="dash-badge badge-completed">Completed</span>
                  <span className="dash-pill" />
                </div>
                <div className="dashboard-row">
                  <div className="dashboard-line-group">
                    <span className="dash-bar long" />
                    <span className="dash-bar short" />
                  </div>
                  <span className="dash-badge badge-matched">Dispatched</span>
                  <span className="dash-pill" />
                </div>
              </div>
            </div>

            {/* Floating Map/Tracking Card */}
            <div className="illustration-map-card">
              <div className="map-badge-card">
                <div className="map-pin-icon">
                  <svg
                    width="18"
                    height="18"
                    viewBox="0 0 24 24"
                    fill="#0284c7"
                  >
                    <path d="M12 2a8 8 0 0 0-8 8c0 5.25 8 12 8 12s8-6.75 8-12a8 8 0 0 0-8-8z" />
                    <circle cx="12" cy="10" r="3" fill="#ffffff" />
                  </svg>
                </div>
                <div className="map-badge-lines">
                  <span className="map-badge-bar long" />
                  <span className="map-badge-bar short" />
                </div>
              </div>

              {/* Map Route SVG */}
              <svg
                className="map-route-svg"
                viewBox="0 0 240 140"
                fill="none"
                xmlns="http://www.w3.org/2000/svg"
              >
                <path
                  d="M 35 115 Q 95 100 120 80 T 195 35"
                  stroke="#38bdf8"
                  strokeWidth="4"
                  strokeLinecap="round"
                  filter="drop-shadow(0 0 8px rgba(56, 189, 248, 0.75))"
                />
                <circle
                  cx="35"
                  cy="115"
                  r="7"
                  fill="#0284c7"
                  stroke="#ffffff"
                  strokeWidth="2.5"
                />
                <circle
                  cx="120"
                  cy="80"
                  r="5"
                  fill="#38bdf8"
                  stroke="#ffffff"
                  strokeWidth="2"
                />
                <circle
                  cx="195"
                  cy="35"
                  r="9"
                  fill="#0284c7"
                  stroke="#ffffff"
                  strokeWidth="3"
                />
                <circle
                  cx="195"
                  cy="35"
                  r="16"
                  stroke="rgba(56, 189, 248, 0.5)"
                  strokeWidth="2"
                />
              </svg>
            </div>

            {/* Floating Appliance Repair Glass Card */}
            <div className="illustration-floating-service-card">
              <div className="service-card-icon-box">
                <svg
                  width="22"
                  height="22"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="#ffffff"
                  strokeWidth="2.2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                >
                  <path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z" />
                </svg>
              </div>
              <div className="service-card-text">
                <span className="service-card-title">Appliance Repair</span>
                <span className="service-card-bar" />
              </div>
            </div>
          </div>
        </section>

        {/* ================================================================
            RIGHT LOGIN PANEL
            ================================================================ */}
        <section className="admin-login-panel" aria-label="Admin Sign In Form">
          <div className="login-panel-content">
            <div className="admin-console-pill">ADMIN CONSOLE</div>

            <h2 className="admin-login-title">AssistLK</h2>

            <p className="admin-login-subtitle">
              Sign in to continue to the authorized admin console.
            </p>

            <form
              onSubmit={handleSubmit}
              className="admin-login-form"
              aria-label="Admin Sign In"
            >
              {/* Email Field */}
              <div className="login-form-field">
                <label htmlFor="admin-email" className="login-form-label">
                  Email
                </label>
                <div className="login-input-container">
                  <span className="login-input-icon" aria-hidden="true">
                    <svg
                      width="20"
                      height="20"
                      viewBox="0 0 24 24"
                      fill="none"
                      stroke="currentColor"
                      strokeWidth="1.8"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                    >
                      <rect x="2" y="4" width="20" height="16" rx="2" />
                      <path d="m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7" />
                    </svg>
                  </span>
                  <input
                    id="admin-email"
                    name="email"
                    type="email"
                    autoComplete="username"
                    className="login-input"
                    placeholder="Enter your email address"
                    value={email}
                    onChange={(event) => setEmail(event.target.value)}
                    required
                  />
                </div>
              </div>

              {/* Password Field */}
              <div className="login-form-field">
                <label htmlFor="admin-password" className="login-form-label">
                  Password
                </label>
                <div className="login-input-container">
                  <span className="login-input-icon" aria-hidden="true">
                    <svg
                      width="20"
                      height="20"
                      viewBox="0 0 24 24"
                      fill="none"
                      stroke="currentColor"
                      strokeWidth="1.8"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                    >
                      <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
                      <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                    </svg>
                  </span>
                  <input
                    id="admin-password"
                    name="password"
                    type={showPassword ? "text" : "password"}
                    autoComplete="current-password"
                    className="login-input login-input-with-toggle"
                    placeholder="Enter your password"
                    value={password}
                    onChange={(event) => setPassword(event.target.value)}
                    required
                  />
                  <button
                    type="button"
                    className="password-toggle-btn"
                    onClick={() => setShowPassword((prev) => !prev)}
                    aria-label={showPassword ? "Hide password" : "Show password"}
                  >
                    {showPassword ? (
                      <svg
                        width="20"
                        height="20"
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="1.8"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                      >
                        <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24" />
                        <line x1="1" y1="1" x2="23" y2="23" />
                      </svg>
                    ) : (
                      <svg
                        width="20"
                        height="20"
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="1.8"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                      >
                        <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                        <circle cx="12" cy="12" r="3" />
                      </svg>
                    )}
                  </button>
                </div>
              </div>

              {/* Error Message */}
              <ErrorMessage message={error} />

              {/* Sign In Button */}
              <button
                type="submit"
                className="admin-login-submit-btn"
                disabled={loading}
              >
                {loading ? "Signing in..." : "Sign In"}
              </button>
            </form>
          </div>
        </section>
      </div>
    </div>
  );
}

export default LoginPage;
