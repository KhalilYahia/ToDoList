# ADR 0001: Initial backend architecture

- Status: Accepted
- Date: 2026-07-22

## Context

OpsManager needs an initial backend foundation for a multi-tenant operations SaaS. The workspace initially contains only staged prompts and repository instructions. It has no Git metadata, existing solution, application source, Docker installation, or local PostgreSQL client/server tools.

The only installed .NET SDK is 10.0.301. .NET 10 is the latest installed stable LTS release, so all new projects target `net10.0`.

## Decision

Use a modular N-tier solution with DDD-inspired domain modeling and the following dependency direction:

```text
OpsManager.Domain <- OpsManager.Repository
OpsManager.Domain <- OpsManager.Service
OpsManager.Service + OpsManager.Repository <- OpsManager.Api
```

The implementation is divided into bounded batches:

1. Scaffold the solution, layer projects, test projects, shared build configuration, and baseline documentation.
2. Add Domain primitives, repository contracts, stable system codes, entities, and entity-level invariants.
3. Add the EF Core PostgreSQL model, tenant and soft-delete filters, generic repository, UnitOfWork, initial migration, and idempotent development seed.
4. Add the thin API composition root, Problem Details handling, configurable CORS, OpenAPI, and live/ready health checks.
5. Add unit and PostgreSQL integration tests, complete documentation, and validate formatting, restore, build, tests, migrations, and project-reference direction.

Domain remains free of EF Core, ASP.NET Core, and persistence concerns. Service and API do not receive or query `OpsManagerDbContext`; persisted data is accessed only through `IUnitOfWork` and `IGenericRepository<TEntity>`. Repository reads return materialized values and never expose `IQueryable`.

Tenant identity is supplied to Repository through a scoped Domain contract. EF global filters provide a default database query boundary, while later Service workflows must add authorization checks and treat cross-tenant resources as not found.

## Environment assumptions

- PostgreSQL behavior is implemented and tested with Npgsql.
- PostgreSQL integration tests use Testcontainers and require a working Docker daemon. Because Docker is not installed in this environment, they must report an explicit skip rather than substitute EF Core InMemory.
- A Docker Compose file is not added in this batch because the prompt makes it conditional on Docker availability. Setup documentation includes a compatible PostgreSQL container example for environments that do provide Docker.
- Development seed credentials are configuration-driven and contain no committed password.
- Existing `prompts/`, `OpsManager_Codex_Prompts/`, `AGENTS.md`, and `README_AR.md` files are preserved.

## Consequences

- The solution uses current .NET 10 APIs and package versions.
- Local database validation cannot be executed in this environment until Docker or PostgreSQL is installed.
- Business workflows, authentication endpoints, resource controllers, reports, background scheduling, and frontend work remain intentionally deferred to later staged prompts.
