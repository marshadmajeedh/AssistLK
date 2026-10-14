// What this suite tests:
//   The admin API service client (adminServiceRequestService) for service request management,
//   verifying query parameter serialization, query filtering (stripping case-insensitive 'All', empty strings,
//   and unsupported filters), detail endpoint lookups, and error propagation.
// Why these tests are needed:
//   Guarantees that admin dashboard filters serialize clean query strings to /api/admin/service-requests
//   (e.g. omitting 'All' filters rather than sending them as literal query values that would yield 0 results),
//   and verifies authoritative detail requests route correctly.
// What is real and what is mocked/faked:
//   - Real: adminServiceRequestService implementation.
//   - Mocked/Faked: apiClient (Axios wrapper) is mocked via Vitest vi.fn().
// Prerequisites:
//   Node.js dependencies installed in web folder (npm install).
// Run from repository root:
//   npm --prefix web test -- src/features/admin/services/__tests__/adminServiceRequestService.test.js

import { beforeEach, describe, expect, it, vi } from "vitest";
import apiClient from "../../../../shared/api/apiClient";
import service from "../adminServiceRequestService";

vi.mock("../../../../shared/api/apiClient", () => ({ default: { get: vi.fn() } }));
beforeEach(() => vi.resetAllMocks());

describe("Admin service request API", () => {
  // Purpose: Checks that calling getAll with no arguments requests /api/admin/service-requests with empty params.
  // Why: Verifies baseline request fetching without query filtering.
  // Scenario: Calling service.getAll().
  // Pass condition: apiClient.get is called with "/api/admin/service-requests" and { params: {} }.
  // Run from repository root:
  //   npm --prefix web test -- src/features/admin/services/__tests__/adminServiceRequestService.test.js -t "gets the list through the shared client"
  it("gets the list through the shared client", async () => {
    const data = [{ serviceRequestId: "id" }];
    apiClient.get.mockResolvedValue({ data });
    expect(await service.getAll()).toEqual(data);
    expect(apiClient.get).toHaveBeenCalledWith("/api/admin/service-requests", { params: {} });
  });

  // Purpose: Checks that unsupported filter properties (like provider) are omitted from query params.
  // Why: Prevents passing unexpected parameters to the admin backend endpoint.
  // Scenario: Calling getAll with status, category, urgency, and an unsupported provider property.
  // Pass condition: Only status, category, and urgency are included in params.
  // Run from repository root:
  //   npm --prefix web test -- src/features/admin/services/__tests__/adminServiceRequestService.test.js -t "sends only supported filters"
  it("sends only supported filters", async () => {
    apiClient.get.mockResolvedValue({ data: [] });
    await service.getAll({ status: "Created", category: "Vehicle Repair", urgency: "High", provider: "ignored" });
    expect(apiClient.get).toHaveBeenCalledWith("/api/admin/service-requests", {
      params: { status: "Created", category: "Vehicle Repair", urgency: "High" },
    });
  });

  // Purpose: Checks that 'All' filter dropdown values are stripped and not passed as literal filters.
  // Why: In the admin UI, 'All' means unconstrained; sending status="All" would return 0 matches from the database.
  // Scenario: Calling getAll({ status: "All", category: "All", urgency: "All" }).
  // Pass condition: apiClient.get is called with empty params {}.
  // Run from repository root:
  //   npm --prefix web test -- src/features/admin/services/__tests__/adminServiceRequestService.test.js -t "omits 'All' filter values and does not serialize them as domain filters"
  it("omits 'All' filter values and does not serialize them as domain filters", async () => {
    apiClient.get.mockResolvedValue({ data: [] });
    await service.getAll({ status: "All", category: "All", urgency: "All" });
    expect(apiClient.get).toHaveBeenCalledWith("/api/admin/service-requests", { params: {} });
  });

  // Purpose: Checks that case-insensitive variations ("all", "ALL", "All") are all stripped.
  // Why: Prevents casing discrepancies from accidentally leaking 'all' into query parameters.
  // Scenario: Calling getAll with mixed casing of 'all'.
  // Pass condition: apiClient.get is called with empty params {}.
  // Run from repository root:
  //   npm --prefix web test -- src/features/admin/services/__tests__/adminServiceRequestService.test.js -t "omits case-insensitive 'all' values"
  it("omits case-insensitive 'all' values", async () => {
    apiClient.get.mockResolvedValue({ data: [] });
    await service.getAll({ status: "all", category: "ALL", urgency: "All" });
    expect(apiClient.get).toHaveBeenCalledWith("/api/admin/service-requests", { params: {} });
  });

  // Purpose: Checks that status=Created is preserved when category and urgency are 'All'.
  // Why: Ensures single-filter selections serialize cleanly while wildcard filters are omitted.
  // Scenario: Calling getAll with status="Created" and category/urgency="All".
  // Pass condition: params contains only { status: "Created" }.
  // Run from repository root:
  //   npm --prefix web test -- src/features/admin/services/__tests__/adminServiceRequestService.test.js -t "serializes status=Created when category and urgency are 'All'"
  it("serializes status=Created when category and urgency are 'All'", async () => {
    apiClient.get.mockResolvedValue({ data: [] });
    await service.getAll({ status: "Created", category: "All", urgency: "All" });
    expect(apiClient.get).toHaveBeenCalledWith("/api/admin/service-requests", {
      params: { status: "Created" },
    });
  });

  // Purpose: Checks that category filter is preserved when status and urgency are 'All'.
  // Why: Ensures category-only filtering produces { category: "Plumbing" }.
  // Scenario: Calling getAll with category="Plumbing" and status/urgency="All".
  // Pass condition: params contains only { category: "Plumbing" }.
  // Run from repository root:
  //   npm --prefix web test -- src/features/admin/services/__tests__/adminServiceRequestService.test.js -t "serializes category when status and urgency are 'All'"
  it("serializes category when status and urgency are 'All'", async () => {
    apiClient.get.mockResolvedValue({ data: [] });
    await service.getAll({ status: "All", category: "Plumbing", urgency: "All" });
    expect(apiClient.get).toHaveBeenCalledWith("/api/admin/service-requests", {
      params: { category: "Plumbing" },
    });
  });

  // Purpose: Checks that urgency filter is preserved when status and category are 'All'.
  // Why: Ensures urgency-only filtering produces { urgency: "High" }.
  // Scenario: Calling getAll with urgency="High" and status/category="All".
  // Pass condition: params contains only { urgency: "High" }.
  // Run from repository root:
  //   npm --prefix web test -- src/features/admin/services/__tests__/adminServiceRequestService.test.js -t "serializes urgency when status and category are 'All'"
  it("serializes urgency when status and category are 'All'", async () => {
    apiClient.get.mockResolvedValue({ data: [] });
    await service.getAll({ status: "All", category: "All", urgency: "High" });
    expect(apiClient.get).toHaveBeenCalledWith("/api/admin/service-requests", {
      params: { urgency: "High" },
    });
  });

  // Purpose: Checks that multiple active domain filters are combined in query parameters.
  // Why: Ensures multi-criteria filtering works properly on the admin requests overview.
  // Scenario: Calling getAll with status="Created", category="Plumbing", urgency="High".
  // Pass condition: params contains all three active key-value pairs.
  // Run from repository root:
  //   npm --prefix web test -- src/features/admin/services/__tests__/adminServiceRequestService.test.js -t "serializes combined valid filters"
  it("serializes combined valid filters", async () => {
    apiClient.get.mockResolvedValue({ data: [] });
    await service.getAll({ status: "Created", category: "Plumbing", urgency: "High" });
    expect(apiClient.get).toHaveBeenCalledWith("/api/admin/service-requests", {
      params: { status: "Created", category: "Plumbing", urgency: "High" },
    });
  });

  // Purpose: Checks that empty strings, whitespace, null, undefined, and case-insensitive 'All' are all stripped from query parameters.
  // Why: Exhaustive verification that invalid/blank filter inputs never contaminate the query string.
  // Scenario: Parameterized test checking "", "   ", null, undefined, "All", "all", "ALL".
  // Pass condition: apiClient.get is called with empty params {} for every case.
  // Note: Parameterized Vitest it.each test. Running this command executes all test cases.
  // Run from repository root:
  //   npm --prefix web test -- src/features/admin/services/__tests__/adminServiceRequestService.test.js -t "omits empty or 'All' filters"
  it.each(["", "   ", null, undefined, "All", "all", "ALL"])("omits empty or 'All' filters (%s)", async (value) => {
    apiClient.get.mockResolvedValue({ data: [] });
    await service.getAll({ status: value, category: value, urgency: value });
    expect(apiClient.get).toHaveBeenCalledWith("/api/admin/service-requests", { params: {} });
  });

  // Purpose: Checks that getById requests the specific admin detail endpoint /api/admin/service-requests/{id}.
  // Why: Verifies admin detail view retrieves authoritative request information.
  // Scenario: Calling getById with a GUID.
  // Pass condition: apiClient.get is called with `/api/admin/service-requests/${id}` and resolves with data.
  // Run from repository root:
  //   npm --prefix web test -- src/features/admin/services/__tests__/adminServiceRequestService.test.js -t "gets authoritative detail"
  it("gets authoritative detail", async () => {
    const data = { serviceRequestId: "a1b2c3d4-1234-4567-8901-123456789abc" };
    apiClient.get.mockResolvedValue({ data });
    expect(await service.getById(data.serviceRequestId)).toEqual(data);
    expect(apiClient.get).toHaveBeenCalledWith(`/api/admin/service-requests/${data.serviceRequestId}`);
  });

  // Purpose: Checks that API errors during getAll and getById are propagated to the caller without suppression.
  // Why: Allows UI components to catch and display meaningful error states to the administrator.
  // Scenario: apiClient.get rejects with an Error for each service method.
  // Pass condition: Calling service method rejects with the exact error.
  // Note: Parameterized Vitest it.each test covering getAll and getById.
  // Run from repository root:
  //   npm --prefix web test -- src/features/admin/services/__tests__/adminServiceRequestService.test.js -t "propagates"
  it.each(["getAll", "getById"])("propagates %s errors unchanged", async (method) => {
    const error = new Error("Network Error");
    apiClient.get.mockRejectedValue(error);
    await expect(service[method]()).rejects.toBe(error);
  });
});
