import { render, screen, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import AppRouter from "../AppRouter";
import { useAuthStore } from "../../../shared/auth/authStore";
import agentMonitoringService from "../../../features/aiWorkflows/services/agentMonitoringService";

vi.mock("../../../features/aiWorkflows/services/agentMonitoringService", () => ({
  default: { getMetrics: vi.fn().mockResolvedValue([]) },
}));

vi.mock("../../../features/admin/services/adminServiceRequestService", () => ({
  default: { getAll: vi.fn().mockResolvedValue([]), getById: vi.fn() },
}));

vi.mock("../../../features/serviceRequests/services/serviceRequestService", () => ({
  default: {
    getMyRequests: vi.fn().mockResolvedValue([]),
    getById: vi.fn().mockResolvedValue({ serviceRequestId: "1", status: "Created" }),
  },
}));

/**
 * TEST SUITE: Application Router Role Boundaries and Routing Guard
 *
 * 1. What this class/suite tests:
 *    - Verifies the top-level application routing hierarchy (`AppRouter`), strictly enforcing
 *      client-side Role-Based Access Control (RBAC) boundaries between Unauthenticated Guests,
 *      Customers, Service Providers, and Administrators.
 * 2. Why these tests are needed:
 *    - Validates that unauthorized roles and unauthenticated guests are bounced immediately
 *      to `/login` or `/unauthorized` before components mount or protected backend API calls
 *      are triggered (preventing unauthorized telemetry and data leakage).
 * 3. What is real and what is mocked/faked:
 *    - Real: Top-level `AppRouter` configuration, browser HTML5 History API simulation via `window.history.pushState`,
 *      and DOM assertions via React Testing Library.
 *    - Mocked: Backend service clients (`adminServiceRequestService`, `serviceRequestService`,
 *      `agentMonitoringService`) return resolved promises so pages mount cleanly when authorized.
 * 4. Prerequisites:
 *    - Node.js dependencies installed (`npm install` inside `/web`).
 * 5. PowerShell command to run this suite from repository root:
 *    npm --prefix web test -- src/app/router/__tests__/AppRouter.test.jsx
 */
describe("AppRouter Role Boundaries and Routing", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.setState({ user: null, token: null });
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that an unauthenticated guest navigating to `/service-requests` is redirected to `/login`.
   *
   * Why: Why this scenario matters
   * - Core protection ensuring private customer requests cannot be accessed anonymously.
   *
   * Scenario: Input or condition being tested
   * - Guest (null user/token) attempts to access `/service-requests`.
   *
   * Pass condition: What result or assertion makes it pass
   * - `window.location.pathname` transitions to `/login`.
   *
   * Run from repository root:
   * npm --prefix web test -- src/app/router/__tests__/AppRouter.test.jsx -t "redirects guest from /service-requests to /login"
   */
  it("redirects guest from /service-requests to /login", async () => {
    window.history.pushState({}, "Test", "/service-requests");
    render(<AppRouter />);

    await waitFor(() => {
      expect(window.location.pathname).toBe("/login");
    });
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that an authenticated Customer can access `/service-requests` and render the page heading.
   *
   * Why: Why this scenario matters
   * - Confirms legitimate customers are permitted into the service request management view.
   *
   * Scenario: Input or condition being tested
   * - User role "Customer" accesses `/service-requests`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Heading "My Service Requests" appears and pathname remains `/service-requests`.
   *
   * Run from repository root:
   * npm --prefix web test -- src/app/router/__tests__/AppRouter.test.jsx -t "allows Customer on /service-requests"
   */
  it("allows Customer on /service-requests", async () => {
    useAuthStore.setState({
      user: { userId: "cust-1", fullName: "Jane Customer", role: "Customer" },
      token: "valid-customer-token",
    });

    window.history.pushState({}, "Test", "/service-requests");
    render(<AppRouter />);

    expect(
      await screen.findByRole("heading", { name: /my service requests/i })
    ).toBeInTheDocument();
    expect(window.location.pathname).toBe("/service-requests");
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that a Customer attempting to access the Admin `/dashboard` is blocked and redirected to `/unauthorized`.
   *
   * Why: Why this scenario matters
   * - Boundary enforcement: customers must not access administrative dashboard metrics.
   *
   * Scenario: Input or condition being tested
   * - User role "Customer" navigates to `/dashboard`.
   *
   * Pass condition: What result or assertion makes it pass
   * - "Unauthorized" heading renders and pathname is `/unauthorized`.
   *
   * Run from repository root:
   * npm --prefix web test -- src/app/router/__tests__/AppRouter.test.jsx -t "rejects Customer from Admin /dashboard"
   */
  it("rejects Customer from Admin /dashboard and redirects to /unauthorized", async () => {
    useAuthStore.setState({
      user: { userId: "cust-1", fullName: "Jane Customer", role: "Customer" },
      token: "valid-customer-token",
    });

    window.history.pushState({}, "Test", "/dashboard");
    render(<AppRouter />);

    expect(
      await screen.findByRole("heading", { name: /unauthorized/i })
    ).toBeInTheDocument();
    expect(window.location.pathname).toBe("/unauthorized");
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that an Admin user can access `/dashboard` and view the dashboard heading.
   *
   * Why: Why this scenario matters
   * - Confirms authorized administrative access to executive operations.
   *
   * Scenario: Input or condition being tested
   * - User role "Admin" accesses `/dashboard`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Heading "Dashboard" appears and pathname is `/dashboard`.
   *
   * Run from repository root:
   * npm --prefix web test -- src/app/router/__tests__/AppRouter.test.jsx -t "allows Admin on /dashboard"
   */
  it("allows Admin on /dashboard", async () => {
    useAuthStore.setState({
      user: { userId: "admin-1", fullName: "Admin User", role: "Admin" },
      token: "valid-admin-token",
    });

    window.history.pushState({}, "Test", "/dashboard");
    render(<AppRouter />);

    expect(
      await screen.findByRole("heading", { name: /^dashboard$/i })
    ).toBeInTheDocument();
    expect(window.location.pathname).toBe("/dashboard");
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that an Admin attempting to access customer-only `/service-requests` is redirected to `/unauthorized`.
   *
   * Why: Why this scenario matters
   * - Strict portal separation: admins use `/admin/service-requests` for monitoring rather than customer creation queues.
   *
   * Scenario: Input or condition being tested
   * - User role "Admin" navigates to `/service-requests`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Heading "Unauthorized" appears and pathname is `/unauthorized`.
   *
   * Run from repository root:
   * npm --prefix web test -- src/app/router/__tests__/AppRouter.test.jsx -t "rejects Admin from customer-only /service-requests"
   */
  it("rejects Admin from customer-only /service-requests and redirects to /unauthorized", async () => {
    useAuthStore.setState({
      user: { userId: "admin-1", fullName: "Admin User", role: "Admin" },
      token: "valid-admin-token",
    });

    window.history.pushState({}, "Test", "/service-requests");
    render(<AppRouter />);

    expect(
      await screen.findByRole("heading", { name: /unauthorized/i })
    ).toBeInTheDocument();
    expect(window.location.pathname).toBe("/unauthorized");
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that an Admin can access the authoritative admin monitoring list at `/admin/service-requests`.
   *
   * Why: Why this scenario matters
   * - Ensures admins have access to system-wide request monitoring.
   *
   * Scenario: Input or condition being tested
   * - User role "Admin" accesses `/admin/service-requests`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Heading "Service Request Monitoring" appears, pathname is `/admin/service-requests`, and mock owner badges are absent.
   *
   * Run from repository root:
   * npm --prefix web test -- src/app/router/__tests__/AppRouter.test.jsx -t "allows Admin on /admin/service-requests"
   */
  it("allows Admin on /admin/service-requests", async () => {
    useAuthStore.setState({
      user: { userId: "admin-1", fullName: "Admin User", role: "Admin" },
      token: "valid-admin-token",
    });

    window.history.pushState({}, "Test", "/admin/service-requests");
    render(<AppRouter />);

    expect(
      await screen.findByRole("heading", { name: /service request monitoring/i })
    ).toBeInTheDocument();
    expect(window.location.pathname).toBe("/admin/service-requests");
    expect(screen.queryByText(/assigned component owner/i)).not.toBeInTheDocument();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that an Admin can access the live `/ai-workflows` monitoring page and that metrics are fetched.
   *
   * Why: Why this scenario matters
   * - Confirms proper mounting and API trigger for the AI agent monitoring subsystem.
   *
   * Scenario: Input or condition being tested
   * - User role "Admin" navigates to `/ai-workflows`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Heading "AI Workflow Monitoring" appears, empty message is present, and `agentMonitoringService.getMetrics` is called.
   *
   * Run from repository root:
   * npm --prefix web test -- src/app/router/__tests__/AppRouter.test.jsx -t "allows Admin on the real /ai-workflows page"
   */
  it("allows Admin on the real /ai-workflows page", async () => {
    useAuthStore.setState({ user: { role: "Admin", fullName: "Admin User" }, token: "valid-admin-token" });
    window.history.pushState({}, "Test", "/ai-workflows");
    render(<AppRouter />);
    expect(await screen.findByRole("heading", { name: "AI Workflow Monitoring" })).toBeInTheDocument();
    expect(await screen.findByText("No AI workflow executions have been recorded yet.")).toBeInTheDocument();
    expect(screen.queryByText(/assigned component owner/i)).not.toBeInTheDocument();
    expect(window.location.pathname).toBe("/ai-workflows");
    expect(agentMonitoringService.getMetrics).toHaveBeenCalledTimes(1);
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that unauthorized roles (Customer, Provider) or Guests (null) are blocked from `/ai-workflows`
   *   BEFORE any service API call is made to fetch metrics.
   *
   * Why: Why this scenario matters
   * - Essential security guard: sensitive agent telemetry must not be queried or transmitted over the wire
   *   when the user lacks administrative clearance.
   *
   * Scenario: Input or condition being tested
   * - Roles "Customer", "Provider", and null attempt to access `/ai-workflows`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Redirected to `/unauthorized` or `/login`, and `agentMonitoringService.getMetrics` is never called (0 calls).
   *
   * Run from repository root (parameterized test - runs all 3 roles):
   * npm --prefix web test -- src/app/router/__tests__/AppRouter.test.jsx -t "blocks .* from /ai-workflows before fetching metrics"
   */
  it.each(["Customer", "Provider", null])("blocks %s from /ai-workflows before fetching metrics", async (role) => {
    if (role) useAuthStore.setState({ user: { role }, token: "valid-token" });
    window.history.pushState({}, "Test", "/ai-workflows");
    render(<AppRouter />);
    await waitFor(() => expect(window.location.pathname).toBe(role ? "/unauthorized" : "/login"));
    expect(screen.queryByRole("heading", { name: "AI Workflow Monitoring" })).not.toBeInTheDocument();
    expect(agentMonitoringService.getMetrics).not.toHaveBeenCalled();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that unauthorized roles (Customer, Provider) or Guests (null) are blocked from `/admin/service-requests`.
   *
   * Why: Why this scenario matters
   * - Protects administrative monitoring lists from non-admin access.
   *
   * Scenario: Input or condition being tested
   * - Roles "Customer", "Provider", and null attempt to access `/admin/service-requests`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Redirected to `/unauthorized` or `/login`, and "Service Request Monitoring" heading is omitted.
   *
   * Run from repository root (parameterized test - runs all 3 roles):
   * npm --prefix web test -- src/app/router/__tests__/AppRouter.test.jsx -t "rejects .* from Admin service request monitoring"
   */
  it.each(["Customer", "Provider", null])("rejects %s from Admin service request monitoring", async (role) => {
    if (role) useAuthStore.setState({ user: { role }, token: "valid-token" });
    window.history.pushState({}, "Test", "/admin/service-requests");
    render(<AppRouter />);
    await waitFor(() => expect(window.location.pathname).toBe(role ? "/unauthorized" : "/login"));
    expect(screen.queryByRole("heading", { name: /service request monitoring/i })).not.toBeInTheDocument();
  });
});
