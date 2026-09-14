
import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuthStore } from "../../../shared/auth/authStore";
import AppButton from "../../../shared/components/AppButton";
import AppInput from "../../../shared/components/AppInput";
import AppCard from "../../../shared/components/AppCard";
import ErrorMessage from "../../../shared/components/ErrorMessage";

import {
  colors,
  spacing,
  typography,
} from "../../../shared/theme";

function LoginPage() {
  const navigate = useNavigate();

  const login = useAuthStore((state) => state.login);
  const setError = useAuthStore(
    (state) => state.setError
  );
  const loading = useAuthStore((state) => state.loading);
  const error = useAuthStore((state) => state.error);

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");

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
    <div className="login-page" style={{ minHeight: "100vh", padding: spacing.md, fontFamily: typography.fontFamily }}>
      <div className="login-backdrop" />
      <div className="login-grid app-shell">
        <section className="login-showcase">
          <div className="page-kicker">AssistLK Portal</div>
          <h1 className="login-title">Bring every service decision into one trusted workspace.</h1>
          <p className="login-copy">
            Track service requests, AI analysis, provider readiness, and audit-friendly operations from a single, role-protected portal.
          </p>
          <div className="login-showcase-grid">
            <div className="login-showcase-card">
              <strong>4 workstreams</strong>
              <span>Problem understanding, matching, booking, and tracking</span>
            </div>
            <div className="login-showcase-card">
              <strong>Shared API rules</strong>
              <span>Web and mobile follow one identity and approval model</span>
            </div>
            <div className="login-showcase-card">
              <strong>Live oversight</strong>
              <span>Surface AI workflow metrics and customer request status instantly</span>
            </div>
          </div>
        </section>

      <AppCard
        className="login-card"
        style={{
          width: "100%",
          maxWidth: "420px",
          boxSizing: "border-box",
        }}
      >
        <form onSubmit={handleSubmit}>
          <div className="pill-note">Staff Workspace</div>
          <h1
            style={{
              ...typography.pageTitle,
              marginTop: spacing.md,
              color: colors.textPrimary,
            }}
          >
            AssistLK
          </h1>

          <p
            style={{
              ...typography.body,
              color: colors.textSecondary,
            }}
          >
            Sign in to continue into the authorized web portal.
          </p>

          <div style={{ marginTop: spacing.lg }}>
            <AppInput
              label="Email"
              name="email"
              type="email"
              autoComplete="username"
              value={email}
              onChange={(event) =>
                setEmail(event.target.value)
              }
              required
            />
          </div>

          <div
            style={{
              marginTop: spacing.md,
            }}
          >
            <AppInput
              label="Password"
              name="password"
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(event) =>
                setPassword(event.target.value)
              }
              required
            />
          </div>

          <ErrorMessage message={error} />

          <AppButton
            type="submit"
            disabled={loading}
            style={{
              width: "100%",
              marginTop: spacing.lg,
            }}
          >
            {loading ? "Signing in..." : "Sign In"}
          </AppButton>
        </form>
      </AppCard>
      </div>
    </div>
  );
}

export default LoginPage;
