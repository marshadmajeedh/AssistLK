// What this suite tests:
//   The API error parsing utility function (getApiErrorMessage) used across the web application
//   to extract clean, user-friendly error messages from Axios error responses (including 400 validation dictionaries,
//   401 unauthorized, 403 forbidden, 404 not found, 409 conflict, network errors, and 500 server crashes).
// Why these tests are needed:
//   Ensures customers and admins receive actionable, friendly error messages in UI banners/toasts
//   and prevents technical backend stack traces from leaking into the browser.
// What is real and what is mocked/faked:
//   Pure unit tests testing the utility logic against mock Axios error objects. No live API or network calls are made.
// Prerequisites:
//   Node.js dependencies installed in web folder (npm install).
// Run from repository root:
//   npm --prefix web test -- src/features/serviceRequests/utils/__tests__/getApiErrorMessage.test.js

import { describe, it, expect } from "vitest";
import getApiErrorMessage from "../getApiErrorMessage";

describe("getApiErrorMessage", () => {
  // Purpose: Checks that null or undefined error arguments return either the default fallback message or a custom fallback.
  // Why: Prevents null reference exceptions when an error handler receives an empty error payload.
  // Scenario: Passing null or undefined as the error input.
  // Pass condition: Returns "An unexpected error occurred. Please try again." or the provided custom fallback string.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/utils/__tests__/getApiErrorMessage.test.js -t "returns fallbackMessage when error is null or undefined"
  it("returns fallbackMessage when error is null or undefined", () => {
    expect(getApiErrorMessage(null)).toBe("An unexpected error occurred. Please try again.");
    expect(getApiErrorMessage(undefined, "Custom fallback")).toBe("Custom fallback");
  });

  // Purpose: Checks that network failures or missing responses yield a clear network connection guidance message.
  // Why: Helps the user understand their network or server connectivity dropped rather than an application bug.
  // Scenario: Error object with ERR_NETWORK code or empty response with request property.
  // Pass condition: Returns "Unable to connect to the server. Please check your network connection and try again."
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/utils/__tests__/getApiErrorMessage.test.js -t "handles network failure or connection errors"
  it("handles network failure or connection errors", () => {
    const networkError = { code: "ERR_NETWORK" };
    expect(getApiErrorMessage(networkError)).toBe(
      "Unable to connect to the server. Please check your network connection and try again."
    );

    const noResponseError = { request: {} };
    expect(getApiErrorMessage(noResponseError)).toBe(
      "Unable to connect to the server. Please check your network connection and try again."
    );
  });

  // Purpose: Checks that HTTP 401 Unauthorized returns an expired session message directing the user to log in again.
  // Why: Clear feedback that JWT token has expired or is invalid.
  // Scenario: Error response with status 401.
  // Pass condition: Returns "Your session has expired. Please log in again."
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/utils/__tests__/getApiErrorMessage.test.js -t "handles 401 Unauthorized"
  it("handles 401 Unauthorized", () => {
    const error = { response: { status: 401 } };
    expect(getApiErrorMessage(error)).toBe("Your session has expired. Please log in again.");
  });

  // Purpose: Checks that HTTP 403 Forbidden returns a permission denied message.
  // Why: Informs the user when role-based access control restricts the requested operation.
  // Scenario: Error response with status 403.
  // Pass condition: Returns "You do not have permission to perform this action."
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/utils/__tests__/getApiErrorMessage.test.js -t "handles 403 Forbidden"
  it("handles 403 Forbidden", () => {
    const error = { response: { status: 403 } };
    expect(getApiErrorMessage(error)).toBe("You do not have permission to perform this action.");
  });

  // Purpose: Checks that HTTP 404 Not Found returns a resource missing message.
  // Why: Informs user that the requested service request record does not exist or was deleted.
  // Scenario: Error response with status 404.
  // Pass condition: Returns "The requested service request was not found."
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/utils/__tests__/getApiErrorMessage.test.js -t "handles 404 Not Found"
  it("handles 404 Not Found", () => {
    const error = { response: { status: 404 } };
    expect(getApiErrorMessage(error)).toBe("The requested service request was not found.");
  });

  // Purpose: Checks that HTTP 409 Conflict extracts custom backend conflict messages, title strings, or defaults to status guidance.
  // Why: State machine conflicts (e.g. attempting to cancel an already-matched request) need to explain why the state transition was blocked.
  // Scenario: Error response with status 409 and custom message, title, or empty data.
  // Pass condition: Extracts the specific conflict message or returns "This service request cannot be modified in its current status."
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/utils/__tests__/getApiErrorMessage.test.js -t "handles 409 Conflict with custom message or title or string or fallback"
  it("handles 409 Conflict with custom message or title or string or fallback", () => {
    const customConflict = {
      response: {
        status: 409,
        data: { message: "Cannot cancel a request that has already been matched." },
      },
    };
    expect(getApiErrorMessage(customConflict)).toBe(
      "Cannot cancel a request that has already been matched."
    );

    const titleConflict = {
      response: {
        status: 409,
        data: { title: "Conflict in current state" },
      },
    };
    expect(getApiErrorMessage(titleConflict)).toBe("Conflict in current state");

    const fallbackConflict = {
      response: {
        status: 409,
        data: {},
      },
    };
    expect(getApiErrorMessage(fallbackConflict)).toBe(
      "This service request cannot be modified in its current status."
    );
  });

  // Purpose: Checks that ASP.NET Core ValidationProblemDetails dictionary (HTTP 400) errors are flattened into a single readable sentence.
  // Why: Combines multiple validation field errors into a single cohesive message for display.
  // Scenario: 400 response with nested dictionary of property error arrays (Description, LocationText).
  // Pass condition: All validation messages are concatenated with spaces into a clean string.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/utils/__tests__/getApiErrorMessage.test.js -t "handles 400 validation error dictionary (ASP.NET Core ValidationProblemDetails)"
  it("handles 400 validation error dictionary (ASP.NET Core ValidationProblemDetails)", () => {
    const validationError = {
      response: {
        status: 400,
        data: {
          errors: {
            Description: ["Description is required.", "Must be at least 10 characters."],
            LocationText: ["Location cannot exceed 255 characters."],
          },
        },
      },
    };
    expect(getApiErrorMessage(validationError)).toBe(
      "Description is required. Must be at least 10 characters. Location cannot exceed 255 characters."
    );
  });

  // Purpose: Checks that error data properties (data.message, data.title, or raw string data) are prioritized for display.
  // Why: Preserves server-supplied explanations when present in general API failures.
  // Scenario: 500 error responses with message, title, or string body.
  // Pass condition: Returns the respective server error string.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/utils/__tests__/getApiErrorMessage.test.js -t "returns data.message, data.title, or data string when present"
  it("returns data.message, data.title, or data string when present", () => {
    const msgError = { response: { status: 500, data: { message: "Internal service error" } } };
    expect(getApiErrorMessage(msgError)).toBe("Internal service error");

    const titleError = { response: { status: 500, data: { title: "Bad Gateway" } } };
    expect(getApiErrorMessage(titleError)).toBe("Bad Gateway");

    const stringError = { response: { status: 500, data: "Plain text error description" } };
    expect(getApiErrorMessage(stringError)).toBe("Plain text error description");
  });

  // Purpose: Checks that internal server stack traces are never rendered to the user and fall back safely.
  // Why: Prevents security vulnerabilities and poor UX caused by leaking internal code paths and stack traces.
  // Scenario: Server error response containing stackTrace field.
  // Pass condition: Returns safe fallback and does not contain backend class or line names.
  // Run from repository root:
  //   npm --prefix web test -- src/features/serviceRequests/utils/__tests__/getApiErrorMessage.test.js -t "never exposes stack traces and returns safe fallback"
  it("never exposes stack traces and returns safe fallback", () => {
    const serverCrash = {
      response: {
        status: 500,
        data: {
          stackTrace: "at AssistLK.Controllers.ServiceRequestController.Analyze() in line 42",
        },
      },
    };
    expect(getApiErrorMessage(serverCrash, "Server error fallback")).toBe("Server error fallback");
    expect(getApiErrorMessage(serverCrash)).not.toContain("AssistLK.Controllers");
  });
});
