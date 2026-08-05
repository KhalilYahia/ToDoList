# Testing

Run formatting, compilation, and all suites:

```powershell
dotnet format OpsManager.sln --no-restore
dotnet build OpsManager.sln --no-restore
dotnet test OpsManager.sln --no-build
```

Coverage includes:

- domain guards, evidence, explicit task transitions/timestamps, overdue derivation, order receipt/quantity rules, and stable enum codes;
- daily/weekly/monthly recurrence and checklist validation;
- API startup, liveness/readiness, sanitized Problem Details, invalid/expired JWTs, role policies, tenant claim context, and OpenAPI generation;
- PostgreSQL migrations, relational constraints, `xmin` stale-write conflicts, scheduled-occurrence uniqueness, JSON/string enum mapping, tenant isolation, pagination, transactions, soft deletion, and seed idempotency;
- frontend task/template/schedule contracts, the eight task status codes, nullable template department, enum weekdays, and overnight due offsets.

Repository/API PostgreSQL tests use Testcontainers PostgreSQL 17. If Docker is unavailable, they explicitly skip rather than substituting EF InMemory. A CI/merge environment with Docker must run them without skips.

Migration checks:

```powershell
dotnet ef migrations has-pending-model-changes --project src/OpsManager.Repository --startup-project src/OpsManager.Repository --no-build
dotnet ef migrations script --idempotent --project src/OpsManager.Repository --startup-project src/OpsManager.Repository --no-build
```

A clean validation database should receive all migrations before release. The Prompt 02 implementation was validated this way and the temporary database was removed afterward.
