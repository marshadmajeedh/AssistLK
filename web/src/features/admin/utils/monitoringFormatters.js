export function formatDate(value) {
  if (!value) return "—";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "—" : date.toLocaleString();
}

export function formatConfidence(analysis) {
  const value = analysis?.confidence;
  return typeof value === "number" && Number.isFinite(value)
    ? `${Math.round(value * 100)}%` : "—";
}
