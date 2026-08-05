# API conventions

## Routes and payloads

- All feature routes use `/api/v1`.
- Controllers accept and return Service DTOs, never persistence entities.
- List responses use `{ items, page, pageSize, totalCount }`; `pageSize` is 1–200.
- Date-report boundaries are UTC and half-open: `[from, to)`.
- Cancellation tokens flow from controllers through Service and Repository.
- User-created names/titles are stored unchanged except whitespace trimming. They are not translated.

OpenAPI is generated at `/openapi/v1.json` in Development and Testing and includes the HTTP Bearer/JWT security scheme. Interactive Swagger UI is available at `/swagger` in those environments and reads that same generated document. OpenAPI and Swagger UI are intentionally not exposed in Production.

## Authentication

Organization access tokens contain verified `sub`, `organization_id`, and `organization_role` claims. Platform tokens contain `sub` and `platform_role` and cannot satisfy organization policies. Clients cannot select or override the current tenant through DTO fields.

Access tokens default to 15 minutes. Refresh tokens default to 30 days, are random 512-bit values, are stored only as SHA-256 hashes, and rotate on every refresh. Reuse of a revoked token revokes its family. The cookie is HttpOnly, `SameSite=Strict`, scoped to `/api/v1`, and Secure outside local development.

Example onboarding:

```http
POST /api/v1/auth/register-organization
Content-Type: application/json

{
  "organizationName": "North Workshop",
  "legalName": "North Workshop LLC",
  "timezone": "Europe/Moscow",
  "defaultLanguage": "ru",
  "managerFullName": "Alex Manager",
  "managerEmail": "alex@example.com",
  "password": "ExamplePassword123",
  "phone": "+70000000000"
}
```

Login is tenant-explicit because one identity may eventually belong to more than one organization:

```http
POST /api/v1/auth/login
Content-Type: application/json

{
  "organizationId": "00000000-0000-0000-0000-000000000000",
  "email": "alex@example.com",
  "password": "ExamplePassword123"
}
```

## Errors

Failures use RFC 7807 Problem Details. Responses include `traceId`; application failures also include a stable `code`.

| Status | Meaning |
|---:|---|
| 400 | Request validation |
| 401 | Missing/invalid/expired authentication |
| 402 | Subscription or feature restriction |
| 403 | Authenticated identity lacks a policy |
| 404 | Missing or inaccessible/cross-tenant resource |
| 409 | Conflict or invalid state transition |
| 422 | Domain invariant violation |
| 429 | Authentication rate limit |
| 500 | Sanitized unexpected failure |

Cross-tenant and complaint-privacy denials deliberately use 404 where revealing existence would leak information.

## Task assignment contract

Task creation and template-based creation accept an assignment object:

```json
{
  "branchId": "11111111-1111-1111-1111-111111111111",
  "departmentId": "22222222-2222-2222-2222-222222222222",
  "title": "Closing checklist",
  "scheduledStartAt": "2026-07-28T20:00:00Z",
  "dueAt": "2026-07-28T22:00:00Z",
  "assignment": {
    "mode": "SelectedUsers",
    "userIds": [
      "33333333-3333-3333-3333-333333333333",
      "44444444-4444-4444-4444-444444444444"
    ]
  }
}
```

SingleUser requires exactly one ID, SelectedUsers requires at least two distinct IDs, and AllDepartmentMembers requires an empty list. The successful response represents the whole distribution:

```json
{
  "distributionId": "55555555-5555-5555-5555-555555555555",
  "assignmentMode": "SelectedUsers",
  "createdTaskCount": 2,
  "tasks": [
    {
      "taskId": "66666666-6666-6666-6666-666666666666",
      "assigneeUserId": "33333333-3333-3333-3333-333333333333",
      "assigneeName": "Employee A",
      "status": "NotStarted"
    }
  ]
}
```

The list contains every created copy; current request limits keep it bounded by the eligible department population. Task query filters include assignee, schedule, and distribution IDs. Employees receive only their own rows regardless of query parameters.
