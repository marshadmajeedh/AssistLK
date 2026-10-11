import { render, screen } from "@testing-library/react";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { describe, it, expect, beforeEach } from "vitest";
import ProtectedRoute from "../ProtectedRoute";
import { useAuthStore } from "../authStore";

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

  it("redirects unauthenticated guest to /login", () => {
    renderWithRoutes(["Customer"]);
    expect(screen.getByText("Login Page Target")).toBeInTheDocument();
    expect(screen.queryByText("Protected Secret Content")).not.toBeInTheDocument();
  });

  it("renders protected content when user has required role", () => {
    useAuthStore.setState({
      user: { role: "Customer", fullName: "Alice" },
      token: "valid-token",
    });

    renderWithRoutes(["Customer"]);
    expect(screen.getByText("Protected Secret Content")).toBeInTheDocument();
  });

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
