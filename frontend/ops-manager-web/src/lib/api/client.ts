import { publicEnvironment } from "@/lib/env";
import { type AuthRealm, sessionStore } from "@/lib/auth/session-store";

import { parseProblemDetails } from "./errors";
import type { PlatformAuthentication, TenantAuthentication } from "./types";

export type ApiRequestOptions = Omit<RequestInit, "body"> & {
  body?: unknown;
  realm?: AuthRealm;
  skipAuth?: boolean;
  skipRefresh?: boolean;
};

let refreshPromises: Partial<
  Record<AuthRealm, Promise<TenantAuthentication | PlatformAuthentication>>
> = {};

function pathForRefresh(realm: AuthRealm): string {
  return realm === "platform" ? "/platform/auth/refresh" : "/auth/refresh";
}

async function refreshSession(
  realm: AuthRealm,
): Promise<TenantAuthentication | PlatformAuthentication> {
  if (refreshPromises[realm]) return refreshPromises[realm]!;

  const promise = fetch(
    `${publicEnvironment.NEXT_PUBLIC_API_BASE_URL}${pathForRefresh(realm)}`,
    {
      method: "POST",
      credentials: "include",
      headers: { Accept: "application/json" },
    },
  )
    .then(async (response) => {
      if (!response.ok) throw await parseProblemDetails(response);
      const authentication = (await response.json()) as
        TenantAuthentication | PlatformAuthentication;
      if (realm === "platform") {
        sessionStore.setPlatform(authentication as PlatformAuthentication);
      } else {
        sessionStore.setTenant(authentication as TenantAuthentication);
      }
      return authentication;
    })
    .finally(() => {
      delete refreshPromises[realm];
    });

  refreshPromises[realm] = promise;
  return promise;
}

export function restoreSession(
  realm: AuthRealm,
): Promise<TenantAuthentication | PlatformAuthentication> {
  return refreshSession(realm);
}

export async function apiRequest<T>(
  path: string,
  options: ApiRequestOptions = {},
): Promise<T> {
  const {
    realm = "tenant",
    skipAuth = false,
    skipRefresh = false,
    body,
    headers,
    ...requestInit
  } = options;

  const requestHeaders = new Headers(headers);
  requestHeaders.set("Accept", "application/json");
  const isFormData = body instanceof FormData;
  if (body !== undefined && !isFormData) {
    requestHeaders.set("Content-Type", "application/json");
  }

  const token = !skipAuth ? sessionStore.tokenFor(realm) : null;
  if (token) requestHeaders.set("Authorization", `Bearer ${token}`);

  const response = await fetch(
    `${publicEnvironment.NEXT_PUBLIC_API_BASE_URL}${path}`,
    {
      ...requestInit,
      headers: requestHeaders,
      credentials: "include",
      body:
        body === undefined
          ? undefined
          : isFormData
            ? body
            : JSON.stringify(body),
    },
  );

  if (
    response.status === 401 &&
    !skipAuth &&
    !skipRefresh &&
    !path.endsWith("/refresh")
  ) {
    try {
      await refreshSession(realm);
    } catch (error) {
      sessionStore.clear();
      throw error;
    }
    return apiRequest<T>(path, { ...options, skipRefresh: true });
  }

  if (!response.ok) throw await parseProblemDetails(response);
  if (response.status === 204) return undefined as T;

  const contentType = response.headers.get("content-type") ?? "";
  return contentType.includes("json")
    ? ((await response.json()) as T)
    : ((await response.text()) as T);
}

export function resetRefreshStateForTests(): void {
  refreshPromises = {};
}
