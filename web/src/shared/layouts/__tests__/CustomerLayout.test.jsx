import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { describe, it, expect, beforeEach } from "vitest";
import CustomerLayout from "../CustomerLayout";
import { useAuthStore } from "../../auth/authStore";

describe("CustomerLayout", () => {
  beforeEach(() => {
    useAuthStore.setState({
      user: { userId: "user-1", fullName: "John Doe", role: "Customer" },
      token: "valid-token",
    });
  });

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
