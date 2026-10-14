import { render, screen } from "@testing-library/react";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { describe, it, expect, beforeEach } from "vitest";
import ProtectedRoute from "../ProtectedRoute";
import { useAuthStore } from "../authStore";

/**
 * TEST SUITE: Protected Route Guard Component
 *
 * 1. What this class/suite tests:
 *    - Verifies the React Router wrapper component (`ProtectedRoute`) that enforces client-side
 *      authentication and Role-Based Access Control (RBAC).
 * 2. Why these tests are needed:
 *    - Unauthenticated guests must be bounced to `/login` with preservation of redirect state.
 *    - Authenticated users lacking the required role must be redirected to `/unauthorized`
 *      to prevent unauthorized access to sensitive portals (e.g. non-admins entering admin routes).
 * 3. What is real and what is mocked/faked:
 *    - Real: Full React Router route resolution and component rendering via React Testing Library.
 *    - Mocked: Zustand authentication store state (`useAuthStore.setState`) is manipulated directly.
 * 4. Prerequisites:
 *    - Node.js dependencies installed (`npm install` inside `/web`).
 * 5. PowerShell command to run this suite from repository root:
 *    npm --prefix web test -- src/shared/auth/__tests__/ProtectedRoute.test.jsx
 */
describe("ProtectedRoute Unit Tests", () => {
  beforeEach(() => {
    useAuthStore.setState({ user: null, token: null });
  });

  function renderWithRoutes(allowedRoles) {
    return render(
      <MemoryRouter initialEntries={["/protected"]}>
        <Routes>
          <Route path="/login" element={<div>Login Page Target</div>} />
          <Route path="/unauthorized" element={<div>Unauthorized Target</div>} />
          <Route element={<ProtectedRoute allowedRoles={allowedRoles} />}>
            <Route path="/protected" element={<div>Protected Secret Content</div>} />
          </Route>
        </Routes>
      </MemoryRouter>
    );
  }

  /**
   * Purpose: What this specific test checks
   * - Verifies that an unauthenticated guest (null token/user) navigating to a protected route
   *   is automatically redirected to `/login`.
   *
   * Why: Why this scenario matters
   * - Essential security requirement preventing guests from seeing protected UI or data.
   *
   * Scenario: Input or condition being tested
   * - `user: null`, `token: null`, attempting to access `/protected`.
   *
   * Pass condition: What result or assertion makes it pass
   * - "Login Page Target" is in the document and "Protected Secret Content" is omitted.
   *
   * Run from repository root:
   * npm --prefix web test -- src/shared/auth/__tests__/ProtectedRoute.test.jsx -t "redirects unauthenticated guest to /login"
   */
  it("redirects unauthenticated guest to /login", () => {
    renderWithRoutes(["Customer"]);
    expect(screen.getByText("Login Page Target")).toBeInTheDocument();
    expect(screen.queryByText("Protected Secret Content")).not.toBeInTheDocument();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that an authenticated user possessing an authorized role renders the child route's content.
   *
   * Why: Why this scenario matters
   * - Legitimate users must be allowed access to their authorized features without interruption.
   *
   * Scenario: Input or condition being tested
   * - User role "Customer" with valid token accesses route allowing `["Customer"]`.
   *
   * Pass condition: What result or assertion makes it pass
   * - "Protected Secret Content" is rendered in the document.
   *
   * Run from repository root:
   * npm --prefix web test -- src/shared/auth/__tests__/ProtectedRoute.test.jsx -t "renders protected content when user has required role"
   */
  it("renders protected content when user has required role", () => {
    useAuthStore.setState({
      user: { role: "Customer", fullName: "Alice" },
      token: "valid-token",
    });

    renderWithRoutes(["Customer"]);
    expect(screen.getByText("Protected Secret Content")).toBeInTheDocument();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that an authenticated user who lacks the allowed role is redirected to `/unauthorized`.
   *
   * Why: Why this scenario matters
   * - RBAC isolation: an Admin account should not access Customer-only routes, and a Customer account
   *   should not access Admin routes.
   *
   * Scenario: Input or condition being tested
   * - User role "Admin" accesses route requiring `["Customer"]`.
   *
   * Pass condition: What result or assertion makes it pass
   * - "Unauthorized Target" is displayed and "Protected Secret Content" is not rendered.
   *
   * Run from repository root:
   * npm --prefix web test -- src/shared/auth/__tests__/ProtectedRoute.test.jsx -t "redirects to /unauthorized when user does not have required role"
   */
  it("redirects to /unauthorized when user does not have required role", () => {
    useAuthStore.setState({
      user: { role: "Admin", fullName: "Bob Admin" },
      token: "valid-token",
    });

    renderWithRoutes(["Customer"]);
    expect(screen.getByText("Unauthorized Target")).toBeInTheDocument();
    expect(screen.queryByText("Protected Secret Content")).not.toBeInTheDocument();
  });
});
