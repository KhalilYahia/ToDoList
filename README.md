# OpsManager

OpsManager is a multi-tenant operations platform for restaurants, workshops, and small businesses. This repository currently contains the Prompt 01 backend foundation: a .NET 10 Web API, Domain model, EF Core/PostgreSQL persistence, generic repository and UnitOfWork, initial migration, development seed, health checks, and foundational tests.

## Solution structure

```text
OpsManager.sln
src/
  OpsManager.Domain/       Entities, enums, invariants, and repository contracts
  OpsManager.Repository/   EF Core model, PostgreSQL migration, repositories, and seed
  OpsManager.Service/      Service-layer abstractions; workflows arrive in Prompt 02
  OpsManager.Api/          Composition root, OpenAPI, Problem Details, and health checks
tests/
  OpsManager.Domain.Tests/
  OpsManager.Repository.IntegrationTests/
  OpsManager.Service.Tests/
  OpsManager.Api.IntegrationTests/
docs/
  decisions/
```

Dependencies point inward: `Domain <- Repository`, `Domain <- Service`, and `Service + Repository <- API`. API and Service do not query or inject `OpsManagerDbContext`.

## Quick start

Prerequisites are .NET SDK 10.0.301 or a compatible .NET 10 patch, and PostgreSQL. Docker is optional but required for Testcontainers integration tests.

```powershell
dotnet tool restore
dotnet restore OpsManager.sln
dotnet build OpsManager.sln --no-restore
dotnet test OpsManager.sln --no-build
dotnet tool run dotnet-ef database update --project src/OpsManager.Repository --startup-project src/OpsManager.Repository
dotnet run --project src/OpsManager.Api
```

The API exposes `/api/v1`, `/openapi/v1.json` in Development/Testing, `/health/live`, and `/health/ready`.

## Database and seed

The initial migration is `20260722202008_InitialCreate`. Override the development connection with `ConnectionStrings__OpsManager` as shown in `.env.example`.

Development seeding is opt-in. Set `Seed__Enabled=true` and provide a local `Seed__Password` of at least 12 characters. The password is hashed with ASP.NET Core's password hasher and is never logged or committed. The seed creates these login records with the configured password:

- `platform.admin@opsmanager.local`
- `manager@opsmanager.local`
- `supervisor@opsmanager.local`
- `employee@opsmanager.local`

It also creates one plan, organization, primary branch, three departments, memberships, a 14-day trial, a task template, and a department-order template. Deterministic IDs make the seed idempotent; it only runs automatically in Development when explicitly enabled.

## Prompt 01 boundary

Authentication endpoints, authorization policies, feature controllers, full Service workflows, reports, scheduled task generation, and the frontend are intentionally not implemented yet.

See [setup](docs/setup.md), [architecture](docs/architecture.md), [data model](docs/data-model.md), and [testing](docs/testing.md). The exact next step is to execute `prompts/02-backend-logic-and-apis.md`.
