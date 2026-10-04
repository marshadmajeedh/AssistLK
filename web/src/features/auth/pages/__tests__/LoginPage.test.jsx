import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { describe, it, expect, vi, beforeEach } from "vitest";
import LoginPage from "../LoginPage";
import { useAuthStore } from "../../../../shared/auth/authStore";

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
