# Prompt 01 — Create the Backend Structure, Domain Entities, and EF Core Model

Use the repository skill `$ops-manager-project`.

## Objective

Create the initial backend solution for OpsManager using ASP.NET Core Web API, Entity Framework Core, PostgreSQL, N-tier architecture, and DDD-inspired domain modeling.

This prompt is intentionally limited to:

- Creating the solution and projects.
- Establishing project references and folders.
- Creating Domain entities, enums, contracts, and repository abstractions.
- Creating the EF Core DbContext and entity configurations.
- Implementing the GenericRepository and UnitOfWork infrastructure.
- Creating the initial PostgreSQL migration and development seed.
- Adding foundational tests and documentation.

Do **not** implement the complete business logic, authentication endpoints, feature controllers, reports, or frontend in this prompt. Those belong to later prompts.

## Mandatory architectural rules

1. Place entities, enums, domain primitives, repository contracts, `IGenericRepository<TEntity>`, and `IUnitOfWork` in Domain.
2. Place `DbContext`, EF configurations, migrations, `GenericRepository<TEntity>`, and `UnitOfWork` in Repository.
3. Reserve Service for DTOs and business logic; create only the project and minimal abstractions required by this prompt.
4. Keep API thin; create only composition, health checks, middleware placeholders that are fully functional, and development configuration.
5. API and Service must not access DbContext directly.
6. Do not expose `IQueryable` outside Repository.
7. Use PostgreSQL-compatible configurations and tests.
8. Follow SOLID and clean-code principles.
9. Add documentation in every batch.
10. Do not add shifts, categories, tags, inventory, online payments, or translation tables.

## Expected solution name

Use `OpsManager` unless an existing repository already establishes another name. If the repository is not empty, inspect it and preserve established naming.

---

# Batch 0 — Inspect the environment and write the implementation plan

Before changing files:

1. Inspect the repository and current Git status.
2. Read `AGENTS.md` and `.agents/skills/ops-manager-project/SKILL.md`.
3. Run:
   - `dotnet --info`
   - `dotnet --list-sdks`
   - `docker --version` if Docker may be used for PostgreSQL tests.
4. Determine the latest installed stable LTS .NET target.
5. Check whether PostgreSQL or Docker is available.
6. Produce a concise implementation plan mapping each batch to files and tests.
7. Record material assumptions in `docs/decisions/0001-initial-architecture.md`.

Do not scaffold until the plan is complete.

Acceptance criteria:

- The selected target framework is documented.
- Existing user files are not overwritten.
- The plan explicitly preserves N-tier dependency direction.

---

# Batch 1 — Scaffold the solution and project structure

Create:

```text
OpsManager.sln
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
docs/
  decisions/
```

Configure project references:

```text
OpsManager.Repository -> OpsManager.Domain
OpsManager.Service    -> OpsManager.Domain
OpsManager.Api        -> OpsManager.Service
OpsManager.Api        -> OpsManager.Repository

Tests reference only the layers they test.
```

Requirements:

- Enable nullable reference types.
- Enable implicit usings where appropriate.
- Treat new compiler warnings seriously; do not globally suppress them.
- Add a solution-level `.editorconfig`.
- Add `.gitignore`.
- Add `Directory.Build.props` for shared safe defaults if useful.
- Add `Directory.Packages.props` only if central package management improves clarity.
- Add a root `README.md`.
- Add `docs/architecture.md` with a Mermaid or text dependency diagram.
- Add `docs/setup.md`.
- Add `.env.example` or equivalent configuration documentation without secrets.
- Add a development `docker-compose.yml` for PostgreSQL if Docker is available. It must use environment variables and a named volume.

Do not create empty classes just to populate folders.

Validation:

- `dotnet restore`
- `dotnet build`
- `dotnet test`

---

# Batch 2 — Create Domain primitives and shared contracts

Create a small, coherent foundation in Domain.

## Base types and interfaces

Create or equivalent:

