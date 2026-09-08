import { render, screen, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import AppRouter from "../AppRouter";
import { useAuthStore } from "../../../shared/auth/authStore";

vi.mock("../../../features/serviceRequests/services/serviceRequestService", () => ({
  default: {
    getMyRequests: vi.fn().mockResolvedValue([]),
    getById: vi.fn().mockResolvedValue({ serviceRequestId: "1", status: "Created" }),
  },
}));

describe("AppRouter Role Boundaries and Routing", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.setState({ user: null, token: null });
  });

  it("redirects guest from /service-requests to /login", async () => {
    window.history.pushState({}, "Test", "/service-requests");
    render(<AppRouter />);

    await waitFor(() => {
      expect(window.location.pathname).toBe("/login");
    });
  });

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

  it("allows Admin on /admin/service-requests", async () => {
    useAuthStore.setState({
      user: { userId: "admin-1", fullName: "Admin User", role: "Admin" },
      token: "valid-admin-token",
    });

    window.history.pushState({}, "Test", "/admin/service-requests");
    render(<AppRouter />);

    expect(
      await screen.findByRole("heading", { name: /admin service requests/i })
    ).toBeInTheDocument();
    expect(window.location.pathname).toBe("/admin/service-requests");
  });
});
