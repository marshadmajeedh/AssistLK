import { useState } from "react";
import AppCard from "../../../shared/components/AppCard";
import AppButton from "../../../shared/components/AppButton";
import { colors, spacing, typography } from "../../../shared/theme";

const volumeTrendData = [
  { month: "Jan", volume: 32, x: 20, y: 150 },
  { month: "Feb", volume: 41, x: 70, y: 130 },
  { month: "Mar", volume: 38, x: 120, y: 136 },
  { month: "Apr", volume: 52, x: 170, y: 104 },
  { month: "May", volume: 61, x: 220, y: 84 },
  { month: "Jun", volume: 68, x: 270, y: 68 },
  { month: "Jul", volume: 76, x: 320, y: 50 },
  { month: "Aug", volume: 84, x: 370, y: 32 },
];

const initialActiveRequests = [
  {
    id: "SR-2048",
    title: "Water leak · Colombo 05",
    category: "Plumbing",
    status: "AI Review",
    time: "10 mins ago",
  },
  {
    id: "SR-2047",
    title: "Vehicle breakdown · Kandy",
    category: "Vehicle Repair",
    status: "Provider Matched",
    time: "24 mins ago",
  },
  {
    id: "SR-2046",
    title: "Power fault · Galle",
    category: "Electrical",
    status: "Awaiting Quote",
    time: "1 hour ago",
  },
  {
    id: "SR-2045",
    title: "AC repair · Negombo",
    category: "HVAC",
    status: "In Progress",
    time: "2 hours ago",
  },
];

const componentBoundaries = [
  {
    name: "Component 1",
    scope: "Service Request & Problem Understanding",
    owner: "Member 1",
    agent: "ProblemUnderstandingAgent",
    status: "Operational",
  },
  {
    name: "Component 2",
    scope: "Provider Management & Intelligent Matching",
    owner: "Member 2",
    agent: "ProviderMatchingAgent",
    status: "Ready for Integration",
  },
  {
    name: "Component 3",
    scope: "Quotation, Booking & Service Coordination",
    owner: "Member 3",
    agent: "ServiceCoordinationAgent",
    status: "Ready for Integration",
  },
  {
    name: "Component 4",
    scope: "Service Tracking, Completion & Safety",
    owner: "Member 4",
    agent: "ValidationSafetyAgent",
    status: "Ready for Integration",
  },
];

function TrendIcon() {
  return (
    <svg
      width="14"
      height="14"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      style={{ verticalAlign: "middle" }}
    >
      <polyline points="23 6 13.5 15.5 8.5 10.5 1 18" />
      <polyline points="17 6 23 6 23 12" />
    </svg>
  );
}

function CheckIcon() {
  return (
    <svg
      width="13"
      height="13"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2.5"
      strokeLinecap="round"
      strokeLinejoin="round"
      style={{ verticalAlign: "middle" }}
    >
      <polyline points="20 6 9 17 4 12" />
    </svg>
  );
}

