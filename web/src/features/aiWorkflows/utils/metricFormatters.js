export function isValidMetricNumber(value) {
  return typeof value === "number" && Number.isFinite(value) && value >= 0;
}

export function formatDuration(value) {
  if (!isValidMetricNumber(value)) return "—";
  return value < 1000 ? `${Math.round(value)} ms` : `${(value / 1000).toFixed(1)} s`;
}

export function formatRecordedAt(value) {
  if (!value) return "—";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "—" : date.toLocaleString();
}

export function summarizeMetrics(metrics) {
  const durations = metrics.map((metric) => metric.executionTimeMs).filter(isValidMetricNumber);
  return {
    total: metrics.length,
    completed: metrics.filter((metric) => metric.status === "Completed").length,
    failed: metrics.filter((metric) => metric.status === "Failed").length,
    averageDuration: durations.length ? durations.reduce((sum, value) => sum + value, 0) / durations.length : null,
    toolCalls: metrics.reduce((sum, metric) => sum + (isValidMetricNumber(metric.toolCalls) ? metric.toolCalls : 0), 0),
  };
}
