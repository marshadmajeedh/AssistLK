import AppCard from "../../../shared/components/AppCard";
import StatusBadge from "../../../shared/components/StatusBadge";
import { badgeStyles, colors } from "../../../shared/theme";

import { formatDate, formatConfidence } from "../utils/monitoringFormatters";
import RequestLocation from "./RequestLocation";

export function UrgencyBadge({ urgency }) {
  const tone = urgency === "Critical" ? [colors.error, colors.errorLight]
    : urgency === "High" ? [colors.warning, colors.warningLight]
      : [colors.textPrimary, colors.neutralLight];
  return <span style={{ ...badgeStyles.base, color: tone[0], backgroundColor: tone[1] }}>{urgency || "Unknown"}</span>;
}

function Field({ label, children }) {
  return <div><dt>{label}</dt><dd>{children}</dd></div>;
}

export default function RequestMonitoringDetails({ request }) {
  const analysis = request.latestAnalysis;
  return <div className="monitoring-sections">
    <AppCard>
      <h3>Request overview</h3>
      <dl className="monitoring-fields">
        <Field label="Full Request ID">{request.serviceRequestId}</Field>
        <Field label="Status"><StatusBadge status={request.status} /></Field>
        <Field label="Created At">{formatDate(request.createdAt)}</Field>
        {request.updatedAt && <Field label="Updated At">{formatDate(request.updatedAt)}</Field>}
        <Field label="Urgency"><UrgencyBadge urgency={request.urgency} /></Field>
        <Field label="Category">{request.category || "Unclassified"}</Field>
        {request.categoryHint && <Field label="Customer category preference">{request.categoryHint}</Field>}
      </dl>
    </AppCard>
    <AppCard><h3>Problem description</h3><p className="monitoring-prose">{request.description}</p></AppCard>
    <AppCard>
      <h3>Location</h3>
      <RequestLocation locationText={request.locationText} locationSource={request.locationSource} />
      {(request.latitude != null || request.longitude != null) && <div className="monitoring-coordinates">
        <h4>GPS coordinates</h4>
        <dl className="monitoring-fields">
        {request.latitude != null && <Field label="Latitude">{request.latitude}</Field>}
        {request.longitude != null && <Field label="Longitude">{request.longitude}</Field>}
        </dl>
      </div>}
    </AppCard>
    <AppCard>
      <h3>Latest AssistLK AI analysis</h3>
      {analysis ? <>
        <dl className="monitoring-fields">
          <Field label="Final category">{request.category || "Unclassified"}</Field>
          <Field label="Urgency"><UrgencyBadge urgency={request.urgency} /></Field>
          <Field label="Analysis confidence">{formatConfidence(analysis)}</Field>
          {analysis.agentName && <Field label="Agent">AssistLK AI</Field>}
          {analysis.createdAt && <Field label="Analysis timestamp">{formatDate(analysis.createdAt)}</Field>}
        </dl>
        <h4>Problem summary</h4><p className="monitoring-prose">{analysis.detectedProblem || "Not available"}</p>
      </> : <p>No analysis available yet.</p>}
    </AppCard>
    <AppCard>
      <h3>Clarification history</h3>
      {request.clarifications?.length ? <ol className="monitoring-history">
        {request.clarifications.map((item) => <li key={item.id}>
          <h4>Round {item.clarificationRound} · Sequence {item.sequence}</h4>
          <dl>
            <Field label="Question"><span className="monitoring-prose">{item.question}</span></Field>
            <Field label="Answer"><span className="monitoring-prose">{item.answer?.trim() ? item.answer : "Unanswered"}</span></Field>
            {item.answeredAt && <Field label="Answered At">{formatDate(item.answeredAt)}</Field>}
            {item.supersededAt && <Field label="Superseded At">{formatDate(item.supersededAt)}</Field>}
          </dl>
        </li>)}
      </ol> : <p>No clarification history.</p>}
    </AppCard>
    <p className="monitoring-muted">Read-only Admin monitoring view. Customer request changes are managed through the customer application.</p>
  </div>;
}
