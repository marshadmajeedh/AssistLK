import { describe, it, expect } from "vitest";
import getApiErrorMessage from "../getApiErrorMessage";

describe("getApiErrorMessage", () => {
  it("returns fallbackMessage when error is null or undefined", () => {
    expect(getApiErrorMessage(null)).toBe("An unexpected error occurred. Please try again.");
    expect(getApiErrorMessage(undefined, "Custom fallback")).toBe("Custom fallback");
  });

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

  it("handles 401 Unauthorized", () => {
    const error = { response: { status: 401 } };
    expect(getApiErrorMessage(error)).toBe("Your session has expired. Please log in again.");
  });

  it("handles 403 Forbidden", () => {
    const error = { response: { status: 403 } };
    expect(getApiErrorMessage(error)).toBe("You do not have permission to perform this action.");
  });

  it("handles 404 Not Found", () => {
    const error = { response: { status: 404 } };
    expect(getApiErrorMessage(error)).toBe("The requested service request was not found.");
  });

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

  it("returns data.message, data.title, or data string when present", () => {
    const msgError = { response: { status: 500, data: { message: "Internal service error" } } };
    expect(getApiErrorMessage(msgError)).toBe("Internal service error");

    const titleError = { response: { status: 500, data: { title: "Bad Gateway" } } };
    expect(getApiErrorMessage(titleError)).toBe("Bad Gateway");

    const stringError = { response: { status: 500, data: "Plain text error description" } };
    expect(getApiErrorMessage(stringError)).toBe("Plain text error description");
  });

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
