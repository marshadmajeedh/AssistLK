// What this suite tests:
//   The frontend API client service (serviceRequestService) for Component 1, verifying that
//   all CRUD, lifecycle, AI analysis, and matching handoff calls invoke the correct Axios HTTP
//   verbs and endpoint paths with expected request and response payloads.
// Why these tests are needed:
//   Guarantees that UI pages and components interact with the correct backend routes
//   (/api/service-requests, /my, /{id}, /{id}/cancel, /{id}/analyze, /{id}/ready-for-matching)
//   without URL or parameter serialization bugs.
// What is real and what is mocked/faked:
//   - Real: The serviceRequestService implementation.
//   - Mocked/Faked: apiClient (Axios wrapper) is mocked using Vitest vi.fn() to avoid actual network calls.
// Prerequisites:
//   Node.js dependencies installed in web folder (npm install).
// Run from repository root:
//   npm --prefix web test -- src/features/serviceRequests/services/__tests__/serviceRequestService.test.js

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

  // Purpose: Checks that creating a service request sends a POST request to /api/service-requests with the payload.
  // Why: Verifies frontend creation form submits correctly to the backend collection endpoint.
  // Scenario: Calling create with description and location coordinates.
  // Pass condition: apiClient.post is called with "/api/service-requests" and payload, returning the created entity.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/services/__tests__/serviceRequestService.test.js -t "calls POST /api/service-requests with data on create"
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

  // Purpose: Checks that retrieving current customer requests sends a GET request to /api/service-requests/my.
  // Why: Ensures the customer request list page loads requests scoped to the authenticated customer.
  // Scenario: Calling getMyRequests.
  // Pass condition: apiClient.get is called with "/api/service-requests/my" and resolves the returned list.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/services/__tests__/serviceRequestService.test.js -t "calls GET /api/service-requests/my on getMyRequests"
  it("calls GET /api/service-requests/my on getMyRequests", async () => {
    const list = [{ serviceRequestId: "req-1" }, { serviceRequestId: "req-2" }];
    apiClient.get.mockResolvedValueOnce({ data: list });

    const result = await serviceRequestService.getMyRequests();

    expect(apiClient.get).toHaveBeenCalledWith("/api/service-requests/my");
    expect(result).toEqual(list);
  });

  // Purpose: Checks that fetching a specific service request sends a GET request to /api/service-requests/{id}.
  // Why: Ensures the service request detail and edit pages fetch the requested record by its unique ID.
  // Scenario: Calling getById with "req-123".
  // Pass condition: apiClient.get is called with "/api/service-requests/req-123" and resolves the detail object.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/services/__tests__/serviceRequestService.test.js -t "calls GET /api/service-requests/{id} on getById"
  it("calls GET /api/service-requests/{id} on getById", async () => {
    const responseData = { serviceRequestId: "req-123", status: "Created" };
    apiClient.get.mockResolvedValueOnce({ data: responseData });

    const result = await serviceRequestService.getById("req-123");

    expect(apiClient.get).toHaveBeenCalledWith("/api/service-requests/req-123");
    expect(result).toEqual(responseData);
  });

  // Purpose: Checks that editing a service request sends a PUT request to /api/service-requests/{id} with the update payload.
  // Why: Ensures the edit request page properly submits updated description and location data.
  // Scenario: Calling update with "req-456" and updated description.
  // Pass condition: apiClient.put is called with "/api/service-requests/req-456" and payload, returning the updated entity.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/services/__tests__/serviceRequestService.test.js -t "calls PUT /api/service-requests/{id} with data on update"
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

  // Purpose: Checks that cancelling a service request sends a POST request to /api/service-requests/{id}/cancel.
  // Why: Ensures user cancellation actions call the dedicated cancellation lifecycle endpoint.
  // Scenario: Calling cancel with "req-789".
  // Pass condition: apiClient.post is called with "/api/service-requests/req-789/cancel" and resolves with status "Cancelled".
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/services/__tests__/serviceRequestService.test.js -t "calls POST /api/service-requests/{id}/cancel on cancel"
  it("calls POST /api/service-requests/{id}/cancel on cancel", async () => {
    const responseData = { serviceRequestId: "req-789", status: "Cancelled" };
    apiClient.post.mockResolvedValueOnce({ data: responseData });

    const result = await serviceRequestService.cancel("req-789");

    expect(apiClient.post).toHaveBeenCalledWith("/api/service-requests/req-789/cancel");
    expect(result).toEqual(responseData);
  });

  // Purpose: Checks that triggering problem analysis sends a POST request to /api/service-requests/{id}/analyze.
  // Why: Ensures clicking "Analyze Problem" dispatches the AI workflow request to the backend.
  // Scenario: Calling analyze with "req-101".
  // Pass condition: apiClient.post is called with "/api/service-requests/req-101/analyze" and returns analysis result with category and urgency.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/services/__tests__/serviceRequestService.test.js -t "calls POST /api/service-requests/{id}/analyze on analyze"
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

  // Purpose: Checks that marking a request ready for matching sends a POST request to /api/service-requests/{id}/ready-for-matching.
  // Why: Ensures confirming analysis results transitions the request to Component 2 provider matching.
  // Scenario: Calling markReadyForMatching with "req-202".
  // Pass condition: apiClient.post is called with "/api/service-requests/req-202/ready-for-matching" and resolves with status "ReadyForMatching".
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/services/__tests__/serviceRequestService.test.js -t "calls POST /api/service-requests/{id}/ready-for-matching on markReadyForMatching"
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
