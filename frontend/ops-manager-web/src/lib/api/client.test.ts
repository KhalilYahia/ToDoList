import { sessionStore } from "@/lib/auth/session-store";

import { resetRefreshStateForTests, restoreSession } from "./client";
import type { TenantAuthentication } from "./types";

const tenantAuthentication: TenantAuthentication = {
  accessToken: "short-lived-access-token",
  accessTokenExpiresAt: "2030-01-01T00:15:00Z",
  user: {
    id: "11111111-1111-4111-8111-111111111111",
    fullName: "Manager",
    email: "manager@example.test",
    phone: null,
    preferredLanguage: "en",
    accountStatus: 0,
  },
  organization: {
    id: "22222222-2222-4222-8222-222222222222",
    name: "Example",
    legalName: null,
    timezone: "UTC",
    defaultLanguage: "en",
    status: 0,
  },
  membership: {
    id: "33333333-3333-4333-8333-333333333333",
    role: 0,
    isActive: true,
    joinedAt: "2030-01-01T00:00:00Z",
  },
  access: {
    mode: 0,
    status: 1,
    expiresAt: "2030-02-01T00:00:00Z",
    reason: null,
  },
};

describe("cookie-backed session restoration", () => {
  beforeEach(() => {
    sessionStore.clear();
    resetRefreshStateForTests();
    localStorage.clear();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("restores an access token through the HttpOnly refresh cookie", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify(tenantAuthentication), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      }),
    );
    vi.stubGlobal("fetch", fetchMock);

    await restoreSession("tenant");

    expect(fetchMock).toHaveBeenCalledOnce();
    expect(fetchMock.mock.calls[0]?.[0]).toContain("/auth/refresh");
    expect(fetchMock.mock.calls[0]?.[1]).toMatchObject({
      method: "POST",
      credentials: "include",
    });
    expect(sessionStore.tokenFor("tenant")).toBe(
      tenantAuthentication.accessToken,
    );
    expect(localStorage.length).toBe(0);
    expect(document.cookie).not.toContain(tenantAuthentication.accessToken);
  });

  it("deduplicates concurrent refresh attempts", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify(tenantAuthentication), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      }),
    );
    vi.stubGlobal("fetch", fetchMock);

    await Promise.all([restoreSession("tenant"), restoreSession("tenant")]);

    expect(fetchMock).toHaveBeenCalledOnce();
  });
});
