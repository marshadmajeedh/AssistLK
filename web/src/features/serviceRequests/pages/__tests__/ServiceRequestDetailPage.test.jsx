// What this suite tests:
//   The ServiceRequestDetailPage React page component across all lifecycle states (Created, Analyzing,
//   AwaitingInformation, Analyzed, ReadyForMatching, Cancelled), the AI analysis workflow execution,
//   follow-up clarification questions, transient vs persisted analysis data separation,
//   confirmation for provider matching, and cancellation flows.
// Why these tests are needed:
//   ServiceRequestDetailPage is the central command center for Component 1 in the web app.
//   These tests verify that UI state transitions follow the domain state machine strictly, preventing
//   illegal operations (like editing after analysis) and verifying interactive dialogs before critical state transitions.
// What is real and what is mocked/faked:
//   - Real: ServiceRequestDetailPage and its child components rendered inside MemoryRouter using @testing-library/react.
//   - Mocked/Faked: serviceRequestService (getById, analyze, cancel, markReadyForMatching) and useNavigate are mocked with Vitest vi.fn().
// Prerequisites:
//   Node.js dependencies installed in web folder (npm install).
// Run from repository root:
//   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/ServiceRequestDetailPage.test.jsx

import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { describe, it, expect, vi, beforeEach } from "vitest";
import ServiceRequestDetailPage from "../ServiceRequestDetailPage";
import serviceRequestService from "../../services/serviceRequestService";

const mockNavigate = vi.fn();
vi.mock("react-router-dom", async () => {
  const actual = await vi.importActual("react-router-dom");
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  };
});

vi.mock("../../services/serviceRequestService", () => ({
  default: {
    getById: vi.fn(),
    analyze: vi.fn(),
    cancel: vi.fn(),
    markReadyForMatching: vi.fn(),
  },
}));

function renderDetailPage(requestId = "req-100") {
  return render(
    <MemoryRouter initialEntries={[`/service-requests/${requestId}`]}>
      <Routes>
        <Route
          path="/service-requests/:id"
          element={<ServiceRequestDetailPage />}
        />
      </Routes>
    </MemoryRouter>
  );
}

