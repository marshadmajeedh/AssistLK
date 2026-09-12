import { beforeEach, describe, expect, it, vi } from "vitest";
import apiClient from "../../../../shared/api/apiClient";
import service from "../adminServiceRequestService";

vi.mock("../../../../shared/api/apiClient", () => ({ default: { get: vi.fn() } }));
beforeEach(() => vi.resetAllMocks());

describe("Admin service request API", () => {
  it("gets the list through the shared client", async () => {
    const data = [{ serviceRequestId: "id" }];
    apiClient.get.mockResolvedValue({ data });
    expect(await service.getAll()).toEqual(data);
    expect(apiClient.get).toHaveBeenCalledWith("/api/admin/service-requests", { params: {} });
  });
  it("sends only supported filters", async () => {
    apiClient.get.mockResolvedValue({ data: [] });
    await service.getAll({ status: "Created", category: "Vehicle Repair", urgency: "High", provider: "ignored" });
    expect(apiClient.get).toHaveBeenCalledWith("/api/admin/service-requests", {
      params: { status: "Created", category: "Vehicle Repair", urgency: "High" },
    });
  });
  it.each(["", "   ", null, undefined])("omits empty filters (%s)", async (value) => {
    apiClient.get.mockResolvedValue({ data: [] });
    await service.getAll({ status: value, category: value, urgency: value });
    expect(apiClient.get).toHaveBeenCalledWith("/api/admin/service-requests", { params: {} });
  });
  it("gets authoritative detail", async () => {
    const data = { serviceRequestId: "a1b2c3d4-1234-4567-8901-123456789abc" };
    apiClient.get.mockResolvedValue({ data });
    expect(await service.getById(data.serviceRequestId)).toEqual(data);
    expect(apiClient.get).toHaveBeenCalledWith(`/api/admin/service-requests/${data.serviceRequestId}`);
  });
  it.each(["getAll", "getById"])("propagates %s errors unchanged", async (method) => {
    const error = new Error("Network Error");
    apiClient.get.mockRejectedValue(error);
    await expect(service[method]()).rejects.toBe(error);
  });
});
