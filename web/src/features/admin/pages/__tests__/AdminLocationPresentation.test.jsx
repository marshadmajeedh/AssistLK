// What this suite tests:
//   Location and geographic coordinate presentation in the Admin Service Request List and Detail panel,
//   verifying OpenStreetMap copyright attribution, manual source formatting, exact coordinate display
//   (including 0.0 values), omission of empty coordinate sections, and address fallbacks.
// Why these tests are needed:
//   Ensures legal compliance with OpenStreetMap license attribution requirements, preserves accurate
//   geographic coordinate data for administrators without UI truncation, and handles partial or missing data safely.
// What is real and what is mocked/faked:
//   - Real: AdminServiceRequestListPage rendered with @testing-library/react and user-event interactions.
//   - Mocked/Faked: apiClient.get is mocked via Vitest vi.fn() to return controlled admin request entities.
// Prerequisites:
//   Node.js dependencies installed in web folder (npm install).
// Run from repository root:
//   npm --prefix web test -- src/features/admin/pages/__tests__/AdminLocationPresentation.test.jsx

import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import AdminServiceRequestListPage from "../AdminServiceRequestListPage";
import apiClient from "../../../../shared/api/apiClient";

vi.mock("../../../../shared/api/apiClient", () => ({ default: { get: vi.fn(), post: vi.fn() } }));
const id = "a1b2c3d4-1234-4567-8901-123456789abc";
const request = { serviceRequestId: id, category: "Plumbing", status: "Created", urgency: "Low",
  locationText: "Battaramulla, Sri Jayawardenepura Kotte", locationSource: "Manual", latitude: 6.905, longitude: 79.9195 };
beforeEach(() => vi.resetAllMocks());
afterEach(() => vi.unstubAllGlobals());

async function showLocation(values = {}, detailValues = {}) {
  apiClient.get.mockResolvedValueOnce({ data: [{ ...request, ...values }] })
    .mockResolvedValueOnce({ data: { ...request, ...values, ...detailValues } });
  const user = userEvent.setup();
  render(<AdminServiceRequestListPage />);
  await user.click(await screen.findByRole("button", { name: "View Details" }));
  const detail = within(screen.getByRole("region", { name: "Request details" }));
  await detail.findByText(id);
  return { detail, table: within(screen.getByRole("table")) };
}

