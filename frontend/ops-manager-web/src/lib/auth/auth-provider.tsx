"use client";

import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useSyncExternalStore,
  type ReactNode,
} from "react";

import { apiRequest, restoreSession } from "@/lib/api/client";
import type {
  PlatformAuthentication,
  TenantAuthentication,
  TenantSession,
} from "@/lib/api/types";

import { type AuthRealm, type Identity, sessionStore } from "./session-store";

type AuthContextValue = {
  identity: Identity | null;
  loginTenant: (values: {
    organizationId?: string;
    email: string;
    password: string;
  }) => Promise<TenantAuthentication>;
  registerTenant: (
    values: Record<string, unknown>,
  ) => Promise<TenantAuthentication>;
  loginPlatform: (values: {
    email: string;
    password: string;
  }) => Promise<PlatformAuthentication>;
  bootstrap: (realm: AuthRealm) => Promise<Identity | null>;
  refresh: () => Promise<void>;
  logout: () => Promise<void>;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const identity = useSyncExternalStore(
    sessionStore.subscribe,
    sessionStore.getSnapshot,
    sessionStore.getServerSnapshot,
  );

  const loginTenant = useCallback(
    async (values: {
      organizationId?: string;
      email: string;
      password: string;
    }) => {
      sessionStore.clear();
      const body: Record<string, unknown> = {
        email: values.email,
        password: values.password,
      };
      if (values.organizationId?.trim()) {
        body.organizationId = values.organizationId.trim();
      }
      const authentication = await apiRequest<TenantAuthentication>(
        "/auth/login",
        {
          method: "POST",
          body,
          skipAuth: true,
          skipRefresh: true,
        },
      );
      sessionStore.setTenant(authentication);
      return authentication;
    },
    [],
  );

  const registerTenant = useCallback(
    async (values: Record<string, unknown>) => {
      sessionStore.clear();
      const authentication = await apiRequest<TenantAuthentication>(
        "/auth/register-organization",
        {
          method: "POST",
          body: values,
          skipAuth: true,
          skipRefresh: true,
        },
      );
      sessionStore.setTenant(authentication);
      return authentication;
    },
    [],
  );

  const loginPlatform = useCallback(
    async (values: { email: string; password: string }) => {
      sessionStore.clear();
      const authentication = await apiRequest<PlatformAuthentication>(
        "/platform/auth/login",
        {
          method: "POST",
          body: values,
          realm: "platform",
          skipAuth: true,
          skipRefresh: true,
        },
      );
      sessionStore.setPlatform(authentication);
      return authentication;
    },
    [],
  );

  const bootstrap = useCallback(async (realm: AuthRealm) => {
    try {
      if (sessionStore.getSnapshot()?.realm !== realm) {
        await restoreSession(realm);
      }
      if (realm === "tenant") {
        const me = await apiRequest<TenantSession>("/auth/me", { realm });
        sessionStore.hydrateTenant(me);
      } else {
        await apiRequest("/platform/auth/me", { realm });
      }
      return sessionStore.getSnapshot();
    } catch {
      sessionStore.clear();
      return null;
    }
  }, []);

  const refresh = useCallback(async () => {
    const realm = sessionStore.getSnapshot()?.realm ?? "tenant";
    if (realm === "tenant") {
      const me = await apiRequest<TenantSession>("/auth/me", { realm });
      sessionStore.hydrateTenant(me);
    }
  }, []);

  const logout = useCallback(async () => {
    const realm = sessionStore.getSnapshot()?.realm ?? "tenant";
    const path =
      realm === "platform" ? "/platform/auth/logout" : "/auth/logout";
    try {
      await apiRequest(path, { method: "POST", realm });
    } finally {
      sessionStore.clear();
    }
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      identity,
      loginTenant,
      registerTenant,
      loginPlatform,
      bootstrap,
      refresh,
      logout,
    }),
    [identity, loginTenant, registerTenant, loginPlatform, bootstrap, refresh, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) throw new Error("useAuth must be used within AuthProvider.");
  return context;
}
