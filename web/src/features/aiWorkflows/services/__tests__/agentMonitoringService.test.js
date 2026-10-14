import { beforeEach, describe, expect, it, vi } from "vitest";
import apiClient from "../../../../shared/api/apiClient";
import service from "../agentMonitoringService";

vi.mock("../../../../shared/api/apiClient", () => ({ default: { get: vi.fn() } }));
beforeEach(() => vi.resetAllMocks());

/**
 * TEST SUITE: Agent Monitoring Service Client
 *
 * 1. What this class/suite tests:
 *    - Verifies the HTTP API client methods in `agentMonitoringService` that fetch AI workflow
 *      telemetry and execution metrics from the ASP.NET Core backend.
 * 2. Why these tests are needed:
 *    - Ensures the client layer correctly routes requests to `/api/agent-monitoring`, unwraps
 *      response data envelopes, handles empty collections cleanly, and propagates Axios/network errors.
 * 3. What is real and what is mocked/faked:
 *    - Real: Service functions and JavaScript async execution.
 *    - Mocked: `apiClient.get` is mocked using Vitest `vi.fn()` to simulate backend responses.
 * 4. Prerequisites:
 *    - Node.js dependencies installed (`npm install` inside `/web`).
 * 5. PowerShell command to run this suite from repository root:
 *    npm --prefix web test -- src/features/aiWorkflows/services/__tests__/agentMonitoringService.test.js
 */
describe("agentMonitoringService", () => {
  /**
   * Purpose: What this specific test checks
   * - Verifies that `getMetrics()` sends an unparameterized GET request to `/api/agent-monitoring`
   *   and extracts `response.data`.
   *
   * Why: Why this scenario matters
   * - Validates the endpoint contract and payload unwrapping logic for AI agent monitoring data.
   *
   * Scenario: Input or condition being tested
   * - Backend returns `{ data: [{ id: "metric-1", status: "Completed" }] }`.
   *
   * Pass condition: What result or assertion makes it pass
   * - The returned promise resolves to the inner `data` array, and `apiClient.get` was called with `/api/agent-monitoring`.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/aiWorkflows/services/__tests__/agentMonitoringService.test.js -t "gets and returns metrics through the shared client"
   */
  it("gets and returns metrics through the shared client without query parameters", async () => {
    const data = [{ id: "metric-1", status: "Completed" }];
    apiClient.get.mockResolvedValue({ data });
    expect(await service.getMetrics()).toBe(data);
    expect(apiClient.get).toHaveBeenCalledExactlyOnceWith("/api/agent-monitoring");
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that `getMetrics()` returns an empty array `[]` when the backend reports no metrics.
   *
   * Why: Why this scenario matters
   * - Ensures callers receive empty arrays instead of null or undefined when no executions exist.
   *
   * Scenario: Input or condition being tested
   * - `apiClient.get` resolves with `{ data: [] }`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Service call resolves to `[]`.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/aiWorkflows/services/__tests__/agentMonitoringService.test.js -t "returns an empty response array"
   */
  it("returns an empty response array", async () => {
    apiClient.get.mockResolvedValue({ data: [] });
    expect(await service.getMetrics()).toEqual([]);
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that HTTP errors, 500s, or connectivity failures reject the promise without being swallowed.
   *
   * Why: Why this scenario matters
   * - UI components must catch network errors to render appropriate error banners and retry buttons.
   *
   * Scenario: Input or condition being tested
   * - `apiClient.get` rejects with `new Error("Network Error")`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Service promise rejects with the exact error object.
   *
   * Run from repository root:
   * npm --prefix web test -- src/features/aiWorkflows/services/__tests__/agentMonitoringService.test.js -t "propagates client errors unchanged"
   */
  it("propagates client errors unchanged", async () => {
    const error = new Error("Network Error");
    apiClient.get.mockRejectedValue(error);
    await expect(service.getMetrics()).rejects.toBe(error);
  });
});