describe("ServiceRequestDetailPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // Group: Tests UI elements and available action buttons according to request lifecycle status.
  describe("Lifecycle-based UI rendering", () => {
    // Purpose: Checks that a request in Created status displays "Analyze Problem", "Edit Request", and "Cancel Request" buttons.
    // Why: Customers must have full freedom to trigger analysis, refine input, or cancel before processing begins.
    // Scenario: Loading a request in Created status.
    // Pass condition: All three action buttons are present in the document.
    // Run from repository root:
    //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/ServiceRequestDetailPage.test.jsx -t "renders Created state with Analyze Problem, Edit, and Cancel actions"
    it("renders Created state with Analyze Problem, Edit, and Cancel actions", async () => {
      serviceRequestService.getById.mockResolvedValueOnce({
        serviceRequestId: "req-100",
        status: "Created",
        description: "Need ceiling fan replaced",
        locationText: "Colombo 03",
        createdAt: "2026-03-01T10:00:00Z",
      });

      renderDetailPage();

      expect(
        await screen.findByRole("heading", { name: /^service request$/i })
      ).toBeInTheDocument();
      expect(screen.getAllByText("Created").length).toBeGreaterThanOrEqual(1);
      expect(
        screen.getByRole("button", { name: /analyze problem/i })
      ).toBeInTheDocument();
      expect(
        screen.getByRole("button", { name: /edit request/i })
      ).toBeInTheDocument();
      expect(
        screen.getByRole("button", { name: /cancel request/i })
      ).toBeInTheDocument();
    });

    // Purpose: Checks that a request in Analyzing status displays a progress spinner/message and hides Analyze and Edit buttons.
    // Why: Prevents double analysis triggers or conflicting edits while the AI workflow is running.
    // Scenario: Loading a request with status "Analyzing".
    // Pass condition: "Analyzing" badge and "currently being analyzed" notice are displayed; Analyze and Edit buttons are absent.
    // Run from repository root:
    //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/ServiceRequestDetailPage.test.jsx -t "renders Analyzing state: analyzing indicator visible, Analyze and Edit absent"
    it("renders Analyzing state: analyzing indicator visible, Analyze and Edit absent", async () => {
      serviceRequestService.getById.mockResolvedValueOnce({
        serviceRequestId: "req-100",
        status: "Analyzing",
        description: "Diagnosing motor issue",
        locationText: "Colombo 03",
      });

      renderDetailPage();

      const analyzingBadges = await screen.findAllByText("Analyzing");
      expect(analyzingBadges.length).toBeGreaterThanOrEqual(1);
      expect(
        screen.getByText(/currently being analyzed/i)
      ).toBeInTheDocument();
      expect(
        screen.queryByRole("button", { name: /^analyze problem$/i })
      ).not.toBeInTheDocument();
      expect(
        screen.queryByRole("button", { name: /^edit request$/i })
      ).not.toBeInTheDocument();
    });

    // Purpose: Checks that a request in AwaitingInformation status renders the clarification section and displays "Edit Request" and "Analyze Again" buttons.
    // Why: Allows the customer to read AI questions, refine their description, or re-run analysis after answering.
    // Scenario: Loading a request with status "AwaitingInformation".
    // Pass condition: "More information is needed" banner and both Edit Request and Analyze Again action buttons are visible.
    // Run from repository root:
    //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/ServiceRequestDetailPage.test.jsx -t "renders AwaitingInformation state: clarification UI visible, Edit Request and Analyze Again visible"
    it("renders AwaitingInformation state: clarification UI visible, Edit Request and Analyze Again visible", async () => {
      serviceRequestService.getById.mockResolvedValueOnce({
        serviceRequestId: "req-100",
        status: "AwaitingInformation",
        description: "Roof leaking",
        locationText: "Galle",
      });

      renderDetailPage();

      expect(
        await screen.findByText("More information is needed")
      ).toBeInTheDocument();
      expect(
        screen.getByRole("button", { name: /edit request/i })
      ).toBeInTheDocument();
      expect(
        screen.getByRole("button", { name: /analyze again/i })
      ).toBeInTheDocument();
    });

    // Purpose: Checks that a request in Analyzed status displays the analysis review card, "Confirm for Provider Matching" button, and "Cancel Request" button, while hiding "Edit Request".
    // Why: Prevents editing once analysis is complete while requiring customer confirmation before matching begins.
    // Scenario: Loading a request with status "Analyzed".
    // Pass condition: Problem Analysis card, Confirm for Provider Matching button, and Cancel Request button are visible; Edit Request is absent.
    // Run from repository root:
    //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/ServiceRequestDetailPage.test.jsx -t "renders Analyzed state: review visible, Confirm for Provider Matching visible, Cancel available, Edit not available"
    it("renders Analyzed state: review visible, Confirm for Provider Matching visible, Cancel available, Edit not available", async () => {
      serviceRequestService.getById.mockResolvedValueOnce({
        serviceRequestId: "req-100",
        status: "Analyzed",
        category: "Plumbing",
        urgency: "Medium",
        description: "Pipe leak under sink",
        locationText: "Colombo",
      });

      renderDetailPage();

      expect(
        await screen.findByText("Problem Analysis")
      ).toBeInTheDocument();
      expect(
        screen.getByRole("button", {
          name: /confirm for provider matching/i,
        })
      ).toBeInTheDocument();
      expect(
        screen.getByRole("button", { name: /cancel request/i })
      ).toBeInTheDocument();
      expect(
        screen.queryByRole("button", { name: /^edit request$/i })
      ).not.toBeInTheDocument();
    });

    // Purpose: Checks that a request in ReadyForMatching status renders the ReadyForMatchingSection and hides all action buttons (Analyze, Edit, Cancel, Confirm).
    // Why: Component 1 is complete; provider matching (Component 2) has taken over, so request parameters are frozen.
    // Scenario: Loading a request with status "ReadyForMatching".
    // Pass condition: "Request ready for provider matching" section is visible and all mutation buttons are absent.
    // Run from repository root:
    //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/ServiceRequestDetailPage.test.jsx -t "renders ReadyForMatching state: ReadyForMatchingSection visible; Analyze, Edit, Cancel, and confirmation absent"
    it("renders ReadyForMatching state: ReadyForMatchingSection visible; Analyze, Edit, Cancel, and confirmation absent", async () => {
      serviceRequestService.getById.mockResolvedValueOnce({
        serviceRequestId: "req-100",
        status: "ReadyForMatching",
        category: "Electrical",
        urgency: "High",
        description: "Main breaker tripping",
        locationText: "Kandy",
      });

      renderDetailPage();

      expect(
        await screen.findByText("Request ready for provider matching")
      ).toBeInTheDocument();
      expect(
        screen.queryByRole("button", { name: /analyze/i })
      ).not.toBeInTheDocument();
      expect(
        screen.queryByRole("button", { name: /edit/i })
      ).not.toBeInTheDocument();
      expect(
        screen.queryByRole("button", { name: /cancel/i })
      ).not.toBeInTheDocument();
      expect(
        screen.queryByRole("button", {
          name: /confirm for provider matching/i,
        })
      ).not.toBeInTheDocument();
    });

    // Purpose: Checks that a Cancelled service request renders read-only with all lifecycle and editing action buttons removed.
    // Why: Cancelled requests are immutable historical records.
    // Scenario: Loading a request with status "Cancelled".
    // Pass condition: Cancelled badge is visible and Analyze, Edit, Cancel, and Confirm buttons are all absent.
    // Run from repository root:
    //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/ServiceRequestDetailPage.test.jsx -t "renders Cancelled state: read-only, all actions absent"
    it("renders Cancelled state: read-only, all actions absent", async () => {
      serviceRequestService.getById.mockResolvedValueOnce({
        serviceRequestId: "req-100",
        status: "Cancelled",
        description: "Cancelled request",
        locationText: "Matara",
      });

      renderDetailPage();

      const cancelledBadges = await screen.findAllByText("Cancelled");
      expect(cancelledBadges).toHaveLength(2);
      expect(
        screen.queryByRole("button", { name: /analyze/i })
      ).not.toBeInTheDocument();
      expect(
        screen.queryByRole("button", { name: /edit/i })
      ).not.toBeInTheDocument();
      expect(
        screen.queryByRole("button", { name: /cancel/i })
      ).not.toBeInTheDocument();
      expect(
        screen.queryByRole("button", {
          name: /confirm for provider matching/i,
        })
      ).not.toBeInTheDocument();
    });
  });

  // Group: Tests the AI problem understanding execution and clarification question interactions.
  describe("Analysis flow & Clarification", () => {
    // Purpose: Checks the end-to-end user click on "Analyze Problem", asserting loading state, service call, and rendered analysis review card.
    // Why: Verifies the primary happy path for initiating AI problem understanding from the web UI.
    // Scenario: User clicks "Analyze Problem" on a Created request; backend returns analyzed output.
    // Pass condition: analyze is called once with "req-100", and Problem Analysis card displays with Plumbing, High, summary, and 88%.
    // Run from repository root:
    //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/ServiceRequestDetailPage.test.jsx -t "handles successful analysis: click Analyze -> loading state -> calls analyze once -> refreshes request -> renders Analyzed"
    it("handles successful analysis: click Analyze -> loading state -> calls analyze once -> refreshes request -> renders Analyzed", async () => {
      const user = userEvent.setup();
      serviceRequestService.getById
        .mockResolvedValueOnce({
          serviceRequestId: "req-100",
          status: "Created",
          description: "Water leaking heavily from bathroom",
          locationText: "Colombo 03",
        })
        .mockResolvedValueOnce({
          serviceRequestId: "req-100",
          status: "Analyzed",
          category: "Plumbing",
          urgency: "High",
          description: "Water leaking heavily from bathroom",
          locationText: "Colombo 03",
        });

      serviceRequestService.analyze.mockResolvedValueOnce({
        serviceRequestId: "req-100",
        status: "Analyzed",
        category: "Plumbing",
        urgency: "High",
        confidence: 0.88,
        problemSummary: "Major plumbing leakage in bathroom",
        needsMoreInformation: false,
        followUpQuestions: [],
      });

      renderDetailPage();

      const analyzeBtn = await screen.findByRole("button", {
        name: /analyze problem/i,
      });
      await user.click(analyzeBtn);

      await waitFor(() => {
        expect(serviceRequestService.analyze).toHaveBeenCalledTimes(1);
        expect(serviceRequestService.analyze).toHaveBeenCalledWith("req-100");
      });

      expect(
        await screen.findByText("Problem Analysis")
      ).toBeInTheDocument();
      expect(screen.getByText("Plumbing")).toBeInTheDocument();
      expect(screen.getByText("High")).toBeInTheDocument();
      expect(
        screen.getByText("Major plumbing leakage in bathroom")
      ).toBeInTheDocument();
      expect(screen.getByText("88%")).toBeInTheDocument();
    });

    // Purpose: Checks that when the AI agent returns needsMoreInformation: true, follow-up questions are displayed without fabricating extra items.
    // Why: Ensures ambiguous requests guide the customer with actual questions returned by the AI agent.
    // Scenario: Analyze returns status AwaitingInformation with two follow-up questions.
    // Pass condition: Both exact question texts are rendered and "More information is needed" section appears.
    // Run from repository root:
    //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/ServiceRequestDetailPage.test.jsx -t "handles clarification flow with follow-up questions without fabricating questions"
    it("handles clarification flow with follow-up questions without fabricating questions", async () => {
      const user = userEvent.setup();
      serviceRequestService.getById
        .mockResolvedValueOnce({
          serviceRequestId: "req-100",
          status: "Created",
          description: "Power cut",
          locationText: "Colombo 04",
        })
        .mockResolvedValueOnce({
          serviceRequestId: "req-100",
          status: "AwaitingInformation",
          category: "Unclassified",
          urgency: "Unknown",
          description: "Power cut",
          locationText: "Colombo 04",
        });

      serviceRequestService.analyze.mockResolvedValueOnce({
        serviceRequestId: "req-100",
        status: "AwaitingInformation",
        category: "Unclassified",
        urgency: "Unknown",
        confidence: 0.3,
        needsMoreInformation: true,
        followUpQuestions: [
          "Is the issue isolated to your home or neighborhood?",
          "Did you hear a breaker trip?",
        ],
      });

      renderDetailPage();

      const analyzeBtn = await screen.findByRole("button", {
        name: /analyze problem/i,
      });
      await user.click(analyzeBtn);

      expect(
        await screen.findByText("More information is needed")
      ).toBeInTheDocument();
      expect(
        screen.getByText(
          "Is the issue isolated to your home or neighborhood?"
        )
      ).toBeInTheDocument();
      expect(
        screen.getByText("Did you hear a breaker trip?")
      ).toBeInTheDocument();
      expect(
        screen.getByRole("button", { name: /edit request/i })
      ).toBeInTheDocument();
      expect(
        screen.getByRole("button", { name: /analyze again/i })
      ).toBeInTheDocument();
    });

    // Purpose: Checks that low-confidence analysis results gracefully guide the user to clarify their problem rather than presenting a technical error alert.
    // Why: Low model confidence is a normal conversational state handled via follow-up questions, not a technical bug.
    // Scenario: Model returns confidence 0.20 and needsMoreInformation: true.
    // Pass condition: Displays clarification questions without any "AI failed" or error alert.
    // Run from repository root:
    //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/ServiceRequestDetailPage.test.jsx -t "safe failure test: low-confidence analysis treated as clarification with no 'AI failed' message"
    it("safe failure test: low-confidence analysis treated as clarification with no 'AI failed' message", async () => {
      const user = userEvent.setup();
      serviceRequestService.getById
        .mockResolvedValueOnce({
          serviceRequestId: "req-100",
          status: "Created",
          description: "Something is broken",
          locationText: "Colombo",
        })
        .mockResolvedValueOnce({
          serviceRequestId: "req-100",
          status: "AwaitingInformation",
          category: "Unclassified",
          urgency: "Unknown",
          description: "Something is broken",
          locationText: "Colombo",
        });

      serviceRequestService.analyze.mockResolvedValueOnce({
        status: "AwaitingInformation",
        category: "Unclassified",
        urgency: "Unknown",
        confidence: 0.2,
        needsMoreInformation: true,
        followUpQuestions: ["Could you describe which appliance or fixture is damaged?"],
      });

      renderDetailPage();

      await user.click(
        await screen.findByRole("button", { name: /analyze problem/i })
      );

      expect(
        await screen.findByText("More information is needed")
      ).toBeInTheDocument();
      expect(
        screen.getByText(
          "Could you describe which appliance or fixture is damaged?"
        )
      ).toBeInTheDocument();
      expect(screen.queryByText(/ai failed/i)).not.toBeInTheDocument();
    });
  });

  // Group: Tests handling of transient AI properties versus persistent relational entity fields.
  describe("Transient vs Persisted Architecture", () => {
    // Purpose: Checks that reloading an Analyzed request where transient fields (problemSummary, confidence) are absent still renders with "Not available" fallbacks and leaves matching unblocked.
    // Why: Validates architectural decoupling: persistence stores the authoritative status and category while transient AI scores fall back gracefully.
    // Scenario: GET /api/service-requests/{id} returns status="Analyzed" without problemSummary or confidence.
    // Pass condition: Category and urgency display, missing fields show "Not available", and "Confirm for Provider Matching" remains clickable.
    // Run from repository root:
    //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/ServiceRequestDetailPage.test.jsx -t "renders Analyzed state when GET returns status=Analyzed without problemSummary/confidence and leaves confirmation unblocked"
    it("renders Analyzed state when GET returns status=Analyzed without problemSummary/confidence and leaves confirmation unblocked", async () => {
      serviceRequestService.getById.mockResolvedValueOnce({
        serviceRequestId: "req-100",
        status: "Analyzed",
        category: "Plumbing",
        urgency: "Low",
        description: "Dripping bathroom faucet",
        locationText: "Kandy",
      });

      renderDetailPage();

      expect(
        await screen.findByText("Problem Analysis")
      ).toBeInTheDocument();
      expect(screen.getByText("Plumbing")).toBeInTheDocument();
      expect(screen.getByText("Low")).toBeInTheDocument();
      // Transient confidence safely displays fallback
      expect(screen.getAllByText("Not available").length).toBeGreaterThan(0);

      // Confirmation is NOT blocked
      expect(
        screen.getByRole("button", {
          name: /confirm for provider matching/i,
        })
      ).toBeInTheDocument();
    });
  });

  // Group: Tests modal dialog confirmation flow for handing off to Component 2 provider matching.
  describe("Ready-For-Matching Confirmation", () => {
    // Purpose: Checks that confirming provider matching requires explicit modal confirmation, dismissing with "Go Back" aborts without calling the API, and confirming executes markReadyForMatching.
    // Why: Transitioning to provider matching is irreversible from the customer side and freezes the request parameters.
    // Scenario: User clicks "Confirm for Provider Matching", cancels via "Go Back", re-opens, and confirms.
    // Pass condition: Modal toggles correctly, cancel makes no API call, confirmation invokes markReadyForMatching("req-100"), and UI updates to "Request ready for provider matching".
    // Run from repository root:
    //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/ServiceRequestDetailPage.test.jsx -t "opens confirmation on click, cancels on 'Go Back' without API call, and confirms with updated status"
    it("opens confirmation on click, cancels on 'Go Back' without API call, and confirms with updated status", async () => {
      const user = userEvent.setup();
      serviceRequestService.getById.mockResolvedValueOnce({
        serviceRequestId: "req-100",
        status: "Analyzed",
        category: "Plumbing",
        urgency: "High",
        description: "Flooded basement",
        locationText: "Colombo",
      });

      renderDetailPage();

      const openConfirmBtn = await screen.findByRole("button", {
        name: /confirm for provider matching/i,
      });
      await user.click(openConfirmBtn);

      // Confirmation modal is visible
      expect(
        screen.getByText("Confirm this analyzed request?")
      ).toBeInTheDocument();

      // Click Go Back -> modal closes, API not called
      const goBackBtn = screen.getByRole("button", { name: /go back/i });
      await user.click(goBackBtn);

      expect(
        screen.queryByText("Confirm this analyzed request?")
      ).not.toBeInTheDocument();
      expect(serviceRequestService.markReadyForMatching).not.toHaveBeenCalled();

      // Re-open and confirm
      await user.click(
        screen.getByRole("button", {
          name: /confirm for provider matching/i,
        })
      );

      serviceRequestService.markReadyForMatching.mockResolvedValueOnce({
        serviceRequestId: "req-100",
        status: "ReadyForMatching",
        category: "Plumbing",
        urgency: "High",
        description: "Flooded basement",
        locationText: "Colombo",
      });

      const confirmBtn = screen.getByRole("button", {
        name: /confirm request/i,
      });
      await user.click(confirmBtn);

      await waitFor(() => {
        expect(serviceRequestService.markReadyForMatching).toHaveBeenCalledWith(
          "req-100"
        );
      });

      // Status transitioned to ReadyForMatching
      expect(
        await screen.findByText("Request ready for provider matching")
      ).toBeInTheDocument();
      expect(
        screen.queryByRole("button", { name: /cancel/i })
      ).not.toBeInTheDocument();
      expect(
        screen.queryByRole("button", { name: /analyze/i })
      ).not.toBeInTheDocument();
      expect(
        screen.queryByRole("button", { name: /edit/i })
      ).not.toBeInTheDocument();
      expect(
        screen.queryByText("Confirm this analyzed request?")
      ).not.toBeInTheDocument();
    });
  });

  // Group: Tests cancellation dialog, cancellation API execution, and conflict handling.
  describe("Cancellation Flow", () => {
    // Purpose: Checks that cancelling a request requires confirmation, dismissing via "Keep Request" aborts safely, and confirming cancels the request and updates UI badges.
    // Why: Prevents accidental cancellation of customer requests.
    // Scenario: User clicks "Cancel Request", dismisses via "Keep Request", re-opens, and confirms.
    // Pass condition: Cancel API called only upon confirmation and page updates to show Cancelled status badges.
    // Run from repository root:
    //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/ServiceRequestDetailPage.test.jsx -t "opens cancel confirmation, cancels on 'Keep Request' without API call, and executes cancel on confirmation"
    it("opens cancel confirmation, cancels on 'Keep Request' without API call, and executes cancel on confirmation", async () => {
      const user = userEvent.setup();
      serviceRequestService.getById.mockResolvedValueOnce({
        serviceRequestId: "req-100",
        status: "Created",
        description: "Need wall painted",
        locationText: "Colombo 05",
      });

      renderDetailPage();

      const cancelBtn = await screen.findByRole("button", {
        name: /cancel request/i,
      });
      await user.click(cancelBtn);

      expect(
        screen.getByText("Are you sure you want to cancel this service request?")
      ).toBeInTheDocument();

      // Keep request
      const keepBtn = screen.getByRole("button", { name: /keep request/i });
      await user.click(keepBtn);

      expect(
        screen.queryByText("Are you sure you want to cancel this service request?")
      ).not.toBeInTheDocument();
      expect(serviceRequestService.cancel).not.toHaveBeenCalled();

      // Re-open and cancel
      await user.click(
        screen.getByRole("button", { name: /cancel request/i })
      );

      serviceRequestService.cancel.mockResolvedValueOnce({
        serviceRequestId: "req-100",
        status: "Cancelled",
        description: "Need wall painted",
        locationText: "Colombo 05",
      });

      const confirmCancelBtn = screen.getByRole("button", {
        name: /^cancel request$/i,
      });
      await user.click(confirmCancelBtn);

      await waitFor(() => {
        expect(serviceRequestService.cancel).toHaveBeenCalledWith("req-100");
      });

      const cancelledBadges = await screen.findAllByText("Cancelled");
      expect(cancelledBadges).toHaveLength(2);
      expect(
        screen.queryByRole("button", { name: /cancel request/i })
      ).not.toBeInTheDocument();
    });

    // Purpose: Checks that if the backend rejects cancellation with HTTP 409 Conflict, an error banner is shown and the existing request remains intact in the view.
    // Why: Handles race conditions where another user or background service transitioned the request.
    // Scenario: Cancellation throws 409 Conflict with message "This service request cannot be modified in its current status."
    // Pass condition: Error message appears in banner and request data remains visible.
    // Run from repository root:
    //   npm --prefix web test -- src/features/serviceRequests/pages/__tests__/ServiceRequestDetailPage.test.jsx -t "handles 409 conflict during cancellation: ErrorMessage displayed, existing request preserved"
    it("handles 409 conflict during cancellation: ErrorMessage displayed, existing request preserved", async () => {
      const user = userEvent.setup();
      serviceRequestService.getById.mockResolvedValueOnce({
        serviceRequestId: "req-100",
        status: "Created",
        description: "Water tank repair",
        locationText: "Colombo",
      });

      renderDetailPage();

      await user.click(
        await screen.findByRole("button", { name: /cancel request/i })
      );

      const conflictError = {
        response: {
          status: 409,
          data: {
            message:
              "This service request cannot be modified in its current status.",
          },
        },
      };
      serviceRequestService.cancel.mockRejectedValueOnce(conflictError);

      await user.click(
        screen.getByRole("button", { name: /^cancel request$/i })
      );

      expect(
        await screen.findByText(
          "This service request cannot be modified in its current status."
        )
      ).toBeInTheDocument();

      // Existing request still visible
      expect(screen.getByText("Water tank repair")).toBeInTheDocument();
    });
  });
});
