import apiClient from "../../../shared/api/apiClient";

const serviceTrackingService = {
  getComplaints: async () => {
    const response = await apiClient.get("/api/reports/complaints");
    return response.data;
  },
};

export default serviceTrackingService;
