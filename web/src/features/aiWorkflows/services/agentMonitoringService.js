import apiClient from "../../../shared/api/apiClient";

export const agentMonitoringService = {
  getMetrics: async () => {
    const response = await apiClient.get("/api/agent-monitoring");
    return response.data;
  },
};

export default agentMonitoringService;
