import { describe, it, expect, vi, beforeEach } from "vitest";
import serviceRequestService from "../serviceRequestService";
import apiClient from "../../../../shared/api/apiClient";

vi.mock("../../../../shared/api/apiClient", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
  },
}));

describe("serviceRequestService", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("calls POST /api/service-requests with data on create", async () => {
    const payload = {
      description: "Fix pipe leak",
      locationText: "Colombo 03",
      latitude: 6.9271,
      longitude: 79.8612,
    };
    const responseData = { serviceRequestId: "req-1", ...payload };
    apiClient.post.mockResolvedValueOnce({ data: responseData });

    const result = await serviceRequestService.create(payload);

    expect(apiClient.post).toHaveBeenCalledWith("/api/service-requests", payload);
    expect(result).toEqual(responseData);
  });

  it("calls GET /api/service-requests/my on getMyRequests", async () => {
    const list = [{ serviceRequestId: "req-1" }, { serviceRequestId: "req-2" }];
    apiClient.get.mockResolvedValueOnce({ data: list });

    const result = await serviceRequestService.getMyRequests();

    expect(apiClient.get).toHaveBeenCalledWith("/api/service-requests/my");
    expect(result).toEqual(list);
  });

  it("calls GET /api/service-requests/{id} on getById", async () => {
    const responseData = { serviceRequestId: "req-123", status: "Created" };
    apiClient.get.mockResolvedValueOnce({ data: responseData });

    const result = await serviceRequestService.getById("req-123");

    expect(apiClient.get).toHaveBeenCalledWith("/api/service-requests/req-123");
    expect(result).toEqual(responseData);
  });

  it("calls PUT /api/service-requests/{id} with data on update", async () => {
    const payload = {
      description: "Updated description",
      locationText: "Kandy",
      latitude: null,
      longitude: null,
    };
    const responseData = { serviceRequestId: "req-456", ...payload };
    apiClient.put.mockResolvedValueOnce({ data: responseData });

    const result = await serviceRequestService.update("req-456", payload);

    expect(apiClient.put).toHaveBeenCalledWith("/api/service-requests/req-456", payload);
    expect(result).toEqual(responseData);
  });

  it("calls POST /api/service-requests/{id}/cancel on cancel", async () => {
    const responseData = { serviceRequestId: "req-789", status: "Cancelled" };
    apiClient.post.mockResolvedValueOnce({ data: responseData });

    const result = await serviceRequestService.cancel("req-789");

    expect(apiClient.post).toHaveBeenCalledWith("/api/service-requests/req-789/cancel");
    expect(result).toEqual(responseData);
  });

  it("calls POST /api/service-requests/{id}/analyze on analyze", async () => {
    const responseData = {
      serviceRequestId: "req-101",
      status: "Analyzed",
      category: "Plumbing",
      urgency: "High",
      confidence: 0.95,
    };
    apiClient.post.mockResolvedValueOnce({ data: responseData });

    const result = await serviceRequestService.analyze("req-101");

    expect(apiClient.post).toHaveBeenCalledWith("/api/service-requests/req-101/analyze");
    expect(result).toEqual(responseData);
  });

  it("calls POST /api/service-requests/{id}/ready-for-matching on markReadyForMatching", async () => {
    const responseData = {
      serviceRequestId: "req-202",
      status: "ReadyForMatching",
    };
    apiClient.post.mockResolvedValueOnce({ data: responseData });

    const result = await serviceRequestService.markReadyForMatching("req-202");

    expect(apiClient.post).toHaveBeenCalledWith("/api/service-requests/req-202/ready-for-matching");
    expect(result).toEqual(responseData);
  });
});
