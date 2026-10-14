import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { describe, it, expect, beforeEach } from "vitest";
import AdminLayout from "../AdminLayout";
import { useAuthStore } from "../../auth/authStore";

/**
 * TEST SUITE: Admin Portal Layout Component
 *
 * 1. What this class/suite tests:
 *    - Verifies the administrator shell layout (`AdminLayout`), including sidebar navigation links,
 *      top-left AssistLK branding block (logo, title, subtitle, tagline), user identity block,
 *      interactive logout flow, and non-interactive footer presentation.
 * 2. Why these tests are needed:
 *    - The admin layout encapsulates all administrative views with persistent navigation and
 *      authenticated session controls. Tests verify that navigation items route correctly,
 *      logout terminates the session cleanly, and presentational elements do not disrupt accessibility.
 * 3. What is real and what is mocked/faked:
 *    - Real: React Router navigation, userEvent clicks, DOM querying via React Testing Library.
 *    - Mocked: Auth store state (`useAuthStore`) initialized with an Admin session.
 * 4. Prerequisites:
 *    - Node.js dependencies installed (`npm install` inside `/web`).
 * 5. PowerShell command to run this suite from repository root:
 *    npm --prefix web test -- src/shared/layouts/__tests__/AdminLayout.test.jsx
 */
describe("AdminLayout", () => {
  beforeEach(() => {
    useAuthStore.setState({
      user: { userId: "admin-1", fullName: "Admin User", role: "Admin" },
      token: "valid-admin-token",
    });
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that the top-left sidebar header displays the AssistLK logo image,
   *   "AssistLK" brand title, "Admin Operations" subtitle, and tagline.
   *
   * Why: Why this scenario matters
   * - Ensures clear branding and system context for portal administrators.
   *
   * Scenario: Input or condition being tested
   * - Render `<AdminLayout />` wrapping a `/dashboard` child route.
   *
   * Pass condition: What result or assertion makes it pass
   * - Logo has accessible name and src, and brand text nodes are in the document.
   *
   * Run from repository root:
   * npm --prefix web test -- src/shared/layouts/__tests__/AdminLayout.test.jsx -t "renders top-left AssistLK identity branding block"
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that navigation links to Dashboard, Service Requests, Providers, Quotations,
   *   Service Tracking, and AI Workflows are rendered alongside the logged-in administrator's name.
   *
   * Why: Why this scenario matters
   * - Complete navigation bar enables rapid switching between all administrative subsystems.
   *
   * Scenario: Input or condition being tested
   * - Render `<AdminLayout />` for an active Admin session.
   *
   * Pass condition: What result or assertion makes it pass
   * - All 6 navigation links are in the document, and "Admin User" is displayed.
   *
   * Run from repository root:
   * npm --prefix web test -- src/shared/layouts/__tests__/AdminLayout.test.jsx -t "renders navigation links and user full name"
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that clicking the Logout button terminates the session (nulls user and token in auth store)
   *   and redirects the browser to the `/login` route.
   *
   * Why: Why this scenario matters
   * - Security-critical session termination flow preventing unauthorized reuse of an open terminal.
   *
   * Scenario: Input or condition being tested
   * - User clicks Logout button while in the admin shell.
   *
   * Pass condition: What result or assertion makes it pass
   * - Browser navigates to `/login` ("Login Page After Logout" is mounted), and `authState.user` and `authState.token` are null.
   *
   * Run from repository root:
   * npm --prefix web test -- src/shared/layouts/__tests__/AdminLayout.test.jsx -t "logs out and redirects to /login"
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that the brand footer ("AssistLK", "Admin Console v1.0") renders at the bottom
   *   as pure presentation without introducing unexpected links or breaking existing layout controls.
   *
   * Why: Why this scenario matters
   * - Footer metadata must provide software versioning without polluting the keyboard tab order or link lists.
   *
   * Scenario: Input or condition being tested
   * - Inspect all links, buttons, and text elements in `<AdminLayout />`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Exactly 6 links and 1 button exist in the document, and version "Admin Console v1.0" is present.
   *
   * Run from repository root:
   * npm --prefix web test -- src/shared/layouts/__tests__/AdminLayout.test.jsx -t "renders subtle brand footer at bottom"
   */
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
