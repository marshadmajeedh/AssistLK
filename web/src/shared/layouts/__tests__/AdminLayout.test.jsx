import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { describe, it, expect, beforeEach } from "vitest";
import AdminLayout from "../AdminLayout";
import { useAuthStore } from "../../auth/authStore";

describe("AdminLayout", () => {
  beforeEach(() => {
    useAuthStore.setState({
      user: { userId: "admin-1", fullName: "Admin User", role: "Admin" },
      token: "valid-admin-token",
    });
  });

  it("renders top-left AssistLK identity branding block with logo, title, subtitle, and tagline", () => {
    render(
      <MemoryRouter initialEntries={["/dashboard"]}>
        <Routes>
          <Route element={<AdminLayout />}>
            <Route path="/dashboard" element={<div>Dashboard Content</div>} />
          </Route>
        </Routes>
      </MemoryRouter>
    );

    const logo = screen.getByRole("img", { name: /assistlk logo/i });
    expect(logo).toBeInTheDocument();
    expect(logo).toHaveAttribute("src");
    expect(screen.getAllByText("AssistLK")[0]).toBeInTheDocument();
    expect(screen.getByText("Admin Operations")).toBeInTheDocument();
    expect(screen.getByText("Trusted service coordination")).toBeInTheDocument();
    expect(screen.getByText("Dashboard Content")).toBeInTheDocument();
  });

  it("renders navigation links and user full name", () => {
    render(
      <MemoryRouter initialEntries={["/ai-workflows"]}>
        <Routes>
          <Route element={<AdminLayout />}>
            <Route path="/ai-workflows" element={<div>AI Workflows Content</div>} />
          </Route>
        </Routes>
      </MemoryRouter>
    );

    expect(screen.getByRole("link", { name: "Dashboard" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Service Requests" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Providers" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Quotations" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Service Tracking" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "AI Workflows" })).toBeInTheDocument();
    expect(screen.getByText("Admin User")).toBeInTheDocument();
  });

  it("logs out and redirects to /login on logout button click", async () => {
    const user = userEvent.setup();

    render(
      <MemoryRouter initialEntries={["/dashboard"]}>
        <Routes>
          <Route path="/login" element={<div>Login Page After Logout</div>} />
          <Route element={<AdminLayout />}>
            <Route path="/dashboard" element={<div>Dashboard Content</div>} />
          </Route>
        </Routes>
      </MemoryRouter>
    );

    const logoutBtn = screen.getByRole("button", { name: /logout/i });
    await user.click(logoutBtn);

    expect(screen.getByText("Login Page After Logout")).toBeInTheDocument();
    const authState = useAuthStore.getState();
    expect(authState.user).toBeNull();
    expect(authState.token).toBeNull();
  });

  it("renders subtle brand footer at bottom without adding interactive elements", () => {
    render(
      <MemoryRouter initialEntries={["/dashboard"]}>
        <Routes>
          <Route element={<AdminLayout />}>
            <Route path="/dashboard" element={<div>Dashboard Content</div>} />
          </Route>
        </Routes>
      </MemoryRouter>
    );

    // 1. AssistLK branding still renders (both top brand and footer brand)
    const assistLkElements = screen.getAllByText("AssistLK");
    expect(assistLkElements).toHaveLength(2);

    // 2. Navigation items remain unchanged
    expect(screen.getByRole("link", { name: "Dashboard" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Service Requests" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Providers" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Quotations" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Service Tracking" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "AI Workflows" })).toBeInTheDocument();

    // 3. Signed-in user block remains
    expect(screen.getByText("Signed in")).toBeInTheDocument();
    expect(screen.getByText("Admin User")).toBeInTheDocument();

    // 4. Logout button remains
    const buttons = screen.getAllByRole("button");
    expect(buttons).toHaveLength(1);
    expect(screen.getByRole("button", { name: /logout/i })).toBeInTheDocument();

    // 5. Footer renders AssistLK and Admin Console v1.0
    expect(screen.getByText("Admin Console v1.0")).toBeInTheDocument();
    expect(assistLkElements[1]).toHaveClass("admin-brand-footer-name");

    // 6. No new interactive elements added
    const links = screen.getAllByRole("link");
    expect(links).toHaveLength(6);

    // 7. Main content unaffected
    expect(screen.getByText("Dashboard Content")).toBeInTheDocument();
  });
});
