export default function RequestLocation({ locationText, locationSource }) {
  const hasAddress = typeof locationText === "string" && locationText.trim().length > 0;
  return <div className="monitoring-location">
    <p className="monitoring-location-address">{hasAddress ? locationText : "—"}</p>
    {hasAddress && locationSource === "OpenStreetMap" && <a
      className="monitoring-location-attribution"
      href="https://www.openstreetmap.org/copyright"
      target="_blank" rel="noopener noreferrer">
      © OpenStreetMap contributors
    </a>}
  </div>;
}
