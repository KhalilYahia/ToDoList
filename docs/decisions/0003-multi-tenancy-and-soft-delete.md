# ADR 0003: Multi-tenancy and soft deletion

- Status: Accepted
- Date: 2026-07-22

## Decision

Every tenant-owned operational table stores `organization_id`, including aggregate children. `OpsManagerDbContext` applies organization and soft-delete global filters. The tenant value comes from a scoped `ITenantContext`; the unauthenticated default has no organization and sees no tenant rows. Explicit platform/development infrastructure can opt into bypass.

Organizations are filtered by their own ID. Global users, refresh tokens, plans, platform users, and platform audit logs are not tenant-filtered. Service authorization checks remain mandatory for same-tenant ownership and role/visibility rules.

Historical operational roots use `deleted_at`. Foreign keys default to restrictive deletion, and repository removal rejects entities that do not implement `ISoftDeletable`.

## Consequences

- Accidental unscoped tenant queries return no tenant data.
- Child tables support direct tenant indexes and filtering without joins.
- Cross-tenant access must still be treated as 404 by Service/API in Prompt 02.
- Completed tasks and historical records remain recoverable and auditable.