- `BaseEntity` with `Guid Id`.
- `IAuditableEntity` with `CreatedAt` and `UpdatedAt`.
- `ISoftDeletable` with nullable `DeletedAt`.
- `ITenantEntity` with `OrganizationId`.
- Optional `AggregateRoot` marker if it adds practical value.
- Domain exceptions for invalid state transitions and invariant violations.

Use `DateTimeOffset` for moments.

## Repository contracts

Create:

- `IGenericRepository<TEntity>`.
- `IUnitOfWork`.
- A specification/filter abstraction only if needed to avoid leaking IQueryable.
- Pagination model suitable for repository reads.
- Transaction abstraction exposed through UnitOfWork without leaking EF types.

Minimum repository capabilities:

- Get by ID.
- First or default by criteria/specification.
- List with criteria/specification and pagination.
- Count.
- Any.
- Add one or many.
- Update.
- Remove/soft-delete.
- Cancellation tokens for all async operations.

`IUnitOfWork` should expose repositories consistently, for example:

```csharp
IGenericRepository<TEntity> Repository<TEntity>()
    where TEntity : BaseEntity;
```

and:

- `SaveChangesAsync`.
- Begin/commit/rollback transaction helpers.

Do not expose `IQueryable`, `DbSet`, or EF Core types.

## System constants

Create constants for:

- Claim names.
- Organization roles.
- Platform roles.
- Supported languages.
- Subscription feature keys.

## Shared enums

Create string-mapped enums or equivalent domain codes for:

- OrganizationStatus.
- UserAccountStatus.
- OrganizationRole.
- EvidenceMode.
- TaskPriority.
- TaskStatus.
- TaskItemStatus.
- RecurrenceType.
- AttachmentType.
- DepartmentOrderStatus.
- DepartmentOrderItemStatus.
- UnitCode.
- ComplaintStatus.
- ComplaintVisibility.
- SubscriptionStatus.
- BillingMode.
- SubscriptionActionType.
- PaymentMethod.
- PaymentStatus.
- PlatformRole.
- NotificationType if notifications are included.

Add unit tests for critical enum/code stability if serialization depends on it.

Update:

- `docs/data-model.md`
- `docs/architecture.md`

---

# Batch 3 — Create organization, identity, and membership entities

Create the following Domain entities with navigation collections kept controlled and nullable relationships modeled explicitly.

## Organization

Fields:

- `Id`
- `Name`
- `LegalName?`
- `LogoUrl?`
- `Phone?`
- `Email?`
- `Timezone`
- `DefaultLanguage`
- `Status`
- `CreatedBy?`
- Audit and soft-delete fields

Rules:

- Default language must be one of `ar`, `en`, or `ru`.
- Timezone is required.
- Status is separate from subscription status.

## Branch

Fields:

- `OrganizationId`
- `Name`
- `Address?`
- `Phone?`
- `Timezone`
- `IsPrimary`
- `IsActive`
- Audit and soft-delete fields

Rules:

- Branch belongs to exactly one organization.
- At most one active primary branch per organization should be enforced as far as PostgreSQL/Service design reasonably allows.

## Department

Fields:

- `OrganizationId`
- `BranchId`
- `Name`
- `Description?`
- `SupervisorUserId?`
- `IsActive`
- Audit and soft-delete fields

Rules:

- Branch must belong to the same organization.
- Supervisor is optional and must belong to the same organization.

## User

Fields:

- `FullName`
- `Email?`
- `NormalizedEmail?`
- `Phone?`
- `PasswordHash`
- `ProfileImageUrl?`
- `PreferredLanguage`
- `AccountStatus`
- `LastLoginAt?`
- Audit and soft-delete fields

Rules:

- At least one login identifier must be supported. Prefer unique email for the first implementation; document the decision.
- PasswordHash is never serialized.
- Preferred language must be supported.

## RefreshToken

Fields:

- `UserId`
- `TokenHash`
- `ExpiresAt`
- `CreatedAt`
- `RevokedAt?`
- `ReplacedByTokenId?`
- `CreatedByIp?`
- `RevokedByIp?`

Rules:

- Store only a hash.
- Support rotation and revocation.

## OrganizationMember

Fields:

- `OrganizationId`
- `UserId`
- `Role`
- `IsActive`
- `JoinedAt`
- `LeftAt?`
- Audit fields

Rules:

- Unique membership per organization/user.
- MVP roles: Manager, Supervisor, Employee.

## UserDepartment

Fields:

- `UserId`
- `DepartmentId`
- `IsPrimary`
- `JoinedAt`
- `LeftAt?`

Rules:

- Unique active relation per user/department.
- User and department must belong to the same organization.

Do not add a shift entity.

Add Domain tests for constructor guards and invariants.

---

# Batch 4 — Create task template, schedule, task, and evidence entities

## TaskTemplate

Fields:

- `OrganizationId`
- `BranchId?`
- `DefaultDepartmentId`
- `DefaultAssigneeUserId?`
- `Title`
- `Description?`
- `DefaultPriority`
- `DefaultDurationMinutes?`
- `RequiresApproval`
- `CreatedBy`
- `IsActive`
- Audit and soft-delete fields

## TaskTemplateItem

Fields:

- `TaskTemplateId`
- `Title`
- `Description?`
- `SortOrder`
- `IsRequired`
- `EvidenceMode`
- Audit fields

## TaskTemplateItemAttachment

Fields:

- `TaskTemplateItemId`
- `FileUrl`
- `FileType`
- `Caption?`
- `UploadedBy`
- `CreatedAt`

## TaskSchedule

Fields:

- `OrganizationId`
- `TaskTemplateId`
- `BranchId`
- `DepartmentId`
- `AssigneeUserId?`
- `RecurrenceType`
- `RecurrenceInterval`
- `Weekdays` as a PostgreSQL-compatible collection or a normalized owned/value representation
- `MonthDay?`
- `RecurrenceRule?`
- `StartDate`
- `EndDate?`
- `StartTime`
- `DueTime`
- `IsActive`
- `CreatedBy`
- Audit fields

Rules:

- Recurrence interval is positive.
- Weekly recurrence requires weekdays.
- Monthly recurrence requires a valid month day.
- EndDate cannot precede StartDate.
- Do not implement full RRULE parsing now; reserve the field for future use.

## Task

Fields:

- `OrganizationId`
- `BranchId`
- `DepartmentId`
- `AssigneeUserId?`
- `TaskTemplateId?`
- `TaskScheduleId?`
- `ParentTaskId?`
- `Title`
- `Description?`
- `OccurrenceDate`
- `ScheduledStartAt`
- `DueAt`
- `Priority`
- `Status`
- `RequiresApproval`
- `IsScheduleOverride`
- `StartedAt?`
- `CompletedAt?`
- `ApprovedAt?`
- `ApprovedBy?`
- `BlockedReason?`
- `CreatedBy`
- `CancelledAt?`
- `CancelledBy?`
- Audit and soft-delete fields

Rules:

- DueAt must be later than ScheduledStartAt.
- Task instance text is a snapshot.
- Completed historical tasks are soft-deleted only.
- Add entity methods/guards for valid state transitions, while full authorization-aware orchestration remains for Service.

## TaskItem

Fields:

- `TaskId`
- `TemplateItemId?`
- `Title`
- `Description?`
- `SortOrder`
- `IsRequired`
- `EvidenceMode`
- `Status`
- `CompletedBy?`
- `CompletedAt?`
- `Note?`
- Audit fields

## TaskAttachment

Fields:

- `TaskId`
- `TaskItemId?`
- `UploadedBy`
- `FileUrl`
- `FileType`
- `AttachmentType`
- `Caption?`
- `CreatedAt`

## TaskStatusHistory

Fields:

- `TaskId`
- `OldStatus?`
- `NewStatus`
- `ChangedBy`
- `Reason?`
- `CreatedAt`

Required unique constraint:

```text
TaskScheduleId + OccurrenceDate + ScheduledStartAt
```

It should apply when TaskScheduleId is not null.

Add Domain tests for:

