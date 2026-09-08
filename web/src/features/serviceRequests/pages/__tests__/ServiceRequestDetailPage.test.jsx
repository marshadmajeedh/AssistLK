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

  describe("Lifecycle-based UI rendering", () => {
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

    it("renders Cancelled state: read-only, all actions absent", async () => {
      serviceRequestService.getById.mockResolvedValueOnce({
        serviceRequestId: "req-100",
        status: "Cancelled",
        description: "Cancelled request",
        locationText: "Matara",
      });

      renderDetailPage();

      expect(await screen.findByText("Cancelled")).toBeInTheDocument();
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

  describe("Analysis flow & Clarification", () => {
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

  describe("Transient vs Persisted Architecture", () => {
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

  describe("Ready-For-Matching Confirmation", () => {
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

  describe("Cancellation Flow", () => {
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

      expect(await screen.findByText("Cancelled")).toBeInTheDocument();
      expect(
        screen.queryByRole("button", { name: /cancel request/i })
      ).not.toBeInTheDocument();
    });

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
