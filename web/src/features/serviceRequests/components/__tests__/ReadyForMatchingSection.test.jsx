// What this suite tests:
//   The ReadyForMatchingSection React component which confirms to the customer that their
//   service request has finished problem understanding analysis and is prepared for provider matching.
// Why these tests are needed:
//   Ensures confirmed categorization, urgency, and location are displayed to the user,
//   navigation back to the customer's request list works, and null request props fall back safely.
// What is real and what is mocked/faked:
//   - Real: ReadyForMatchingSection component rendered inside MemoryRouter using @testing-library/react.
//   - Mocked/Faked: react-router-dom useNavigate hook is mocked to verify route navigation without changing browser location.
// Prerequisites:
//   Node.js dependencies installed in web folder (npm install).
// Run from repository root:
//   npm --prefix web test -- src/features/serviceRequests/components/__tests__/ReadyForMatchingSection.test.jsx

import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { describe, it, expect, vi } from "vitest";
import ReadyForMatchingSection from "../ReadyForMatchingSection";

const mockNavigate = vi.fn();
vi.mock("react-router-dom", async () => {
  const actual = await vi.importActual("react-router-dom");
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  };
});

describe("ReadyForMatchingSection", () => {
  const sampleRequest = {
    category: "Plumbing",
    urgency: "High",
    locationText: "Colombo 03",
    status: "ReadyForMatching",
  };

  // Purpose: Checks that confirmed category, urgency, and location text are displayed in the section.
  // Why: Provides customer with full visibility into the final parameters before matching begins.
  // Scenario: Providing a valid request object with status "ReadyForMatching".
  // Pass condition: Heading "Request ready for provider matching" and field values are visible in DOM.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/components/__tests__/ReadyForMatchingSection.test.jsx -t "renders confirmed category, urgency, location, and status"
  it("renders confirmed category, urgency, location, and status", () => {
    render(
      <MemoryRouter>
        <ReadyForMatchingSection request={sampleRequest} />
      </MemoryRouter>
    );

    expect(
      screen.getByText("Request ready for provider matching")
    ).toBeInTheDocument();
    expect(screen.getByText("Plumbing")).toBeInTheDocument();
    expect(screen.getByText("High")).toBeInTheDocument();
    expect(screen.getByText("Colombo 03")).toBeInTheDocument();
  });

  // Purpose: Checks that clicking "Back to My Requests" invokes navigate with "/service-requests".
  // Why: Verifies that return navigation redirects the customer to their request overview dashboard.
  // Scenario: User clicks the "Back to My Requests" button.
  // Pass condition: mockNavigate is called with "/service-requests".
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/components/__tests__/ReadyForMatchingSection.test.jsx -t "navigates to service requests list when Back to My Requests is clicked"
  it("navigates to service requests list when Back to My Requests is clicked", async () => {
    const user = userEvent.setup();
    render(
      <MemoryRouter>
        <ReadyForMatchingSection request={sampleRequest} />
      </MemoryRouter>
    );

    const backBtn = screen.getByRole("button", {
      name: /back to my requests/i,
    });
    await user.click(backBtn);

    expect(mockNavigate).toHaveBeenCalledWith("/service-requests");
  });

  // Purpose: Checks that passing a null request prop defaults category, urgency, and location to fallback values.
  // Why: Prevents component crashes when the request object is missing or still loading.
  // Scenario: Rendering with request={null}.
  // Pass condition: "Unclassified", "Unknown", and "Not provided" text elements are rendered.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/components/__tests__/ReadyForMatchingSection.test.jsx -t "handles missing request properties with safe fallbacks"
  it("handles missing request properties with safe fallbacks", () => {
    render(
      <MemoryRouter>
        <ReadyForMatchingSection request={null} />
      </MemoryRouter>
    );

    expect(screen.getByText("Unclassified")).toBeInTheDocument();
    expect(screen.getByText("Unknown")).toBeInTheDocument();
    expect(screen.getByText("Not provided")).toBeInTheDocument();
  });
});
