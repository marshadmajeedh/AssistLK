import apiClient from "../../../shared/api/apiClient";

export const serviceRequestService = {
  create: async (data) => {
    const response = await apiClient.post("/api/service-requests", data);
    return response.data;
  },

  getMyRequests: async () => {
    const response = await apiClient.get("/api/service-requests/my");
    return response.data;
  },

  getById: async (id) => {
    const response = await apiClient.get(`/api/service-requests/${id}`);
    return response.data;
  },

  update: async (id, data) => {
    const response = await apiClient.put(`/api/service-requests/${id}`, data);
    return response.data;
  },

  cancel: async (id) => {
    const response = await apiClient.post(`/api/service-requests/${id}/cancel`);
    return response.data;
  },

  analyze: async (id) => {
    const response = await apiClient.post(`/api/service-requests/${id}/analyze`);
    return response.data;
  },

  markReadyForMatching: async (id) => {
    const response = await apiClient.post(
      `/api/service-requests/${id}/ready-for-matching`
    );
    return response.data;
  },
};

export default serviceRequestService;