- Invalid due time.
- Invalid transitions.
- Required evidence metadata rules that can be enforced at entity level.
- Schedule recurrence guards.

---

# Batch 5 — Create department order entities

## OrderTemplate

Fields:

- `OrganizationId`
- `BranchId`
- `Name`
- `Description?`
- `SourceDepartmentId`
- `TargetDepartmentId`
- `RequiresApproval`
- `AllowCustomItems`
- `CreatedBy`
- `IsActive`
- Audit and soft-delete fields

Rules:

- Source and target departments differ.
- Both belong to the same organization and branch, unless the architecture explicitly documents cross-branch orders as supported. For MVP, keep orders within one branch.

## OrderTemplateItem

Fields:

- `OrderTemplateId`
- `Name`
- `Description?`
- `UnitCode`
- `CustomUnitLabel?`
- `DefaultQuantity?`
- `MinimumQuantity?`
- `SortOrder`
- `ImageUrl?`
- `IsActive`
- Audit fields

Rules:

- `CustomUnitLabel` is required only when UnitCode is Custom.
- Quantities must be non-negative.

## DepartmentOrder

Fields:

- `OrganizationId`
- `BranchId`
- `OrderNumber`
- `OrderTemplateId?`
- `SourceDepartmentId`
- `TargetDepartmentId`
- `CreatedBy`
- `AssignedTo?`
- `Priority`
- `Status`
- `RequestedAt`
- `RequiredAt?`
- `GeneralNote?`
- `AcceptedAt?`
- `AcceptedBy?`
- `ReadyAt?`
- `DeliveredAt?`
- `DeliveredBy?`
- `ReceivedAt?`
- `ReceivedBy?`
- `RejectedAt?`
- `RejectedBy?`
- `RejectionReason?`
- `LinkedTaskId?`
- `CancelledAt?`
- Audit and soft-delete fields

Rules:

- OrderNumber is unique per organization.
- Delivery and receipt are separate.
- Receipt cannot occur before delivery.
- Target and source differ.

## DepartmentOrderItem

Fields:

- `DepartmentOrderId`
- `TemplateItemId?`
- `ItemNameSnapshot`
- `ItemDescriptionSnapshot?`
- `UnitCodeSnapshot`
- `CustomUnitLabelSnapshot?`
- `RequestedQuantity`
- `FulfilledQuantity`
- `ReceivedQuantity`
- `Status`
- `ItemNote?`
- `FulfillmentNote?`
- `IsCustomItem`
- `PreparedBy?`
- `PreparedAt?`
- Audit fields

## DepartmentOrderAttachment

Fields:

- `DepartmentOrderId`
- `OrderItemId?`
- `UploadedBy`
- `FileUrl`
- `FileType`
- `Caption?`
- `CreatedAt`

## DepartmentOrderStatusHistory

Fields:

- `DepartmentOrderId`
- `OldStatus?`
- `NewStatus`
- `ChangedBy`
- `Note?`
- `CreatedAt`

Add Domain tests for quantity and delivery/receipt invariants.

---

# Batch 6 — Create complaints, subscriptions, notifications, and audit entities

## Complaint

Fields:

- `OrganizationId`
- `BranchId`
- `ComplaintNumber`
- `SubmittedBy`
- `TargetDepartmentId?`
- `AssignedTo?`
- `Title`
- `Description`
- `Status`
- `Visibility`
- `ReviewedAt?`
- `ClosedAt?`
- Audit and soft-delete fields

## ComplaintMessage

Fields:

- `ComplaintId`
- `SenderUserId`
- `MessageText`
- `IsInternal`
- `CreatedAt`

## ComplaintAttachment

Fields:

- `ComplaintId`
- `ComplaintMessageId?`
- `UploadedBy`
- `FileUrl`
- `FileType`
- `CreatedAt`

## SubscriptionPlan

Fields:

