import type { ProblemDetails } from "./types";

export class ApiError extends Error {
  readonly status: number;
  readonly code?: string;
  readonly traceId?: string;
  readonly fieldErrors: Record<string, string[]>;
  readonly problem: ProblemDetails;

  constructor(problem: ProblemDetails, fallbackStatus = 500) {
    super(
      problem.detail ?? problem.title ?? "The request could not be completed.",
    );
    this.name = "ApiError";
    this.status = problem.status ?? fallbackStatus;
    this.code = problem.code;
    this.traceId = problem.traceId;
    this.fieldErrors = problem.errors ?? {};
    this.problem = problem;
  }
}

export async function parseProblemDetails(
  response: Response,
): Promise<ApiError> {
  let problem: ProblemDetails = {
    status: response.status,
    title: response.statusText || "Request failed",
  };

  const contentType = response.headers.get("content-type") ?? "";
  if (contentType.includes("json")) {
    try {
      problem = {
        ...problem,
        ...((await response.json()) as ProblemDetails),
      };
    } catch {
      // Keep the sanitized HTTP fallback when a server returns malformed JSON.
    }
  }

  return new ApiError(problem, response.status);
}

export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.message;
  if (error instanceof TypeError) {
    return "The API is unavailable. Check your connection and try again.";
  }
  return error instanceof Error ? error.message : "Something went wrong.";
}