describe("Persisted Admin location presentation", () => {
  // Purpose: Checks that non-OSM location sources (Manual, undefined, unexpected) render plain address text without OpenStreetMap copyright links.
  // Why: OSM attribution must only be displayed when data originated from OpenStreetMap.
  // Scenario: Parameterized test for locationSource = "Manual", undefined, and "Unexpected".
  // Pass condition: Address text is present in table and detail; OSM link is not in the document.
  // Note: Parameterized Vitest it.each test.
  // Run from repository root:
  //   npm --prefix web test -- src/features/admin/pages/__tests__/AdminLocationPresentation.test.jsx -t "shows readable text without attribution for"
  it.each(["Manual", undefined, "Unexpected"])("shows readable text without attribution for %s", async (locationSource) => {
    const { detail, table } = await showLocation({ locationSource });
    expect(detail.getByText(request.locationText)).toBeInTheDocument();
    expect(table.getByText(request.locationText)).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "© OpenStreetMap contributors" })).not.toBeInTheDocument();
  });

  // Purpose: Checks that when locationSource is "OpenStreetMap", copyright link with correct href is shown adjacent to address.
  // Why: Legal requirement to credit OpenStreetMap contributors when geocoding data comes from OSM.
  // Scenario: Loading a request with locationSource="OpenStreetMap".
  // Pass condition: Credit link points to openstreetmap.org/copyright in both table and detail view, and GPS coordinates display in detail.
  // Run from repository root:
  //   npm --prefix web test -- src/features/admin/pages/__tests__/AdminLocationPresentation.test.jsx -t "attributes OSM content adjacent to the full address in list and details"
  it("attributes OSM content adjacent to the full address in list and details", async () => {
    const { detail, table } = await showLocation({ locationSource: "OpenStreetMap" });
    for (const scope of [table, detail]) {
      const credit = scope.getByRole("link", { name: "© OpenStreetMap contributors" });
      expect(credit).toHaveAttribute("href", "https://www.openstreetmap.org/copyright");
      expect(credit.parentElement).toHaveTextContent(request.locationText);
    }
    expect(table.queryByText("6.905")).not.toBeInTheDocument();
    expect(detail.getByRole("heading", { name: "GPS coordinates" })).toBeInTheDocument();
    expect(detail.getByText("6.905")).toBeInTheDocument();
    expect(detail.getByText("79.9195")).toBeInTheDocument();
  });

  // Purpose: Checks that coordinate values of 0.0 are treated as valid numbers and not discarded as falsy values.
  // Why: 0.0 is a valid geographic coordinate and must not be hidden by truthiness checks.
  // Scenario: Request with latitude=0 and longitude=0.
  // Pass condition: Two "0" values are displayed in the GPS coordinates section.
  // Run from repository root:
  //   npm --prefix web test -- src/features/admin/pages/__tests__/AdminLocationPresentation.test.jsx -t "preserves both zero coordinate values"
  it("preserves both zero coordinate values", async () => {
    const { detail } = await showLocation({ latitude: 0, longitude: 0 });
    expect(detail.getAllByText("0")).toHaveLength(2);
  });

  // Purpose: Checks that when coordinates are null or undefined, the technical GPS section is omitted entirely.
  // Why: Avoids confusing empty "GPS coordinates" headers when no coordinates were captured.
  // Scenario: Request with latitude=null, longitude=undefined.
  // Pass condition: Heading "GPS coordinates" and Latitude/Longitude labels are not in the DOM.
  // Run from repository root:
  //   npm --prefix web test -- src/features/admin/pages/__tests__/AdminLocationPresentation.test.jsx -t "omits missing coordinates without an empty technical section"
  it("omits missing coordinates without an empty technical section", async () => {
    const { detail } = await showLocation({ latitude: null, longitude: undefined });
    expect(detail.queryByText("GPS coordinates")).not.toBeInTheDocument();
    expect(detail.queryByText("Latitude")).not.toBeInTheDocument();
    expect(detail.queryByText("Longitude")).not.toBeInTheDocument();
  });

  // Purpose: Checks that if only one coordinate is present, the available coordinate is rendered without crashing.
  // Why: Diagnostic resilience for partial location telemetry.
  // Scenario: latitude=0, longitude=null.
  // Pass condition: Latitude and "0" are rendered, Longitude is omitted.
  // Run from repository root:
  //   npm --prefix web test -- src/features/admin/pages/__tests__/AdminLocationPresentation.test.jsx -t "preserves the existing partial-coordinate diagnostic behavior"
  it("preserves the existing partial-coordinate diagnostic behavior", async () => {
    const { detail } = await showLocation({ latitude: 0, longitude: null });
    expect(detail.getByText("Latitude")).toBeInTheDocument();
    expect(detail.getByText("0")).toBeInTheDocument();
    expect(detail.queryByText("Longitude")).not.toBeInTheDocument();
  });

  // Purpose: Checks that blank, empty, or undefined address strings render a neutral dash ("—") without fabricating fake text.
  // Why: Clear visual placeholder without misleading address text.
  // Scenario: Parameterized test for undefined, null, "", and whitespace address text.
  // Pass condition: Address container displays "—" and no OSM attribution is shown.
  // Note: Parameterized Vitest it.each test.
  // Run from repository root:
  //   npm --prefix web test -- src/features/admin/pages/__tests__/AdminLocationPresentation.test.jsx -t "uses a neutral fallback for missing address"
  it.each([undefined, null, "", "   "])("uses a neutral fallback for missing address %s without fabricated text or attribution", async (locationText) => {
    const { detail } = await showLocation({ locationText, locationSource: "OpenStreetMap" });
    const address = detail.getByRole("heading", { name: "Location" }).nextElementSibling;
    expect(address).toHaveTextContent("—");
    expect(address).not.toHaveTextContent("6.905");
    expect(screen.queryByRole("link", { name: "© OpenStreetMap contributors" })).not.toBeInTheDocument();
  });

  // Purpose: Checks that multi-line or long address strings render completely in the detail panel without truncation.
  // Why: Admins must be able to read full delivery/service addresses.
  // Scenario: Request with a multi-line Sri Lankan address string.
  // Pass condition: Full address text is preserved in textContent with CSS class "monitoring-location-address".
  // Run from repository root:
  //   npm --prefix web test -- src/features/admin/pages/__tests__/AdminLocationPresentation.test.jsx -t "renders the complete long address without truncation"
  it("renders the complete long address without truncation", async () => {
    const locationText = "No. 123, Denzil Kobbekaduwa Mawatha, Battaramulla,\nSri Jayawardenepura Kotte, Western Province, Sri Lanka";
    const { detail } = await showLocation({ locationText, locationSource: "OpenStreetMap" });
    const address = detail.getByRole("heading", { name: "Location" }).nextElementSibling.querySelector("p");
    expect(address.textContent).toBe(locationText);
    expect(address).toHaveClass("monitoring-location-address");
  });

  // Purpose: Checks that opening details fetches fresh authoritative data using exactly two GET requests and no extraneous calls.
  // Why: Network efficiency and data freshness in admin monitoring.
  // Scenario: User opens detail panel; verifies dispatched API calls.
  // Pass condition: Exactly two GET calls (/api/admin/service-requests, /api/admin/service-requests/{id}), zero POSTs, zero fetch calls.
  // Run from repository root:
  //   npm --prefix web test -- src/features/admin/pages/__tests__/AdminLocationPresentation.test.jsx -t "uses fresh Admin detail data and makes only the two Admin GET requests"
  it("uses fresh Admin detail data and makes only the two Admin GET requests", async () => {
    const fetch = vi.fn();
    vi.stubGlobal("fetch", fetch);
    const { detail } = await showLocation({}, { locationText: "Updated authoritative address", locationSource: "OpenStreetMap" });
    expect(detail.getByText("Updated authoritative address")).toBeInTheDocument();
    expect(detail.getByRole("link", { name: "© OpenStreetMap contributors" })).toBeInTheDocument();
    expect(apiClient.get.mock.calls).toEqual([
      ["/api/admin/service-requests", { params: {} }], [`/api/admin/service-requests/${id}`],
    ]);
    expect(apiClient.post).not.toHaveBeenCalled();
    expect(fetch).not.toHaveBeenCalled();
  });
});