- `Name`
- `Code`
- `Description?`
- `MonthlyPrice?`
- `YearlyPrice?`
- `Currency`
- `MaxUsers`
- `MaxBranches`
- `MaxStorageMb`
- `Features` represented without making Domain depend on JSON libraries; configure JSONB in Repository
- `GracePeriodDays`
- `IsActive`
- Audit fields

## OrganizationSubscription

Fields:

- `OrganizationId`
- `PlanId`
- `Status`
- `BillingMode`
- `StartsAt?`
- `EndsAt?`
- `TrialStartedAt?`
- `TrialEndsAt?`
- `GracePeriodEndsAt?`
- `ActivatedByPlatformUserId?`
- `SuspendedAt?`
- `SuspendedByPlatformUserId?`
- `SuspensionReason?`
- `CancelledAt?`
- `Notes?`
- Audit fields

## SubscriptionHistory

Fields:

- `SubscriptionId`
- `OldStatus?`
- `NewStatus`
- `OldEndsAt?`
- `NewEndsAt?`
- `ActionType`
- `ChangedByPlatformUserId`
- `Reason?`
- `CreatedAt`

## ManualPayment

Fields:

- `OrganizationId`
- `SubscriptionId`
- `Amount`
- `Currency`
- `PaymentMethod`
- `PaymentReference?`
- `PaymentStatus`
- `PaidAt?`
- `PeriodStart`
- `PeriodEnd`
- `RecordedByPlatformUserId`
- `ReceiptFileUrl?`
- `Note?`
- `CreatedAt`

## PlatformUser

Fields:

- `FullName`
- `Email`
- `NormalizedEmail`
- `PasswordHash`
- `Role`
- `Status`
- `PreferredLanguage`
- `LastLoginAt?`
- Audit fields

## Notification

For basic in-app notifications:

- `OrganizationId`
- `UserId`
- `NotificationType`
- `Parameters` represented cleanly in Domain and mapped to JSONB
- `Title`
- `Body`
- `RelatedEntityType?`
- `RelatedEntityId?`
- `IsRead`
- `ReadAt?`
- `CreatedAt`

## AuditLog and PlatformAuditLog

Include:

- Actor ID.
- Organization ID where applicable.
- Action.
- EntityType.
- EntityId.
- OldValues.
- NewValues.
- IP address where applicable.
- UserAgent where applicable.
- CreatedAt.

Do not let audit logging introduce a dependency from Domain to ASP.NET Core.

Add basic entity tests.

---

# Batch 7 — Implement EF Core DbContext and entity configurations

Create `OpsManagerDbContext` in Repository.

Requirements:

1. DbSets for all implemented entities.
2. One configuration class per entity or aggregate-owned component.
3. Table and column naming convention documented and applied consistently.
4. UUID primary keys.
5. PostgreSQL `timestamptz` for DateTimeOffset.
6. Decimal precision:
   - Money: choose and document a safe precision such as `numeric(18,2)`.
   - Quantities: choose and document a precision such as `numeric(18,3)`.
7. String lengths for names, codes, URLs, and status values.
8. Enum-to-string conversion.
9. JSONB configuration for feature maps, notification parameters, and audit snapshots.
10. Foreign-key delete behavior chosen explicitly; avoid accidental cascade deletion of historical records.
11. Soft-delete global filters.
12. Tenant query filtering architecture that can receive the current tenant safely without making Service access DbContext.
13. Unique indexes and common query indexes.

At minimum configure indexes for:

- Organization default/system lookup fields.
- Branch names within organization.
- Department names within branch.
- User normalized email.
- Organization membership uniqueness.
- Task organization/date.
- Task assignee/status.
- Task department/status.
- Task schedule occurrence uniqueness.
- Order target department/status.
- Order source department/created date.
- Order number per organization.
- Complaint number per organization.
- Subscription organization/status and expiration.
- Notification user/read/created date.
- Refresh-token hash and user.

Configure constraints for:

- Positive recurrence interval.
- Due date after start where database enforcement is practical.
- Non-negative quantities.
- Source and target department inequality.
- Valid subscription periods.
- Trial end after trial start.
- Payment period end not before start.

Create Repository integration tests using PostgreSQL Testcontainers when available:

