/**
 * Status-based lifecycle helper for Service Request UI.
 *
 * NOTE: These helpers are for UX presentation only (showing/hiding actions,
 * rendering friendly guard views). Backend validation remains authoritative.
 */

export const EDITABLE_STATUSES = Object.freeze([
  "Created",
  "AwaitingInformation",
]);

export const CANCELLABLE_STATUSES = Object.freeze([
  "Created",
  "Analyzing",
  "AwaitingInformation",
  "Analyzed",
]);

export const ANALYZABLE_STATUSES = Object.freeze([
  "Created",
  "AwaitingInformation",
]);

/**
 * Checks whether a request can be edited in the UI.
 * @param {string} status - ServiceRequestStatus string
 * @returns {boolean}
 */
export function canEditRequest(status) {
  if (!status || typeof status !== "string") {
    return false;
  }
  return EDITABLE_STATUSES.includes(status);
}

/**
 * Checks whether a request can be cancelled in the UI.
 * @param {string} status - ServiceRequestStatus string
 * @returns {boolean}
 */
export function canCancelRequest(status) {
  if (!status || typeof status !== "string") {
    return false;
  }
  return CANCELLABLE_STATUSES.includes(status);
}

/**
 * Checks whether a request can be analyzed in the UI.
 * @param {string} status - ServiceRequestStatus string
 * @returns {boolean}
 */
export function canAnalyzeRequest(status) {
  if (!status || typeof status !== "string") {
    return false;
  }
  return ANALYZABLE_STATUSES.includes(status);
}

export default {
  EDITABLE_STATUSES,
  CANCELLABLE_STATUSES,
  ANALYZABLE_STATUSES,
  canEditRequest,
  canCancelRequest,
  canAnalyzeRequest,
};
