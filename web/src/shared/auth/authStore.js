import { create } from "zustand";
import apiClient from "../api/apiClient";

const storedUser = sessionStorage.getItem("assistlk_user");

export const useAuthStore = create((set) => ({
  user: storedUser ? JSON.parse(storedUser) : null,

  token: sessionStorage.getItem("assistlk_token"),

  loading: false,

  error: null,

  login: async (email, password) => {
    set({
      loading: true,
      error: null,
    });

    try {
      const response = await apiClient.post("/api/auth/login", {
        email,
        password,
      });

      const {
        token,
        userId,
        fullName,
        email: userEmail,
        role,
      } = response.data;

      const user = {
        userId,
        fullName,
        email: userEmail,
        role,
      };

      sessionStorage.setItem(
        "assistlk_token",
        token
      );

      sessionStorage.setItem(
        "assistlk_user",
        JSON.stringify(user)
      );

      set({
        user,
        token,
        loading: false,
      });

      return user;
    } catch (error) {
      const message =
        error.response?.data?.message ??
        "Unable to login.";

      set({
        error: message,
        loading: false,
      });

      throw error;
    }
  },

  logout: () => {
    sessionStorage.removeItem("assistlk_token");
    sessionStorage.removeItem("assistlk_user");

    set({
      user: null,
      token: null,
      error: null,
    });
  },

  setError: (message) => {
    set({
      error: message,
    });
  },
}));
