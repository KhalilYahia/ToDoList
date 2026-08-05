# Setup

## Requirements

- .NET SDK 10
- PostgreSQL 17-compatible server
- Docker only for Testcontainers integration tests

Copy values from `.env.example` into your local environment or user-secret store. Never commit a production database password or JWT signing key.

Required production settings:

```text
ConnectionStrings__OpsManager
Jwt__Issuer
Jwt__Audience
Jwt__SigningKey
Cors__AllowedOrigins__0
```

`Jwt__SigningKey` must be at least 32 UTF-8 bytes. The API refuses to start outside Development/Testing with the committed development placeholder. HTTPS, `RefreshCookie__Secure=true`, and an explicit CORS allowlist are required in production.

CORS origins are exact. Local development permits both
`http://localhost:3000` and `http://127.0.0.1:3000`. Prefer using the same
hostname for both applications—for example, frontend
`http://localhost:3000` and API `http://localhost:5291`—because the refresh
cookie uses `SameSite=Strict`. The browser network-panel text
`strict-origin-when-cross-origin` is its referrer policy, not a CORS error.

## Database

```powershell
dotnet tool restore
dotnet restore OpsManager.sln
dotnet ef database update --project src/OpsManager.Repository --startup-project src/OpsManager.Repository
```

Design-time commands read `OPSMANAGER_DB_CONNECTION`. Runtime reads `ConnectionStrings__OpsManager`.

Migrations:

- `20260722202008_InitialCreate`
- `20260723220445_BackendLogicAndApis`
- `20260723220906_TemplateItemHistoryPreservation`

The Prompt 02 migration revokes legacy refresh sessions because old tokens did not carry tenant ownership. Users log in again after upgrading.

## Optional development seed

```text
Seed__Enabled=true
Seed__Password=YourLocalPassword123
```

The seed password is hashed and never logged. The seeded plan code is `development-standard`, matching the default onboarding configuration.

## Run

```powershell
dotnet run --project src/OpsManager.Api
```

The Development profile permits the refresh cookie over local HTTP. All non-development environments keep it Secure.

### Docker Compose Deployment

To spin up PostgreSQL, the .NET Web API backend, and the Next.js frontend together:

```bash
docker compose up --build -d
```

To stop and remove containers and networks:

```bash
docker compose down
```

After startup:

- API base: `http://localhost:5291/api/v1`
- Swagger UI: `http://localhost:5291/swagger`
- OpenAPI JSON: `http://localhost:5291/openapi/v1.json`
- Liveness: `http://localhost:5291/health/live`
- Database readiness: `http://localhost:5291/health/ready`

Swagger and the generated OpenAPI document are available only in Development and Testing.
