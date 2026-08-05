import type { Identity } from "@/lib/auth/session-store";

const organizationRoles = ["Manager", "Supervisor", "Employee"] as const;
const platformRoles = ["Administrator", "Support"] as const;
const subscriptionModes = [
  "Full",
  "GraceLimited",
  "ReadOnly",
  "Blocked",
] as const;

function codeFromValue(
  value: number | string | null | undefined,
  codes: readonly string[],
): string | null {
  if (typeof value === "number") return codes[value] ?? null;
  return value ?? null;
}

export function tenantRole(identity: Identity | null): string | null {
  return identity?.realm === "tenant"
    ? codeFromValue(identity.session.membership.role, organizationRoles)
    : null;
}

export function subscriptionAccessMode(
  identity: Identity | null,
): string | null {
  return identity?.realm === "tenant"
    ? codeFromValue(identity.session.access.mode, subscriptionModes)
    : null;
}

export function isManager(identity: Identity | null): boolean {
  return tenantRole(identity) === "Manager";
}

export function isSupervisorOrManager(identity: Identity | null): boolean {
  return ["Manager", "Supervisor"].includes(tenantRole(identity) ?? "");
}

export function isPlatformAdministrator(identity: Identity | null): boolean {
  return (
    identity?.realm === "platform" &&
    codeFromValue(identity.user.role, platformRoles) === "Administrator"
  );
}

export function isReadOnly(identity: Identity | null): boolean {
  return subscriptionAccessMode(identity) === "ReadOnly";
}

export function canMutateTenant(identity: Identity | null): boolean {
  if (identity?.realm !== "tenant") return false;
  return !["ReadOnly", "Blocked"].includes(
    subscriptionAccessMode(identity) ?? "",
  );
}
