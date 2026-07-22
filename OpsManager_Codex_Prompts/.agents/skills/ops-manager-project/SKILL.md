---
name: ops-manager-project
description: Build and modify the OpsManager multi-tenant operations SaaS using ASP.NET Core Web API, EF Core, PostgreSQL, N-tier DDD, and Next.js/React. Use for project scaffolding, entities, repositories, services, APIs, reports, frontend pages, tests, migrations, and documentation in this repository. Do not use for unrelated projects.
---

# OpsManager Project Skill

Apply these instructions to all work in this repository.

## 1. Product scope

OpsManager is a multi-tenant B2B operations system for restaurants, workshops, and small businesses.

The MVP includes:

1. Organizations, branches, departments, users, and role-based permissions.
2. Task templates, checklist items, schedules, task instances, evidence attachments, approvals, and status history.
3. Department order templates and actual orders between departments, including selected items, quantities, preparation, delivery, and receipt confirmation.
4. Complaints, messages, attachments, visibility, assignment, and status tracking.
5. Manually managed trials, plans, organization subscriptions, manual payments, and platform administration.
6. Basic reports for tasks, department orders, complaints, and subscriptions.
7. Arabic, English, and Russian interface localization. User-created content remains in the language in which it was entered.

Out of scope:

- Shifts or attendance.
- Categories or tags.
- Inventory, purchasing, accounting, or ERP functions.
- Automatic online payments.
- AI features.
- Real-time chat.
- Automatic translation of user-created content.
- Microservices, CQRS, event sourcing, or unnecessary distributed infrastructure.

## 2. Technology stack

### Backend

- ASP.NET Core Web API.
- C# with nullable reference types enabled.
- Entity Framework Core.
- PostgreSQL through Npgsql.
- OpenAPI/Swagger.
- FluentValidation or an equivalent centralized validation approach.
- Built-in dependency injection and logging.
- xUnit for new test projects.

Before creating the solution, run `dotnet --list-sdks`. Use the latest installed stable LTS target supported by the environment. Never silently change the target framework of an existing repository.

### Frontend

- Next.js App Router.
- React and TypeScript in strict mode.
- Tailwind CSS.
- TanStack Query.
- React Hook Form and Zod.
- `next-intl` or the repository's established i18n solution.
- Responsive and accessible UI with Arabic RTL support.

Use the existing package manager. For a new frontend, prefer pnpm when available; otherwise use npm.

## 3. Required architecture

Use N-tier architecture with DDD-inspired domain modeling.

```text
src/
  OpsManager.Domain/
  OpsManager.Repository/
  OpsManager.Service/
  OpsManager.Api/
tests/
  OpsManager.Domain.Tests/
  OpsManager.Repository.IntegrationTests/
  OpsManager.Service.Tests/
  OpsManager.Api.IntegrationTests/
frontend/
  ops-manager-web/
docs/
```

Dependency direction:

```text
Domain <- Repository
Domain <- Service
Service + Repository <- API
```

### Domain

Place in `OpsManager.Domain`:

- Entities and aggregate roots.
- Value objects when useful.
- Enums and constants.
- Base entity, tenant, audit, and soft-delete contracts.
- `IGenericRepository<TEntity>`.
- `IUnitOfWork`.
- Repository/specification contracts.
- Entity-level invariants and transition guards.

Domain must not reference EF Core, ASP.NET Core, PostgreSQL, Repository, Service, or API.

### Repository

Place in `OpsManager.Repository`:

- EF Core DbContext.
- `IEntityTypeConfiguration<TEntity>` classes.
- PostgreSQL mappings, indexes, constraints, and migrations.
- `GenericRepository<TEntity>`.
- `UnitOfWork`.
- Transactions, seed infrastructure, tenant filters, and soft-delete filters.

Repository contains persistence logic, not business decisions.

### Service

Place in `OpsManager.Service`:

- Service interfaces and implementations.
- Business workflows and authorization-aware use cases.
- Request/response DTOs in feature-based `DTOs` folders.
- Validators and mappings.
- Report query services.
- Contracts for current user, clock, token service, file storage, and notifications.

