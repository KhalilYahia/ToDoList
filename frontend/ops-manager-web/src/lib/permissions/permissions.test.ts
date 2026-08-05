import {
  canMutateTenant,
  isManager,
  isPlatformAdministrator,
} from "./permissions";
import type { Identity } from "@/lib/auth/session-store";

const tenantIdentity = {
  realm: "tenant",
  token: "token",
  expiresAt: "2030-01-01T00:00:00Z",
  session: {
    user: {},
    organization: {},
    membership: { role: 0 },
    departmentIds: [],
    access: { mode: 0 },
  },
} as unknown as Identity;

describe("permission helpers", () => {
  it("recognizes manager access", () => {
    expect(isManager(tenantIdentity)).toBe(true);
    expect(canMutateTenant(tenantIdentity)).toBe(true);
  });

  it("blocks tenant mutations in read-only mode", () => {
    const readOnly = structuredClone(tenantIdentity) as Identity;
    if (readOnly.realm === "tenant") {
      readOnly.session.access.mode = 2;
    }
    expect(canMutateTenant(readOnly)).toBe(false);
  });

  it("keeps platform roles separate", () => {
    expect(isPlatformAdministrator(tenantIdentity)).toBe(false);
    const administrator = {
      realm: "platform",
      token: "token",
      expiresAt: "2030-01-01T00:00:00Z",
      user: { role: 0 },
    } as unknown as Identity;
    const support = structuredClone(administrator) as Identity;
    if (support.realm === "platform") support.user.role = 1;
    expect(isPlatformAdministrator(administrator)).toBe(true);
    expect(isPlatformAdministrator(support)).toBe(false);
  });
});
