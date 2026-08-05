# Prompt 02 gap report

- Date: 2026-07-24
- Baseline: Prompt 01 / `20260722202008_InitialCreate`

## Baseline status

- The solution targets .NET 10 and builds with no warnings.
- Domain, Repository, Service, and API project references preserve the required inward dependency direction.
- `IGenericRepository<TEntity>` provides materialized, cancellation-aware CRUD, count, existence, and paginated reads. `IUnitOfWork` provides one logical save and an EF-neutral transaction abstraction.
- API and Service contain no `DbContext` or `IQueryable` references.
- The initial migration is applied to the configured local PostgreSQL database.
- PostgreSQL Testcontainers tests are discoverable but Docker is not installed, so they report explicit skips. Two provider/model tests run without Docker.

## Foundational defect fixed before Prompt 02 work

The API test factory added its connection-string override at a lower precedence than application settings. The unavailable-readiness test therefore used the real local database once PostgreSQL became available. The factory now sets the host setting explicitly so tests remain isolated from developer configuration, and database retry is configurable so an intentionally unavailable test target fails fast.

## Prompt 02 gaps

Prompt 01 intentionally has no authentication, authorization policies, feature DTOs, validators, business services, feature controllers, scheduled workers, notifications API, audit orchestration, or reports. The default tenant context is also unauthenticated and therefore returns no tenant rows. Repository projection support is not yet sufficient for report queries.

The following implementation batches close those gaps:

1. Cross-cutting Service/API contracts, validation and Problem Details, JWT/cookies, verified claim contexts, policies, authentication, onboarding, and organization administration.
2. Task-template snapshots, task execution and approval, schedule CRUD, recurrence calculation, and bounded hosted generation.
3. Department-order templates and workflow, quantity/state guards, complaint visibility and internal-message filtering.
4. Platform authentication, subscription/payment transitions and access enforcement, notifications/auditing, and projected reports.
5. Schema migration, integration/unit tests, OpenAPI/API examples, architecture/workflow/report documentation, and boundary checks.

The existing user-edited database connection remains untouched. Authentication and refresh-token schema changes will be delivered through a new migration rather than modifying `InitialCreate`.
