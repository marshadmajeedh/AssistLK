import apiClient from "../../../shared/api/apiClient";

const serviceTrackingService = {
  getComplaints: async () => {
    const response = await apiClient.get("/api/reports/complaints");
    return response.data;
  },
  getSuspiciousJobs: async () => {
    const response = await apiClient.get("/api/reports/suspicious-jobs");
    return response.data;
  },
  updateJobStatus: async (jobId, payload) => {
    const response = await apiClient.put(`/api/service-jobs/${jobId}/status`, payload);
    return response.data;
  },
  updateComplaintStatus: async (complaintId, payload) => {
    const response = await apiClient.put(`/api/reports/complaints/${complaintId}/status`, payload);
    return response.data;
  },
};

export default serviceTrackingService;
