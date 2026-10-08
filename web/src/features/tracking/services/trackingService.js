import apiClient from "../../../shared/api/apiClient";

export const trackingService = {
  getJobs: async (status) => {
    const params = {};
    if (typeof status === "string" && status.trim()) {
      params.status = status.trim();
    }
    const response = await apiClient.get("/api/service-jobs", { params });
    return response.data;
  },
};

export default trackingService;