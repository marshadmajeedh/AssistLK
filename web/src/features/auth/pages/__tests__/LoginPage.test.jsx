import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { describe, it, expect, vi, beforeEach } from "vitest";
import LoginPage from "../LoginPage";
import { useAuthStore } from "../../../../shared/auth/authStore";

/**
 * TEST SUITE: Login Page UI and Authentication Behavior
 *
 * 1. What this class/suite tests:
 *    - Verifies the administrator login screen (`LoginPage`), including HTML5 input validation,
 *      password visibility toggling, credential dispatch through Zustand `useAuthStore`,
 *      role-based redirection (Admin to `/dashboard`), non-admin authorization rejection
 *      (Customer rejected with an explicit error alert), AssistLK branding presentation,
 *      and loading state feedback.
 * 2. Why these tests are needed:
 *    - The login page is the security gate for the AssistLK admin portal. It must ensure that
 *      only authorized administrative accounts gain entry, while regular customers and service
 *      providers are safely blocked from accessing administrative features.
 * 3. What is real and what is mocked/faked:
 *    - Real: Full React DOM rendering via React Testing Library, userEvent keyboard typing and clicks,
 *      React Router navigation, and Zustand auth store state mutations.
 *    - Mocked: The API login function (`useAuthStore.setState({ login: loginMock })`) is stubbed
 *      to test authentication and authorization responses without live network calls.
 * 4. Prerequisites:
 *    - Node.js dependencies installed (`npm install` inside `/web`).
 * 5. PowerShell command to run this suite from repository root:
 *    npm --prefix web test -- src/features/auth/pages/__tests__/LoginPage.test.jsx
 */
