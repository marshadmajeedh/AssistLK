import apiClient from "../../../shared/api/apiClient";

export const agentMonitoringService = {
  getMetrics: async () => {
    const response = await apiClient.get("/api/agent-monitoring", {
      headers: { "Cache-Control": "no-cache" },
    });
    return response.data;
  },
};

export default agentMonitoringService;