Business orchestration belongs in Service.

### API

Place in `OpsManager.Api`:

- Thin controllers.
- Authentication and authorization configuration.
- Dependency-injection composition.
- Middleware and exception handling.
- OpenAPI configuration.
- Hosted services that call Service-layer workflows.

Controllers accept HTTP input, call Service, and return HTTP results. They contain no EF queries, business calculations, or status-transition logic.

## 4. Mandatory data-access rules

1. Query and mutate persisted data only through `IUnitOfWork` and `IGenericRepository<TEntity>`.
2. Never inject or query `DbContext` from API or Service.
3. Never expose `IQueryable` outside Repository.
4. All repository operations are asynchronous and cancellation-aware.
5. Use specifications or expression-based filters for complex queries.
6. Use `AsNoTracking` for read-only queries inside Repository.
7. Paginate list endpoints.
8. Commit one logical business operation once through UnitOfWork.
9. Use transactions when a multi-entity workflow must be atomic.
10. Avoid N+1 queries and excessive `Include` chains.
11. Use the correct name `UnitOfWork`, never `UnitPfWork`.

The generic repository should support a small coherent set such as:

- `GetByIdAsync`
- `FirstOrDefaultAsync`
- `ListAsync`
- `CountAsync`
- `AnyAsync`
- `AddAsync`
- `AddRangeAsync`
- `Update`
- `Remove`

`IUnitOfWork` should expose repositories consistently, such as `Repository<TEntity>()`, plus `SaveChangesAsync` and transaction helpers.

## 5. Multi-tenancy and security

- Every tenant-owned record must be scoped by `OrganizationId` directly or through a guaranteed parent.
- Enforce tenant filtering server-side using repository/EF filters plus Service authorization checks.
- Do not trust `OrganizationId` sent by the client. Resolve tenant identity from authenticated claims except for explicit platform-admin operations.
- Validate that referenced branches, departments, users, templates, tasks, orders, and complaints belong to the current organization.
- Use policy-based authorization.
- Organization roles: `Manager`, `Supervisor`, `Employee`.
- Platform roles are separate.
- Use ASP.NET Core password hashing; never implement cryptography manually.
- Use short-lived JWT access tokens and rotating refresh tokens.
- Store refresh tokens hashed.
- Prefer secure HttpOnly refresh-token cookies.
- Never store access tokens in browser localStorage.
- Return RFC 7807 Problem Details.
- Never expose stack traces, password hashes, token hashes, or cross-tenant data.

## 6. Database conventions

- Use UUID/`Guid` primary keys.
- Use UTC `DateTimeOffset` and PostgreSQL `timestamptz` for moments.
- Use `DateOnly` and `TimeOnly` only for true date-only/time-only concepts.
- Use `decimal` for money and quantities.
- Add `CreatedAt`, `UpdatedAt`, and optional `DeletedAt` consistently.
- Use soft deletion for historical operational data.
- Store enums as strings unless an ADR explains otherwise.
- Add tenant-aware indexes and unique constraints.
- Store attachment metadata and object-storage URLs only; never binary files in PostgreSQL.
- Do not add translation tables.
- Supported UI languages are `ar`, `en`, and `ru`.
- Do not add shifts, categories, or tags.

## 7. Core domain rules

### Tasks

- A template is reusable and is not an execution.
- Creating a task from a template copies title, description, checklist items, evidence requirements, and defaults into task-instance snapshots.
- Editing a template never changes historical tasks.
- A schedule creates independent task instances.
- Generate only a bounded future window, initially 30 days.
- Prevent duplicate generated occurrences with a unique database constraint.
- A required item with required evidence cannot be completed without an attachment.
- Completed tasks are never hard-deleted.

### Department orders

- A template defines selectable items between one source department and one target department.
- Actual orders copy selected item names, descriptions, units, and quantities as snapshots.
- The module does not track stock.
- Source and target departments must belong to the same organization and must differ.
- Delivery and receipt are separate actions.
- Receipt cannot be confirmed before delivery.
- Quantities cannot be negative.

