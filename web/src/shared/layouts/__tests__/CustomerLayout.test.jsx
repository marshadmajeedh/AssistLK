import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { describe, it, expect, beforeEach } from "vitest";
import CustomerLayout from "../CustomerLayout";
import { useAuthStore } from "../../auth/authStore";

/**
 * TEST SUITE: Customer Portal Layout Component
 *
 * 1. What this class/suite tests:
 *    - Verifies the customer application shell layout (`CustomerLayout`), including top navigation
 *      links ("My Requests", "Create Request"), "AssistLK" brand title, user display name,
 *      and the logout workflow.
 * 2. Why these tests are needed:
 *    - The customer layout provides the persistent chrome and navigation for authenticated customers
 *      creating and tracking their service requests. Tests verify navigation structure and clean logout transitions.
 * 3. What is real and what is mocked/faked:
 *    - Real: React Router route rendering and userEvent click events via React Testing Library.
 *    - Mocked: Auth store state (`useAuthStore`) initialized with a Customer session.
 * 4. Prerequisites:
 *    - Node.js dependencies installed (`npm install` inside `/web`).
 * 5. PowerShell command to run this suite from repository root:
 *    npm --prefix web test -- src/shared/layouts/__tests__/CustomerLayout.test.jsx
 */
describe("CustomerLayout", () => {
  beforeEach(() => {
    useAuthStore.setState({
      user: { userId: "user-1", fullName: "John Doe", role: "Customer" },
      token: "valid-token",
    });
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that the customer header renders "AssistLK", links for "My Requests" and "Create Request",
   *   the user's full name ("John Doe"), and mounts child route content.
   *
   * Why: Why this scenario matters
   * - Customers must have immediate access to their request lists and creation wizards from anywhere in the app.
   *
   * Scenario: Input or condition being tested
   * - Render `<CustomerLayout />` wrapping a `/service-requests` child route.
   *
   * Pass condition: What result or assertion makes it pass
   * - Brand "AssistLK", both navigation links, "John Doe", and "My Requests Content" are in the document.
   *
   * Run from repository root:
   * npm --prefix web test -- src/shared/layouts/__tests__/CustomerLayout.test.jsx -t "renders navigation links, brand, and user full name"
   */
  it("renders navigation links, brand, and user full name", () => {
    render(
      <MemoryRouter initialEntries={["/service-requests"]}>
        <Routes>
          <Route element={<CustomerLayout />}>
            <Route
              path="/service-requests"
              element={<div>My Requests Content</div>}
            />
          </Route>
        </Routes>
      </MemoryRouter>
    );

    expect(screen.getByText("AssistLK")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /my requests/i })).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: /create request/i })
    ).toBeInTheDocument();
    expect(screen.getByText("John Doe")).toBeInTheDocument();
    expect(screen.getByText("My Requests Content")).toBeInTheDocument();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that clicking Logout in the customer navigation clears user/token state and redirects to `/login`.
   *
   * Why: Why this scenario matters
   * - Ensures customer session termination is complete and secure.
   *
   * Scenario: Input or condition being tested
   * - Customer clicks Logout button in header.
   *
   * Pass condition: What result or assertion makes it pass
   * - Navigates to `/login`, and `authState.user` and `authState.token` are null.
   *
   * Run from repository root:
   * npm --prefix web test -- src/shared/layouts/__tests__/CustomerLayout.test.jsx -t "logs out and redirects to /login on logout button click"
   */
  it("logs out and redirects to /login on logout button click", async () => {
    const user = userEvent.setup();

    render(
      <MemoryRouter initialEntries={["/service-requests"]}>
        <Routes>
          <Route path="/login" element={<div>Login Page After Logout</div>} />
          <Route element={<CustomerLayout />}>
            <Route
              path="/service-requests"
              element={<div>My Requests Content</div>}
            />
          </Route>
        </Routes>
      </MemoryRouter>
    );

    const logoutBtn = screen.getByRole("button", { name: /logout/i });
    await user.click(logoutBtn);

    expect(
      screen.getByText("Login Page After Logout")
    ).toBeInTheDocument();

    const authState = useAuthStore.getState();
    expect(authState.user).toBeNull();
    expect(authState.token).toBeNull();
  });
});