describe("LoginPage UI & Auth Behavior", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.setState({
      user: null,
      token: null,
      loading: false,
      error: null,
    });
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that the Email input field has the correct label, email type, placeholder, and required attribute.
   *
   * Why: Why this scenario matters
   * - Ensures browser native validation for email format and accessibility labeling for screen readers.
   *
   * Scenario: Input or condition being tested
   * - Render `<LoginPage />` inside memory router.
   *
   * Pass condition: What result or assertion makes it pass
   * - Element accessible by label "email" has type="email", placeholder="Enter your email address", and is required.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/auth/pages/__tests__/LoginPage.test.jsx -t "1. Email field renders with label"
   */
  it("1. Email field renders with label, placeholder, and required validation", () => {
    render(
      <MemoryRouter initialEntries={["/login"]}>
        <LoginPage />
      </MemoryRouter>
    );

    const emailLabel = screen.getByLabelText(/^email$/i);
    expect(emailLabel).toBeInTheDocument();
    expect(emailLabel).toHaveAttribute("type", "email");
    expect(emailLabel).toHaveAttribute("placeholder", "Enter your email address");
    expect(emailLabel).toBeRequired();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that the Password input field renders correctly and that clicking the "Show password"
   *   button toggles its input type between "password" and "text".
   *
   * Why: Why this scenario matters
   * - Improves usability by letting users visually confirm complex passwords before submission.
   *
   * Scenario: Input or condition being tested
   * - User clicks "Show password" button, then clicks "Hide password".
   *
   * Pass condition: What result or assertion makes it pass
   * - Initial type is "password"; after first click type is "text"; after second click type reverts to "password".
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/auth/pages/__tests__/LoginPage.test.jsx -t "2. Password field renders with label"
   */
  it("2. Password field renders with label, placeholder, and supports visibility toggling", async () => {
    const user = userEvent.setup();

    render(
      <MemoryRouter initialEntries={["/login"]}>
        <LoginPage />
      </MemoryRouter>
    );

    const passwordInput = screen.getByLabelText(/^password$/i);
    expect(passwordInput).toBeInTheDocument();
    expect(passwordInput).toHaveAttribute("type", "password");
    expect(passwordInput).toHaveAttribute("placeholder", "Enter your password");
    expect(passwordInput).toBeRequired();

    const toggleBtn = screen.getByRole("button", { name: /show password/i });
    expect(toggleBtn).toBeInTheDocument();

    await user.click(toggleBtn);
    expect(passwordInput).toHaveAttribute("type", "text");
    expect(screen.getByRole("button", { name: /hide password/i })).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: /hide password/i }));
    expect(passwordInput).toHaveAttribute("type", "password");
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that typing email and password and clicking "Sign In" invokes the store's `login()`
   *   method with the exact credentials typed.
   *
   * Why: Why this scenario matters
   * - Confirms form submission correctly wires input values to the authentication state handler.
   *
   * Scenario: Input or condition being tested
   * - User types "admin@assistlk.com" and "SecretPassword123!" and submits.
   *
   * Pass condition: What result or assertion makes it pass
   * - `loginMock` is called with ("admin@assistlk.com", "SecretPassword123!").
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/auth/pages/__tests__/LoginPage.test.jsx -t "3. Sign In submits credentials"
   */
  it("3. Sign In submits credentials through the existing login handler", async () => {
    const user = userEvent.setup();
    const loginMock = vi.fn().mockResolvedValue({
      userId: "admin-1",
      fullName: "Admin Officer",
      role: "Admin",
    });

    useAuthStore.setState({ login: loginMock });

    render(
      <MemoryRouter initialEntries={["/login"]}>
        <LoginPage />
      </MemoryRouter>
    );

    const emailInput = screen.getByLabelText(/^email$/i);
    const passwordInput = screen.getByLabelText(/^password$/i);
    const submitBtn = screen.getByRole("button", { name: /^sign in$/i });

    await user.type(emailInput, "admin@assistlk.com");
    await user.type(passwordInput, "SecretPassword123!");
    await user.click(submitBtn);

    expect(loginMock).toHaveBeenCalledWith("admin@assistlk.com", "SecretPassword123!");
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that inputs retain browser autocomplete hints ("username" and "current-password")
   *   and HTML5 required flags.
   *
   * Why: Why this scenario matters
   * - Enables password managers and browser autofill to work smoothly and securely.
   *
   * Scenario: Input or condition being tested
   * - Inspect attributes of email and password input elements.
   *
   * Pass condition: What result or assertion makes it pass
   * - Email has `required` and `autoComplete="username"`; password has `required` and `autoComplete="current-password"`.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/auth/pages/__tests__/LoginPage.test.jsx -t "4. HTML5 input validation attributes"
   */
  it("4. HTML5 input validation attributes are preserved", () => {
    render(
      <MemoryRouter initialEntries={["/login"]}>
        <LoginPage />
      </MemoryRouter>
    );

    const emailInput = screen.getByLabelText(/^email$/i);
    const passwordInput = screen.getByLabelText(/^password$/i);

    expect(emailInput).toHaveAttribute("required");
    expect(emailInput).toHaveAttribute("autoComplete", "username");
    expect(passwordInput).toHaveAttribute("required");
    expect(passwordInput).toHaveAttribute("autoComplete", "current-password");
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that an authentication error in the store is rendered in an accessible alert banner (`role="alert"`).
   *
   * Why: Why this scenario matters
   * - Immediate, accessible feedback when credentials fail.
   *
   * Scenario: Input or condition being tested
   * - `useAuthStore` initialized with `error: "Invalid email or password."`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Alert banner displays the exact error message.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/auth/pages/__tests__/LoginPage.test.jsx -t "5. Existing error rendering remains functional"
   */
  it("5. Existing error rendering remains functional when an error is present", () => {
    useAuthStore.setState({
      error: "Invalid email or password.",
    });

    render(
      <MemoryRouter initialEntries={["/login"]}>
        <LoginPage />
      </MemoryRouter>
    );

    const errorAlert = screen.getByRole("alert");
    expect(errorAlert).toBeInTheDocument();
    expect(errorAlert).toHaveTextContent("Invalid email or password.");
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that successful authentication with an "Admin" role navigates to the `/dashboard` route.
   *
   * Why: Why this scenario matters
   * - Admins must be routed directly into their operational console upon login.
   *
   * Scenario: Input or condition being tested
   * - Admin logs in with valid credentials, `loginMock` returns `{ role: "Admin" }`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Route `/dashboard` mounts and displays "Admin Dashboard Landed".
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/auth/pages/__tests__/LoginPage.test.jsx -t "6. Redirects Admin to /dashboard"
   */
  it("6. Redirects Admin to /dashboard on successful authentication", async () => {
    const user = userEvent.setup();
    const loginMock = vi.fn().mockResolvedValue({
      userId: "admin-1",
      fullName: "System Admin",
      role: "Admin",
    });

    useAuthStore.setState({ login: loginMock });

    render(
      <MemoryRouter initialEntries={["/login"]}>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/dashboard" element={<div>Admin Dashboard Landed</div>} />
        </Routes>
      </MemoryRouter>
    );

    await user.type(screen.getByLabelText(/^email$/i), "admin@assistlk.com");
    await user.type(screen.getByLabelText(/^password$/i), "ValidPass123");
    await user.click(screen.getByRole("button", { name: /^sign in$/i }));

    await waitFor(() => {
      expect(screen.getByText("Admin Dashboard Landed")).toBeInTheDocument();
    });
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that a non-admin account (e.g. role "Customer") is rejected with an explicit
   *   authorization error message and NOT navigated to the dashboard.
   *
   * Why: Why this scenario matters
   * - Role security guard: the web portal is strictly an admin console; non-admin accounts
   *   must not bypass role enforcement even if their password is valid.
   *
   * Scenario: Input or condition being tested
   * - Customer logs in; `loginMock` resolves with `{ role: "Customer" }`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Store error is set to "Role 'Customer' is not authorized to access this portal." and `/dashboard` does not land.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/auth/pages/__tests__/LoginPage.test.jsx -t "7. Non-admin authorization behavior"
   */
  it("7. Non-admin authorization behavior remains unchanged (rejects Customer with error message)", async () => {
    const user = userEvent.setup();
    const logoutMock = vi.fn();
    const loginMock = vi.fn().mockResolvedValue({
      userId: "cust-1",
      fullName: "Regular Customer",
      role: "Customer",
    });

    useAuthStore.setState({
      login: loginMock,
      logout: logoutMock,
    });

    render(
      <MemoryRouter initialEntries={["/login"]}>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/dashboard" element={<div>Dashboard Should Not Land</div>} />
        </Routes>
      </MemoryRouter>
    );

    await user.type(screen.getByLabelText(/^email$/i), "customer@test.com");
    await user.type(screen.getByLabelText(/^password$/i), "CustomerPass123");
    await user.click(screen.getByRole("button", { name: /^sign in$/i }));

    await waitFor(() => {
      expect(useAuthStore.getState().error).toBe(
        "Role 'Customer' is not authorized to access this portal."
      );
    });
    expect(screen.queryByText("Dashboard Should Not Land")).not.toBeInTheDocument();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that the AssistLK brand logo, headings, tagline, and descriptive text render accurately.
   *
   * Why: Why this scenario matters
   * - Professional branding presentation and portal identification for users.
   *
   * Scenario: Input or condition being tested
   * - Render `<LoginPage />` and query brand elements.
   *
   * Pass condition: What result or assertion makes it pass
   * - Logo image with alt "AssistLK Logo" is present with src; headings "Admin Portal", "Trusted service operations...",
   *   "ADMIN CONSOLE" are all in the document.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/auth/pages/__tests__/LoginPage.test.jsx -t "8. AssistLK branding"
   */
  it("8. AssistLK branding, logo, headings, tagline, and description render accurately", () => {
    render(
      <MemoryRouter initialEntries={["/login"]}>
        <LoginPage />
      </MemoryRouter>
    );

    // Official Logo with alt text
    const logoImg = screen.getByRole("img", { name: /assistlk logo/i });
    expect(logoImg).toBeInTheDocument();
    expect(logoImg).toHaveAttribute("src");

    // Headings
    expect(screen.getByText("Admin Portal")).toBeInTheDocument();
    expect(
      screen.getByText("Trusted service operations, smarter coordination.")
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        /Manage service requests, provider matching, quotations, and live tracking/i
      )
    ).toBeInTheDocument();

    // Right panel titles
    expect(screen.getByText("ADMIN CONSOLE")).toBeInTheDocument();
    expect(
      screen.getByText("Sign in to continue to the authorized admin console.")
    ).toBeInTheDocument();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that the 4 system feature callouts ("Service Requests", "Provider Matching",
   *   "Quotations & Approvals", "Live Tracking") render in the brand row.
   *
   * Why: Why this scenario matters
   * - Highlights platform capabilities to administrators during sign-in.
   *
   * Scenario: Input or condition being tested
   * - Inspect `.brand-feature-row` element.
   *
   * Pass condition: What result or assertion makes it pass
   * - Row contains all four component capability names.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/auth/pages/__tests__/LoginPage.test.jsx -t "9. Four compact feature items"
   */
  it("9. Four compact feature items render presentational content without breaking navigation", () => {
    const { container } = render(
      <MemoryRouter initialEntries={["/login"]}>
        <LoginPage />
      </MemoryRouter>
    );

    const featureRow = container.querySelector(".brand-feature-row");
    expect(featureRow).toBeInTheDocument();
    expect(featureRow).toHaveTextContent("Service Requests");
    expect(featureRow).toHaveTextContent("Provider Matching");
    expect(featureRow).toHaveTextContent("Quotations & Approvals");
    expect(featureRow).toHaveTextContent("Live Tracking");
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that the login form possesses proper accessibility markup (`role="form"` with name).
   *
   * Why: Why this scenario matters
   * - Screen readers announce the form landmark ("Admin Sign In") and facilitate easy keyboard navigation.
   *
   * Scenario: Input or condition being tested
   * - Query form by role and accessibility name.
   *
   * Pass condition: What result or assertion makes it pass
   * - Form with name "Admin Sign In", email input, password input, and Sign In button are present.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/auth/pages/__tests__/LoginPage.test.jsx -t "10. Responsive structure and form integrity"
   */
  it("10. Responsive structure and form integrity remain intact", () => {
    render(
      <MemoryRouter initialEntries={["/login"]}>
        <LoginPage />
      </MemoryRouter>
    );

    // Form and controls are intact and accessible
    expect(screen.getByRole("form", { name: /admin sign in/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/^email$/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^password$/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^sign in$/i })).toBeInTheDocument();
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that when authentication is in flight (`loading: true`), the submit button
   *   is disabled and changes its label to "Signing in...".
   *
   * Why: Why this scenario matters
   * - Prevents duplicate form submissions and communicates progress to the user.
   *
   * Scenario: Input or condition being tested
   * - `useAuthStore` initialized with `loading: true`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Button is disabled and displays text matching `/signing in\.\.\./i`.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/auth/pages/__tests__/LoginPage.test.jsx -t "11. Loading state disables button"
   */
  it("11. Loading state disables button and displays progress text", () => {
    useAuthStore.setState({ loading: true });

    render(
      <MemoryRouter initialEntries={["/login"]}>
        <LoginPage />
      </MemoryRouter>
    );

    const submitBtn = screen.getByRole("button", { name: /signing in\.\.\./i });
    expect(submitBtn).toBeInTheDocument();
    expect(submitBtn).toBeDisabled();
  });
});
