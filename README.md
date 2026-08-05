# OpsManager

OpsManager is a multi-tenant operations SaaS for restaurants, workshops, and small businesses. The backend is a .NET 10 Web API with ASP.NET Core, EF Core, PostgreSQL, JWT authentication, rotating refresh cookies, role and tenant authorization, recurring operational tasks, inter-department orders, complaints, subscriptions, notifications, and reports.

Organization branch provisioning is a platform-administrator responsibility.
Tenant managers can read branches for operational configuration, but only the
platform API can add, update, or delete them.

## Structure

```text
src/
  OpsManager.Domain/       Entities, enums, invariants, repository contracts
  OpsManager.Repository/   EF Core/PostgreSQL, migrations, repositories, seed
  OpsManager.Service/      DTOs, validation, authorization-aware workflows, reports
  OpsManager.Api/          Controllers, JWT, Problem Details, OpenAPI, hosted jobs
tests/
  OpsManager.Domain.Tests/
  OpsManager.Service.Tests/
  OpsManager.Repository.IntegrationTests/
  OpsManager.Api.IntegrationTests/
```

Dependencies point inward. Domain has no EF or ASP.NET dependency. Service and API never inject `OpsManagerDbContext`; persistence and reporting flow through `IUnitOfWork` and materializing generic-repository operations. No `IQueryable` crosses the Repository boundary.

## Quick start

Prerequisites: .NET 10 SDK and PostgreSQL. Docker is optional and enables disposable PostgreSQL integration tests.

```powershell
dotnet tool restore
dotnet restore OpsManager.sln
dotnet ef database update --project src/OpsManager.Repository --startup-project src/OpsManager.Repository
dotnet build OpsManager.sln --no-restore
dotnet test OpsManager.sln --no-build
dotnet run --project src/OpsManager.Api
```

Development endpoints:

- API root: `GET /api/v1`
- OpenAPI: `GET /openapi/v1.json`
- Liveness/readiness: `GET /health/live`, `GET /health/ready`
- Organization onboarding: `POST /api/v1/auth/register-organization`
- Platform login: `POST /api/v1/platform/auth/login`

Access tokens are returned in authentication responses and kept only in frontend memory. Rotating refresh tokens are hashed in PostgreSQL and sent only as persistent HttpOnly cookies; the frontend uses them to restore login after reload. Local HTTP development overrides the cookie's `Secure` flag; production requires HTTPS and a signing key from a secret store. See [frontend authentication](docs/frontend-authentication.md).

## Development seed

Set `Seed__Enabled=true` and provide `Seed__Password` with at least 12 characters. The idempotent seed creates a platform administrator, a plan with all MVP features, a trial organization, Manager/Supervisor/Employee users, departments, memberships, and starter task/order templates.

Seeded emails use the configured password:

- `platform.admin@opsmanager.local`
- `manager@opsmanager.local`
- `supervisor@opsmanager.local`
- `employee@opsmanager.local`

See [setup](docs/setup.md), [architecture](docs/architecture.md), [API conventions](docs/api-conventions.md), [endpoint catalog](docs/api-endpoints.md), [authorization](docs/authorization-matrix.md), and [testing](docs/testing.md).

The exact next implementation stage is `prompts/03-frontend-project.md`.
