import React, { useState } from "react";
import ProviderVerificationQueue from "./ProviderVerificationQueue";
import MatchApprovalsPage from "./MatchApprovalsPage";
import { colors, typography, spacing, radius } from "../../shared/theme";

function ProvidersDashboardPage() {
  const [activeTab, setActiveTab] = useState("verification");

  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        gap: spacing.lg,
      }}
    >
      <div
        style={{
          borderBottom: `1px solid ${colors.border}`,
          display: "flex",
          gap: spacing.md,
        }}
      >
        <button
          onClick={() => setActiveTab("verification")}
          style={{
            background: "none",
            border: "none",
            padding: `${spacing.sm}px ${spacing.sm}px`,
            borderBottom: activeTab === "verification" ? `3px solid ${colors.primary}` : "3px solid transparent",
            color: activeTab === "verification" ? colors.primary : colors.textSecondary,
            fontFamily: typography.fontFamily,
            fontSize: typography.button.fontSize,
            fontWeight: 600,
            cursor: "pointer",
            transition: "all 0.2s ease",
          }}
        >
          Verification Queue
        </button>
        <button
          onClick={() => setActiveTab("match-approvals")}
          style={{
            background: "none",
            border: "none",
            padding: `${spacing.sm}px ${spacing.sm}px`,
            borderBottom: activeTab === "match-approvals" ? `3px solid ${colors.primary}` : "3px solid transparent",
            color: activeTab === "match-approvals" ? colors.primary : colors.textSecondary,
            fontFamily: typography.fontFamily,
            fontSize: typography.button.fontSize,
            fontWeight: 600,
            cursor: "pointer",
            transition: "all 0.2s ease",
          }}
        >
          Match Approvals (HITL Gate)
        </button>
      </div>

      <div>
        {activeTab === "verification" && <ProviderVerificationQueue />}
        {activeTab === "match-approvals" && <MatchApprovalsPage />}
      </div>
    </div>
  );
}

export default ProvidersDashboardPage;
