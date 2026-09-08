export function getApiErrorMessage(
  error,
  fallbackMessage = "An unexpected error occurred. Please try again."
) {
  if (!error) {
    return fallbackMessage;
  }

  // Network or connection failure
  if (error.code === "ERR_NETWORK" || (!error.response && error.request)) {
    return "Unable to connect to the server. Please check your network connection and try again.";
  }

  const status = error.response?.status;
  const data = error.response?.data;

  // Specific HTTP status code user messages
  if (status === 401) {
    return "Your session has expired. Please log in again.";
  }

  if (status === 403) {
    return "You do not have permission to perform this action.";
  }

  if (status === 404) {
    return "The requested service request was not found.";
  }

  // Check for ASP.NET Core Validation Problem Details errors dictionary
  if (data?.errors && typeof data.errors === "object") {
    const errorMessages = Object.values(data.errors)
      .flat()
      .filter((msg) => typeof msg === "string" && msg.trim().length > 0);

    if (errorMessages.length > 0) {
      return errorMessages.join(" ");
    }
  }

  // Check for custom error message property
  if (typeof data?.message === "string" && data.message.trim().length > 0) {
    return data.message.trim();
  }

  // Check for problem details title
  if (typeof data?.title === "string" && data.title.trim().length > 0) {
    return data.title.trim();
  }

  // Check for plain text string response
  if (typeof data === "string" && data.trim().length > 0) {
    return data.trim();
  }

  return fallbackMessage;
}

export default getApiErrorMessage;
