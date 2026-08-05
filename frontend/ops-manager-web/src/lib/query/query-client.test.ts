import { ApiError } from "@/lib/api/errors";

import { shouldRetryQuery } from "./query-client";
import { queryKeys } from "./query-keys";

describe("query infrastructure", () => {
  it("does not retry validation or authorization failures", () => {
    expect(shouldRetryQuery(0, new ApiError({ status: 403 }))).toBe(false);
    expect(shouldRetryQuery(0, new ApiError({ status: 422 }))).toBe(false);
  });

  it("limits retries for transient failures", () => {
    expect(shouldRetryQuery(0, new ApiError({ status: 503 }))).toBe(true);
    expect(shouldRetryQuery(2, new TypeError("offline"))).toBe(false);
  });

  it("keeps list and detail keys distinct", () => {
    expect(queryKeys.tasks.lists({ page: 1 })).not.toEqual(
      queryKeys.tasks.detail("task-1"),
    );
  });

  it("scopes platform branch keys to their organization", () => {
    expect(
      queryKeys.platform.organizationBranches("organization-1"),
    ).not.toEqual(queryKeys.platform.organizationBranches("organization-2"));
    expect(
      queryKeys.platform.organizationBranch("organization-1", "branch-1"),
    ).not.toEqual(
      queryKeys.platform.organizationBranch("organization-1", "branch-2"),
    );
  });
});
