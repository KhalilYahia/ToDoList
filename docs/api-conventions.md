# API conventions

Prompt 01 provides bootstrap endpoints only. Feature APIs added in Prompt 02 must follow these rules:

- Version routes under `/api/v1` and use plural resource names.
- Accept and return DTOs, never EF entities.
- Use `page`, `pageSize`, `totalCount`, and `items` for pagination.
- Pass cancellation tokens from HTTP through Service and Repository.
- Return 201 for creation, 204 for successful no-content mutations, 400 for validation, 401 for unauthenticated access, 403 for explicit policy denial, 404 for missing or cross-tenant resources, and 409 for state conflicts.
- Return RFC 7807 Problem Details and never include stack traces, hashes, or cross-tenant information.
- Resolve `OrganizationId` from authenticated claims; ignore tenant IDs supplied by ordinary clients.
- Keep controllers thin: HTTP translation only, with business transitions and authorization-aware workflows in Service.

OpenAPI is generated at `/openapi/v1.json` in Development and Testing. Production publication can be enabled deliberately after deployment controls are defined.
