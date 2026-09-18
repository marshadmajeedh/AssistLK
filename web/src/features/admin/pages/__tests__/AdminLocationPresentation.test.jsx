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
  it.each(["Manual", undefined, "Unexpected"])("shows readable text without attribution for %s", async (locationSource) => {
    const { detail, table } = await showLocation({ locationSource });
    expect(detail.getByText(request.locationText)).toBeInTheDocument();
    expect(table.getByText(request.locationText)).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "© OpenStreetMap contributors" })).not.toBeInTheDocument();
  });

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

  it("preserves both zero coordinate values", async () => {
    const { detail } = await showLocation({ latitude: 0, longitude: 0 });
    expect(detail.getAllByText("0")).toHaveLength(2);
  });

  it("omits missing coordinates without an empty technical section", async () => {
    const { detail } = await showLocation({ latitude: null, longitude: undefined });
    expect(detail.queryByText("GPS coordinates")).not.toBeInTheDocument();
    expect(detail.queryByText("Latitude")).not.toBeInTheDocument();
    expect(detail.queryByText("Longitude")).not.toBeInTheDocument();
  });

  it("preserves the existing partial-coordinate diagnostic behavior", async () => {
    const { detail } = await showLocation({ latitude: 0, longitude: null });
    expect(detail.getByText("Latitude")).toBeInTheDocument();
    expect(detail.getByText("0")).toBeInTheDocument();
    expect(detail.queryByText("Longitude")).not.toBeInTheDocument();
  });

  it.each([undefined, null, "", "   "])("uses a neutral fallback for missing address %s without fabricated text or attribution", async (locationText) => {
    const { detail } = await showLocation({ locationText, locationSource: "OpenStreetMap" });
    const address = detail.getByRole("heading", { name: "Location" }).nextElementSibling;
    expect(address).toHaveTextContent("—");
    expect(address).not.toHaveTextContent("6.905");
    expect(screen.queryByRole("link", { name: "© OpenStreetMap contributors" })).not.toBeInTheDocument();
  });

  it("renders the complete long address without truncation", async () => {
    const locationText = "No. 123, Denzil Kobbekaduwa Mawatha, Battaramulla,\nSri Jayawardenepura Kotte, Western Province, Sri Lanka";
    const { detail } = await showLocation({ locationText, locationSource: "OpenStreetMap" });
    const address = detail.getByRole("heading", { name: "Location" }).nextElementSibling.querySelector("p");
    expect(address.textContent).toBe(locationText);
    expect(address).toHaveClass("monitoring-location-address");
  });

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
