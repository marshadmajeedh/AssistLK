
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
    <div className="login-page">
      <div className="login-backdrop" />
      <div className="login-grid">
        <section className="login-showcase">
          <div>
            <div className="pill-note" style={{ backgroundColor: "rgba(255,255,255,0.12)", color: "#FFFFFF", borderColor: "rgba(255,255,255,0.22)" }}>
              AssistLK Portal
            </div>
            <h1 className="login-title">Enterprise Service Operations Portal</h1>
            <p className="login-copy">
              Centralized management for customer service requests, provider matching, quotations, and live service coordination.
            </p>
          </div>

          <div className="login-showcase-grid">
            <div className="login-showcase-card">
              <strong>Intelligent Dispatch</strong>
              <span>Automated technician matching based on location, availability, and skills</span>
            </div>
            <div className="login-showcase-card">
              <strong>Unified Operations</strong>
              <span>End-to-end management from request triage to verified job completion</span>
            </div>
            <div className="login-showcase-card">
              <strong>Verified Network</strong>
              <span>Real-time provider readiness, credential audits, and transparent operations</span>
            </div>
          </div>
        </section>

        <AppCard className="login-card">
          <form onSubmit={handleSubmit} style={{ width: "100%" }}>
            <div className="pill-note">Admin Console</div>
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
                marginTop: spacing.xs,
                marginBottom: spacing.xl,
              }}
            >
              Sign in to continue into the authorized web portal.
            </p>

            <div style={{ marginTop: spacing.md }}>
              <AppInput
                label="Email"
                name="email"
                type="email"
                autoComplete="username"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                required
              />
            </div>

            <div style={{ marginTop: spacing.md }}>
              <AppInput
                label="Password"
                name="password"
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                required
              />
            </div>

            <ErrorMessage message={error} />

            <AppButton
              type="submit"
              disabled={loading}
              style={{
                width: "100%",
                marginTop: spacing.xl,
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
