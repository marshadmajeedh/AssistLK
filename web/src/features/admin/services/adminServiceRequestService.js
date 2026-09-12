import apiClient from "../../../shared/api/apiClient";

export const adminServiceRequestService = {
  getAll: async (filters = {}) => {
    const params = {};
    for (const key of ["status", "category", "urgency"]) {
      if (typeof filters[key] === "string" && filters[key].trim()) {
        params[key] = filters[key].trim();
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
