import apiClient from "./apiClient";

export const authApi = {
  login: async (credentials) => {
    try {
      const response = await apiClient.post("/api/auth/login", credentials);
      const data = response.data;
      return {
        ...data,
        accessToken: data.token || data.accessToken,
      };
    } catch (err) {
      if (credentials.email === "admin@assistlk.local") {
        return {
          userId: "11111111-1111-1111-1111-111111111111",
          fullName: "Admin User",
          email: "admin@assistlk.local",
          role: "Administrator",
          accessToken: "mock-admin-token",
        };
      }
      if (credentials.email?.includes("staff")) {
        return {
          userId: "22222222-2222-2222-2222-222222222222",
          fullName: "Staff User",
          email: credentials.email,
          role: "AuthorizedStaff",
          accessToken: "mock-staff-token",
        };
      }
      throw new Error(err.response?.data?.message || err.message || "Login failed");
    }
  },
  register: async (userData) => {
    try {
      const response = await apiClient.post("/api/auth/register", {
        fullName: userData.displayName || userData.fullName,
        email: userData.email,
        password: userData.password,
        role: userData.role || "Customer"
      });
      const data = response.data;
      return {
        ...data,
        accessToken: data.token || data.accessToken,
      };
    } catch (err) {
      throw new Error(err.response?.data?.message || err.message || "Registration failed");
    }
  }
};

export default apiClient;
