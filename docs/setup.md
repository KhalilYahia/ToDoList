# Setup

## Prerequisites

- .NET SDK 10.0.301 or a compatible .NET 10 patch.
- PostgreSQL 16 or newer; the integration tests use `postgres:17-alpine` when Docker is available.
- Docker only if running Testcontainers-based tests.

Docker and local PostgreSQL tools were not available when Prompt 01 was implemented, so no environment-specific Compose file was generated.

## Configure PostgreSQL

Copy `.env.example` values into your shell, IDE secret store, or an uncommitted local settings file. Do not commit real credentials.

Example Docker command on a machine with Docker:

```powershell
docker run --name opsmanager-postgres -e POSTGRES_DB=opsmanager -e POSTGRES_USER=opsmanager -e POSTGRES_PASSWORD=change-me -p 5432:5432 -v opsmanager-postgres-data:/var/lib/postgresql/data -d postgres:17-alpine
```

Set the connection string:

```powershell
$env:ConnectionStrings__OpsManager='Host=localhost;Port=5432;Database=opsmanager;Username=opsmanager;Password=change-me'
```

## Restore, migrate, and run

```powershell
dotnet tool restore
dotnet restore OpsManager.sln
dotnet tool run dotnet-ef database update --project src/OpsManager.Repository --startup-project src/OpsManager.Repository
dotnet run --project src/OpsManager.Api
```

The design-time factory also accepts `OPSMANAGER_DB_CONNECTION` for EF CLI commands.

## Development seed

Seeding is disabled by default and never runs automatically in Production or Testing.

```powershell
$env:Seed__Enabled='true'
$env:Seed__Password='replace-with-a-local-strong-password'
dotnet run --project src/OpsManager.Api
```

The API hashes the configured password with ASP.NET Core's password hasher, passes only the hash to Repository seed infrastructure, applies migrations, and inserts deterministic records that can be seeded repeatedly without duplication. The seed password is not printed.

## OpenAPI

Run in Development and fetch `http://localhost:<port>/openapi/v1.json`. The generated document describes the bootstrap endpoints now and will include feature endpoints added in Prompt 02.
