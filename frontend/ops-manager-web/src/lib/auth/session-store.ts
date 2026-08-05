import type {
  PlatformAuthentication,
  PlatformUser,
  SubscriptionAccess,
  TenantAuthentication,
  TenantSession,
} from "@/lib/api/types";

export type AuthRealm = "tenant" | "platform";

export type TenantIdentity = {
  realm: "tenant";
  token: string;
  expiresAt: string;
  session: TenantSession;
};

export type PlatformIdentity = {
  realm: "platform";
  token: string;
  expiresAt: string;
  user: PlatformUser;
};

export type Identity = TenantIdentity | PlatformIdentity;

let identity: Identity | null = null;
const listeners = new Set<() => void>();

function emit(): void {
  listeners.forEach((listener) => listener());
}

export const sessionStore = {
  getSnapshot: (): Identity | null => identity,
  getServerSnapshot: (): Identity | null => null,
  subscribe(listener: () => void): () => void {
    listeners.add(listener);
    return () => listeners.delete(listener);
  },
  clear(): void {
    identity = null;
    emit();
  },
  setTenant(authentication: TenantAuthentication): void {
    identity = {
      realm: "tenant",
      token: authentication.accessToken,
      expiresAt: authentication.accessTokenExpiresAt,
      session: {
        user: authentication.user,
        organization: authentication.organization,
        membership: authentication.membership,
        departmentIds: [],
        access: authentication.access,
      },
    };
    emit();
  },
  hydrateTenant(session: TenantSession): void {
    if (identity?.realm !== "tenant") return;
    identity = { ...identity, session };
    emit();
  },
  setPlatform(authentication: PlatformAuthentication): void {
    identity = {
      realm: "platform",
      token: authentication.accessToken,
      expiresAt: authentication.accessTokenExpiresAt,
      user: authentication.user,
    };
    emit();
  },
  tokenFor(realm: AuthRealm): string | null {
    return identity?.realm === realm ? identity.token : null;
  },
  access(): SubscriptionAccess | null {
    return identity?.realm === "tenant" ? identity.session.access : null;
  },
};