- Migrations apply successfully.
- Important unique constraints work.
- Soft-delete filter works.
- Tenant isolation works.
- Enum and JSONB mappings round-trip.
- Snapshot fields persist correctly.

Do not use EF Core InMemory for these validations.

Update `docs/data-model.md` with relationships, indexes, constraints, and snapshot rationale.

---

# Batch 8 — Implement GenericRepository and UnitOfWork

Implement in Repository:

- `GenericRepository<TEntity>`.
- `UnitOfWork`.
- Specification/filter evaluator if selected.
- Pagination support.
- Transaction support.
- No-tracking read behavior.
- Soft-delete behavior.
- Tenant-aware repository behavior.
- DI registration extension method for Repository.

Rules:

- No IQueryable leaves Repository.
- No Service or API dependency.
- UnitOfWork caches repository instances safely per scope.
- SaveChanges updates audit timestamps consistently.
- Hard deletion requires an explicit infrastructure-only path and must not be the default.
- Cancellation tokens are honored.

Add Repository integration tests covering:

- CRUD.
- Pagination.
- Filters.
- Count and Any.
- Soft delete.
- UnitOfWork transaction rollback.
- Tenant boundary behavior.

---

# Batch 9 — Add initial migration and development seed

Create the first migration.

Create a safe development seed strategy for:

- One platform administrator.
- One active subscription plan.
- One sample organization.
- One primary branch.
- Three sample departments.
- Manager, supervisor, and employee memberships.
- A trial subscription.
- One task template with checklist items.
- One department order template with items.

Requirements:

- Seed passwords come from development configuration or are generated safely and printed only in a clearly marked development setup step.
- Never commit real secrets.
- Seeding is idempotent.
- Production does not run development seed automatically.
- Document migration and seed commands.

Validate:

- Create a fresh database.
- Apply migrations.
- Run seed.
- Query basic row counts through Repository tests or a safe verification command.
- Re-run seed and confirm no duplicates.

---

# Batch 10 — API bootstrap and health checks

Without implementing feature controllers yet:

- Configure dependency injection for Repository and Service placeholders that are genuinely needed.
- Configure PostgreSQL options.
- Configure Problem Details and exception middleware.
- Configure OpenAPI.
- Configure health checks for the API and database.
- Add `/health/live` and `/health/ready`.
- Add environment-specific configuration.
- Add structured request logging using built-in logging or a justified package.
- Configure CORS through settings; do not hard-code production origins.
- Add a simple root/version endpoint if useful.

Do not add business controllers in this prompt.

Add API integration tests for:

- API starts.
- Liveness succeeds.
- Readiness behavior is correct with database available/unavailable.
- Problem Details format for an intentional test exception if a safe test endpoint or test host hook is used.

---

# Batch 11 — Final validation and documentation

Run:

- Formatting.
- `dotnet restore`.
- `dotnet build`.
- All unit tests.
- All integration tests.
- Migration validation against a fresh PostgreSQL database.
- Dependency-cycle check through project references.
- Search to confirm no DbContext injection exists in API or Service.

Complete:

- `README.md`.
- `docs/architecture.md`.
- `docs/data-model.md`.
- `docs/setup.md`.
- `docs/testing.md`.
- `docs/decisions/0001-initial-architecture.md`.
- `docs/decisions/0002-generic-repository-and-unit-of-work.md`.
- `docs/decisions/0003-multi-tenancy-and-soft-delete.md`.

Final response must include:

1. Created project tree.
2. Key architectural decisions.
3. Migration name.
4. Seeded development accounts and how credentials are configured.
5. Commands executed.
6. Build and test results.
7. Known limitations intentionally deferred to Prompt 02.
8. Exact next step: execute `prompts/02-backend-logic-and-apis.md`.

## Do not implement in this prompt

- Login/register/refresh controllers.
- Organization-management controllers.
- Task, order, complaint, subscription, or report controllers.
- Complete Service business logic.
- Background schedule generation.
- Frontend.
- Online payments.
