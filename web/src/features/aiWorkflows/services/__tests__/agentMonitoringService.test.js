import { beforeEach, describe, expect, it, vi } from "vitest";
import apiClient from "../../../../shared/api/apiClient";
import service from "../agentMonitoringService";

vi.mock("../../../../shared/api/apiClient", () => ({ default: { get: vi.fn() } }));
beforeEach(() => vi.resetAllMocks());

describe("agentMonitoringService", () => {
  it("gets and returns metrics through the shared client without query parameters", async () => {
    const data = [{ id: "metric-1", status: "Completed" }];
    apiClient.get.mockResolvedValue({ data });
    expect(await service.getMetrics()).toBe(data);
    expect(apiClient.get).toHaveBeenCalledExactlyOnceWith("/api/agent-monitoring");
  });
  it("returns an empty response array", async () => {
    apiClient.get.mockResolvedValue({ data: [] });
    expect(await service.getMetrics()).toEqual([]);
  });
  it("propagates client errors unchanged", async () => {
    const error = new Error("Network Error");
    apiClient.get.mockRejectedValue(error);
    await expect(service.getMetrics()).rejects.toBe(error);
  });
});
