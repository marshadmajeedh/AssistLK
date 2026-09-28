import apiClient from "../../../shared/api/apiClient";

export const adminServiceRequestService = {
  getAll: async (filters = {}) => {
    const params = {};
    for (const key of ["status", "category", "urgency"]) {
      const val = filters[key];
      if (typeof val === "string") {
        const trimmed = val.trim();
        if (trimmed && trimmed.toLowerCase() !== "all") {
          params[key] = trimmed;
        }
      }
    }
    const response = await apiClient.get("/api/admin/service-requests", { params });
    return response.data;
  },
  getById: async (id) => {
    const response = await apiClient.get(`/api/admin/service-requests/${encodeURIComponent(id)}`);
    return response.data;
  },
};

export default adminServiceRequestService;
