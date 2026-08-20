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

      if (user.role !== "Admin") {
        useAuthStore.getState().logout();
        setError(
          "This web portal is available to administrators only."
        );
        return;
      }

      navigate("/dashboard");
    } catch {
      // Error is already stored in authStore.
    }
  };

  return (
    <div
      style={{
        minHeight: "100vh",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        backgroundColor: colors.background,
        padding: spacing.md,
        fontFamily: typography.fontFamily,
      }}
    >
      <AppCard
        style={{
          width: "100%",
          maxWidth: "420px",
          boxSizing: "border-box",
        }}
      >
        <form onSubmit={handleSubmit}>
          <h1
            style={{
              ...typography.pageTitle,
              marginTop: 0,
              color: colors.textPrimary,
            }}
          >
            AssistLK Admin
          </h1>

          <p
            style={{
              ...typography.body,
              color: colors.textSecondary,
            }}
          >
            Sign in to continue.
          </p>

          <div style={{ marginTop: spacing.lg }}>
            <AppInput
              label="Email"
              type="email"
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
              type="password"
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
  );
}

export default LoginPage;