### Complaints

- Complaints may be management-only or visible to explicitly allowed participants.
- Internal messages must never be returned to unauthorized users.
- Important changes are audited.

### Subscriptions

- Subscription state belongs to the organization, not employees.
- A new organization receives a configurable trial, initially 14 days.
- Billing is manual in the MVP.
- After the grace period, expired organizations become read-only; data remains available.
- User account status and organization subscription status are independent.
- Platform admins can record payments, activate, extend, suspend, reactivate, and expire subscriptions.

## 8. API conventions

- Version routes under `/api/v1`.
- Use plural resource names.
- Never expose EF entities from controllers.
- Use request/response DTOs.
- Use consistent pagination: `page`, `pageSize`, `totalCount`, `items`.
- Pass cancellation tokens through all layers.
- Use HTTP semantics consistently: 201 create, 204 no-content success, 400 validation, 401 unauthenticated, 403 unauthorized, 404 missing/inaccessible, 409 state conflict.
- Treat cross-tenant access as 404 unless an explicit policy requires 403.
- Maintain OpenAPI documentation.

## 9. Frontend conventions

- Use feature-based folders.
- Separate API types, query hooks, forms, pages, and reusable UI primitives.
- Keep server state in TanStack Query.
- Use URL parameters for filters and pagination where practical.
- Use React Hook Form with Zod.
- Implement loading, empty, error, permission-denied, and subscription read-only states.
- Render navigation by role, but do not treat hidden UI as authorization.
- Support desktop and mobile.
- Arabic is RTL; English and Russian are LTR.
- Translate system UI only, never user-created content.
- Use semantic HTML, labels, keyboard support, focus management, and accessible dialogs.

## 10. Clean-code rules

- Follow SOLID without unnecessary abstraction.
- Prefer small cohesive classes and services.
- Use dependency inversion for external concerns.
- Avoid service locators and global mutable state.
- Split oversized services by use case.
- Use constants for roles, claims, feature keys, and system codes.
- Comments explain why, not obvious code.
- Remove dead code and unused packages.
- Do not add a package or pattern without a real need.

## 11. Testing

For every feature batch:

- Add unit tests for business rules and transitions.
- Add Repository integration tests for mappings, tenant isolation, and constraints.
- Add API integration tests for authorization, validation, and success paths.
- Add at least one cross-tenant test per protected resource family.
- Prefer PostgreSQL Testcontainers when Docker is available.
- Do not use EF Core InMemory as proof of PostgreSQL behavior.
- Run formatting, build, tests, and migration validation before completion.

## 12. Documentation

Maintain:

- `README.md`
- `docs/architecture.md`
- `docs/data-model.md`
- `docs/api-conventions.md`
- `docs/authorization-matrix.md`
- `docs/setup.md`
- `docs/testing.md`
- `docs/decisions/` for ADRs
- `.env.example` without secrets
- OpenAPI generation instructions

Documentation must describe implemented behavior accurately.

## 13. Codex execution protocol

1. Read this skill and all applicable `AGENTS.md` files.
2. Inspect the repository before changing files.
3. State assumptions and a short batch plan.
4. Implement one bounded batch at a time.
5. After each batch, run formatter, build, tests, and migration checks.
6. Fix failures before continuing.
7. Update documentation in the same batch.
8. Do not perform destructive database or Git actions without explicit permission.
9. Preserve unrelated user changes.
10. Do not create empty placeholders merely to fill the tree.
11. Stop at a safe batch boundary if a real product decision blocks correct work.
12. End with changed files, commands, results, migrations, risks, and next step.

## 14. Definition of done

A batch is done only when:

- Layer boundaries are respected.
- API and Service never query DbContext.
- DTOs are used externally.
- Tenant and role checks are enforced.
- Validation and error handling exist.
- Relevant tests pass.
- No new build warnings remain.
- Schema changes have valid migrations.
- Documentation is updated.
- No secrets, build output, or local machine paths are committed.
