import type { components } from "./schema";

export type Schemas = components["schemas"];
export type TenantAuthentication = Schemas["AuthenticationResponse"];
export type TenantSession = Schemas["MeResponse"];
export type PlatformAuthentication = Schemas["PlatformAuthenticationResponse"];
export type PlatformUser = Schemas["PlatformUserDto"];
export type SubscriptionAccess = Schemas["SubscriptionAccess"];
export type OrganizationRole = Schemas["OrganizationRole"];
export type PlatformRole = Schemas["PlatformRole"];

export type PagedResponse<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
};

export type ProblemDetails = {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  code?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
};
