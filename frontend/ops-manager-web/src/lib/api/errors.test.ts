import { ApiError, parseProblemDetails } from "./errors";

describe("Problem Details", () => {
  it("parses stable codes, trace IDs, and validation fields", async () => {
    const error = await parseProblemDetails(
      new Response(
        JSON.stringify({
          status: 400,
          title: "Validation failed",
          code: "validation_failed",
          traceId: "trace-1",
          errors: { email: ["Invalid email"] },
        }),
        {
          status: 400,
          headers: { "content-type": "application/problem+json" },
        },
      ),
    );

    expect(error).toBeInstanceOf(ApiError);
    expect(error.code).toBe("validation_failed");
    expect(error.traceId).toBe("trace-1");
    expect(error.fieldErrors.email).toEqual(["Invalid email"]);
  });

  it("uses a sanitized fallback for a malformed response", async () => {
    const error = await parseProblemDetails(
      new Response("not json", { status: 500, statusText: "Server Error" }),
    );
    expect(error.status).toBe(500);
    expect(error.message).toBe("Server Error");
  });
});
