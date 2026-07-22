# Testing

Run the complete suite:

```powershell
dotnet restore OpsManager.sln
dotnet build OpsManager.sln --no-restore
dotnet test OpsManager.sln --no-build
```

Test coverage in Prompt 01 includes:

- Domain constructor guards, task transitions, schedule recurrence, evidence requirements, order delivery/receipt, quantities, and enum string-code stability.
- Service clock behavior.
- API startup, liveness, unavailable readiness, sanitized Problem Details, and database-backed readiness.
- PostgreSQL migration application, unique constraints, enum/JSONB mapping, snapshots, tenant isolation, pagination, soft deletion, transaction rollback, and idempotent development seeding.

Repository integration tests use `Testcontainers.PostgreSql`; EF Core InMemory is not used as evidence of PostgreSQL behavior. If the Docker CLI/daemon is unavailable, Docker-dependent tests report explicit skips. On a Docker-enabled machine, all those tests start disposable PostgreSQL 17 containers and must pass before merging persistence changes.

Validate migration drift and generate a reviewable SQL script with:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations has-pending-model-changes --project src/OpsManager.Repository --startup-project src/OpsManager.Repository --no-build
dotnet tool run dotnet-ef migrations script 0 InitialCreate --idempotent --project src/OpsManager.Repository --startup-project src/OpsManager.Repository --no-build
```