function AdminDashboardPage() {
  const [activeTab, setActiveTab] = useState("overview");

  const metricCards = [
    {
      label: "Open Requests (Demo)",
      value: "48",
      change: "+18.7% vs last month (Demo)",
      trend: "up",
    },
    {
      label: "Verified Providers (Demo)",
      value: "126",
      change: "+12.4% vs last month (Demo)",
      trend: "up",
    },
    {
      label: "Pending Approvals (Demo)",
      value: "7",
      change: "3 urgent priority (Demo)",
      trend: "attention",
    },
  ];

  // Generate SVG path for volume trend
  const pathD = volumeTrendData.reduce((acc, pt, index) => {
    return index === 0 ? `M ${pt.x} ${pt.y}` : `${acc} L ${pt.x} ${pt.y}`;
  }, "");
  const areaD = `${pathD} L 370 190 L 20 190 Z`;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: spacing.xl }}>
      {/* Header */}
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "flex-start",
          flexWrap: "wrap",
          gap: spacing.md,
        }}
      >
        <div>
          <span
            style={{
              fontSize: "12px",
              fontWeight: 700,
              letterSpacing: "0.08em",
              color: colors.primary,
              textTransform: "uppercase",
            }}
          >
            Operations Workspace
          </span>
          <h1
            style={{
              ...typography.pageTitle,
              margin: `${spacing.xs} 0 0 0`,
              color: colors.textPrimary,
            }}
          >
            Dashboard
          </h1>
          <p
            style={{
              ...typography.body,
              color: colors.textSecondary,
              marginTop: spacing.xs,
            }}
          >
            Platform operations workspace, architectural demonstration metrics, and team
            component boundaries.
          </p>
        </div>

        <div style={{ display: "flex", gap: spacing.sm }}>
          <AppButton
            variant={activeTab === "overview" ? "primary" : "secondary"}
            onClick={() => setActiveTab("overview")}
          >
            Overview
          </AppButton>
          <AppButton
            variant={activeTab === "components" ? "primary" : "secondary"}
            onClick={() => setActiveTab("components")}
          >
            Component Boundaries
          </AppButton>
        </div>
      </div>

      {/* Demonstration Banner */}
      <div
        style={{
          backgroundColor: colors.surfaceHover || "#f8fafc",
          border: `1px solid ${colors.border}`,
          borderRadius: "6px",
          padding: `${spacing.sm} ${spacing.md}`,
          fontSize: "13px",
          color: colors.textSecondary,
          display: "flex",
          alignItems: "center",
          gap: spacing.sm,
        }}
      >
        <span style={{ fontWeight: 600, color: colors.primary }}>
          Demonstration Mode:
        </span>
        <span>
          The metrics, volume trends, and request pipeline below represent static demonstration data. Platform telemetry and provider metrics will connect to production endpoints as future components are integrated.
        </span>
      </div>

      {/* Metrics Row */}
      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))",
          gap: spacing.lg,
        }}
      >
        {metricCards.map((card) => (
          <AppCard key={card.label}>
            <div>
              <span
                style={{
                  fontSize: "13px",
                  color: colors.textSecondary,
                  fontWeight: 500,
                }}
              >
                {card.label}
              </span>
              <div
                style={{
                  fontSize: "32px",
                  fontWeight: 700,
                  color: colors.textPrimary,
                  margin: `${spacing.xs} 0`,
                }}
              >
                {card.value}
              </div>
              <div
                style={{
                  fontSize: "12px",
                  color:
                    card.trend === "attention"
                      ? colors.warning || "#d97706"
                      : colors.success || "#16a34a",
                  display: "flex",
                  alignItems: "center",
                  gap: "4px",
                }}
              >
                <TrendIcon />
                {card.change}
              </div>
            </div>
          </AppCard>
        ))}
      </div>

      {activeTab === "overview" ? (
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(340px, 1fr))",
            gap: spacing.lg,
          }}
        >
          {/* Volume Trend Chart */}
          <AppCard>
            <div style={{ marginBottom: spacing.md }}>
              <h2
                style={{
                  ...typography.sectionHeading,
                  margin: 0,
                  color: colors.textPrimary,
                }}
              >
                Service Request Volume (Demonstration)
              </h2>
              <span
                style={{ fontSize: "12px", color: colors.textSecondary }}
              >
                Illustrative 8-month activity trend projection
              </span>
            </div>

            <div style={{ width: "100%", overflowX: "auto" }}>
              <svg
                viewBox="0 0 400 220"
                style={{ width: "100%", height: "220px", display: "block" }}
              >
                <defs>
                  <linearGradient id="areaGradient" x1="0" y1="0" x2="0" y2="1">
                    <stop
                      offset="0%"
                      stopColor={colors.primary}
                      stopOpacity="0.35"
                    />
                    <stop
                      offset="100%"
                      stopColor={colors.primary}
                      stopOpacity="0.02"
                    />
                  </linearGradient>
                </defs>

                {/* Horizontal Guide Lines */}
                <line
                  x1="15"
                  y1="40"
                  x2="385"
                  y2="40"
                  stroke={colors.border}
                  strokeDasharray="4 4"
                />
                <line
                  x1="15"
                  y1="90"
                  x2="385"
                  y2="90"
                  stroke={colors.border}
                  strokeDasharray="4 4"
                />
                <line
                  x1="15"
                  y1="140"
                  x2="385"
                  y2="140"
                  stroke={colors.border}
                  strokeDasharray="4 4"
                />
                <line
                  x1="15"
                  y1="190"
                  x2="385"
                  y2="190"
                  stroke={colors.border}
                />

                {/* Area and Line */}
                <path d={areaD} fill="url(#areaGradient)" />
                <path
                  d={pathD}
                  fill="none"
                  stroke={colors.primary}
                  strokeWidth="3"
                  strokeLinecap="round"
                />

                {/* Points and Labels */}
                {volumeTrendData.map((pt) => (
                  <g key={pt.month}>
                    <circle
                      cx={pt.x}
                      cy={pt.y}
                      r="4"
                      fill={colors.surface}
                      stroke={colors.primary}
                      strokeWidth="2.5"
                    />
                    <text
                      x={pt.x}
                      y="210"
                      textAnchor="middle"
                      fill={colors.textSecondary}
                      fontSize="11"
                      fontFamily="inherit"
                    >
                      {pt.month}
                    </text>
                  </g>
                ))}
              </svg>
            </div>
          </AppCard>

          {/* Active Service Requests */}
          <AppCard>
            <div style={{ marginBottom: spacing.md }}>
              <h2
                style={{
                  ...typography.sectionHeading,
                  margin: 0,
                  color: colors.textPrimary,
                }}
              >
                Sample Service Requests Pipeline
              </h2>
              <span
                style={{ fontSize: "12px", color: colors.textSecondary }}
              >
                Demonstration coordination workflow pipeline
              </span>
            </div>

            <div
              style={{
                display: "flex",
                flexDirection: "column",
                gap: spacing.sm,
              }}
            >
              {initialActiveRequests.map((req) => (
                <div
                  key={req.id}
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    padding: `${spacing.sm} 0`,
                    borderBottom: `1px solid ${colors.border}`,
                  }}
                >
                  <div>
                    <div
                      style={{
                        fontWeight: 600,
                        fontSize: "14px",
                        color: colors.textPrimary,
                      }}
                    >
                      {req.id} · {req.title}
                    </div>
                    <div
                      style={{
                        fontSize: "12px",
                        color: colors.textSecondary,
                        marginTop: "2px",
                      }}
                    >
                      {req.category} · {req.time}
                    </div>
                  </div>
                  <span
                    style={{
                      fontSize: "12px",
                      fontWeight: 600,
                      padding: "4px 8px",
                      borderRadius: "4px",
                      backgroundColor: colors.surfaceHover || "#f1f5f9",
                      color: colors.primary,
                      whiteSpace: "nowrap",
                    }}
                  >
                    {req.status}
                  </span>
                </div>
              ))}
            </div>
          </AppCard>
        </div>
      ) : (
        /* Team Component Boundaries */
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))",
            gap: spacing.lg,
          }}
        >
          {componentBoundaries.map((comp) => (
            <AppCard key={comp.name}>
              <h3
                style={{
                  margin: `0 0 ${spacing.xs} 0`,
                  fontSize: "16px",
                  color: colors.textPrimary,
                }}
              >
                {comp.name}
              </h3>
              <p
                style={{
                  ...typography.body,
                  fontSize: "13px",
                  color: colors.textSecondary,
                  minHeight: "36px",
                }}
              >
                {comp.scope}
              </p>
              <div
                style={{
                  display: "flex",
                  justifyContent: "space-between",
                  alignItems: "center",
                  marginTop: spacing.md,
                  paddingTop: spacing.sm,
                  borderTop: `1px solid ${colors.border}`,
                  fontSize: "12px",
                }}
              >
                <span style={{ fontWeight: 600, color: colors.textPrimary }}>
                  {comp.owner}
                </span>
                <span
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: "4px",
                    color: colors.success || "#16a34a",
                    fontWeight: 500,
                  }}
                >
                  <CheckIcon />
                  {comp.status}
                </span>
              </div>
            </AppCard>
          ))}
        </div>
      )}
    </div>
  );
}

export default AdminDashboardPage;
