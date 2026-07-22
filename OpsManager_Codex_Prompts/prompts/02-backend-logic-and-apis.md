# Prompt 02 — Implement Backend Business Logic, APIs, Scheduling, and Reports

Use the repository skill `$ops-manager-project`.

## Objective

Continue from the backend structure created by Prompt 01 and implement the MVP business logic and HTTP APIs.

The required modules are:

1. Authentication and authorization.
2. Organizations, branches, departments, users, and memberships.
3. Task templates, schedules, task instances, checklist execution, evidence, and approvals.
4. Department order templates and actual department orders.
5. Complaints.
6. Manual trials, subscriptions, plans, payments, and platform administration.
7. Basic reports.
8. In-app notifications and audit logging where required.

The implementation must preserve the existing Domain/Repository/Service/API boundaries.

## Mandatory rules

- Service and API must never query DbContext directly.
- All persistence goes through UnitOfWork and GenericRepository.
- Controllers stay thin.
- Request/response DTOs live in feature-based Service DTO folders.
- User-provided OrganizationId is never trusted for tenant operations.
- Every list endpoint is paginated unless it is a small controlled lookup.
- Every protected resource is tenant-scoped and role-checked.
- Return Problem Details for failures.
- Add tests and documentation in each batch.
- Do not add shifts, categories, tags, inventory, automatic translation, or online payments.
- Do not introduce MediatR, CQRS, or another framework unless the repository already uses it and the decision is documented.

---

# Batch 0 — Inspect Prompt 01 output and produce a gap report

1. Read the skill, AGENTS.md, architecture docs, data model, migrations, and tests.
2. Build and test the solution before changing it.
3. Inspect GenericRepository and UnitOfWork contracts.
4. Verify that API and Service do not reference DbContext.
5. Verify PostgreSQL integration tests are operational.
6. Produce a short gap report against this prompt.
7. Update the execution plan before implementing.

If Prompt 01 is incomplete or broken, fix foundational defects first and document them.

---

# Batch 1 — Cross-cutting Service and API infrastructure

Implement the reusable application infrastructure before feature endpoints.

## Service abstractions

Create:

- `ICurrentUserContext`.
- `ICurrentTenantContext`.
- `IClock`.
- `IPasswordService`.
- `ITokenService`.
- `IFileStorageService`.
- `INotificationService`.
- `IAuditService`.
- Pagination DTOs.
- Shared result/error model only if it improves controller consistency without duplicating Problem Details.

Create a system clock implementation and a development/local file-storage implementation behind the interface. File storage must be replaceable with object storage later.

## Validation and errors

- Add validators for request DTOs.
- Add a global exception-to-Problem-Details middleware or handler.
- Map validation, not found, forbidden, conflict, subscription restriction, and invalid transition errors consistently.
- Include a correlation/trace identifier.
- Do not expose implementation details.

## Authentication foundations

- Configure JWT bearer authentication.
- Use short-lived access tokens.
- Use rotating refresh tokens stored hashed.
- Prefer secure HttpOnly refresh-token cookies.
- Add configurable issuer, audience, signing key, access lifetime, and refresh lifetime.
- Add policies for Manager, Supervisor, Employee, and platform roles.
- Separate organization access from platform administration.
- Add current-user and current-tenant implementations based on verified claims.

## API conventions

Create shared API behavior for:

- Pagination.
- Validation.
- Problem Details.
- Cancellation tokens.
- OpenAPI security scheme.
- API version prefix `/api/v1`.

Tests:

- Authentication middleware behavior.
- Invalid token.
- Expired token.
- Problem Details format.
- Role-policy checks.
- Current tenant cannot be overridden by request data.

Documentation:

- Update `docs/api-conventions.md`.
- Create/update `docs/authorization-matrix.md`.
- Document token and cookie behavior without including secrets.

---

# Batch 2 — Authentication and onboarding APIs

Implement business logic and thin controllers for:

## Organization onboarding

Endpoint concept:

```text
POST /api/v1/auth/register-organization
```

Request includes:

- Organization name.
- Legal name optional.
- Timezone.
- Default language.
- Manager full name.
- Manager email.
- Password.
- Optional phone.

Atomic workflow:

1. Validate supported language and timezone.
2. Ensure normalized email is unique according to the documented identity policy.
3. Create organization.
4. Create primary branch with a sensible default name.
5. Create manager user.
6. Create manager membership.
7. Create a trial subscription using the configured default plan and trial duration.
8. Create audit entries.
9. Return authentication result and organization summary.

Use a transaction.

## Login and token endpoints

Implement:

```text
POST /api/v1/auth/login
POST /api/v1/auth/refresh
POST /api/v1/auth/logout
GET  /api/v1/auth/me
```

Requirements:

- Login checks account status, membership status, organization status, and subscription access mode.
- Refresh rotates and revokes the old refresh token.
- Logout revokes the current refresh token.
- `me` returns user, membership, organization, role, language, and subscription access state.
- Do not return password or token hashes.
- Add throttling/rate limiting using built-in ASP.NET Core facilities where practical.
- Add audit records for important authentication events without logging secrets.

Tests:

- Successful onboarding.
- Rollback when a dependent creation fails.
- Duplicate email.
- Login success/failure.
- Refresh rotation and reuse detection.
- Suspended user.
- Suspended organization.
- Expired read-only organization can authenticate but receives access-state metadata.
- Cross-tenant claim tampering is rejected.

---

# Batch 3 — Organization, branch, department, and user management

Implement Services, DTOs, validators, mappings, and controllers.

## Organization endpoints

```text
GET   /api/v1/organization
PATCH /api/v1/organization
```

Manager only for updates.

Editable fields:

- Name.
- Legal name.
- Logo URL through file service if implemented.
- Phone.
- Email.
- Timezone.
- Default language.

Do not allow tenant clients to change subscription state here.

## Branch endpoints

```text
GET    /api/v1/branches
POST   /api/v1/branches
GET    /api/v1/branches/{id}
PATCH  /api/v1/branches/{id}
DELETE /api/v1/branches/{id}
```

Rules:

- Manager manages branches.
- Enforce subscription `MaxBranches`.
- Preserve historical data with soft delete.
- Do not delete the only active branch or a branch with unsafe dependencies without a conflict response.

## Department endpoints

```text
GET    /api/v1/departments
POST   /api/v1/departments
GET    /api/v1/departments/{id}
PATCH  /api/v1/departments/{id}
DELETE /api/v1/departments/{id}
```

Rules:

- Manager can manage all.
- Supervisor can read departments relevant to their branch.
- Supervisor assignment must reference an active member of the same organization.
- Department names are user-created content and are not translated.

## User/member endpoints

```text
GET    /api/v1/members
POST   /api/v1/members
GET    /api/v1/members/{id}
PATCH  /api/v1/members/{id}
POST   /api/v1/members/{id}/activate
POST   /api/v1/members/{id}/suspend
PUT    /api/v1/members/{id}/departments
```

Rules:

- Manager manages members and roles.
- Supervisor may read members in authorized departments but cannot promote roles.
- Enforce subscription `MaxUsers`.
- Membership status and account status remain separate.
- Prevent removing or suspending the last active manager.
- Password setup/reset must use secure one-time workflow or a clearly documented MVP invitation approach.
- Never allow an employee to assign themselves a higher role.

Tests:

- Role permissions.
- User and branch limits.
- Cross-tenant access.
- Last-manager protection.
- Invalid department assignment.
- Soft deletion/history preservation.

Documentation:

- Update authorization matrix.
- Add endpoint examples.

---

# Batch 4 — Task template and reusable checklist APIs

Implement:

```text
GET    /api/v1/task-templates
POST   /api/v1/task-templates
GET    /api/v1/task-templates/{id}
PATCH  /api/v1/task-templates/{id}
DELETE /api/v1/task-templates/{id}
POST   /api/v1/task-templates/{id}/clone
POST   /api/v1/task-templates/{id}/activate
POST   /api/v1/task-templates/{id}/deactivate
```

Template item operations may be nested or handled atomically in template requests:

```text
POST   /api/v1/task-templates/{id}/items
PATCH  /api/v1/task-templates/{id}/items/{itemId}
DELETE /api/v1/task-templates/{id}/items/{itemId}
POST   /api/v1/task-templates/{id}/items/reorder
```

Requirements:

- Manager can create and manage all templates.
- Supervisor may manage templates only if product rules permit; default to read/use unless explicitly granted.
- Employee can read active templates relevant to their department when needed to create permitted tasks.
- Validate branch, department, default assignee, and creator tenant ownership.
- Preserve item order.
- Do not modify historical task instances when template changes.
- Attach instructional files through `IFileStorageService`.
- Validate evidence mode.
- Clone creates a new independent template and items.

Tests:

- CRUD permissions.
- Item ordering.
- Clone behavior.
- Template update does not alter an existing task snapshot.
- Tenant isolation.

---

# Batch 5 — Task creation, execution, approval, and calendar APIs

Implement Service workflows and controllers.

## Create tasks

```text
POST /api/v1/tasks
POST /api/v1/task-templates/{templateId}/create-task
```

Task creation supports:

- One-off task without template.
- Task created from a reusable template.
- Assignment to a department only.
- Optional assignment to a specific active member.
- Scheduled start, due time, priority, approval requirement.
- Optional edits to copied checklist items before creation if allowed.

When created from a template:

- Copy all user-facing data and evidence requirements into Task and TaskItem snapshots.
- Do not reference template text dynamically at read time.

## Task queries

```text
GET /api/v1/tasks
GET /api/v1/tasks/{id}
GET /api/v1/tasks/calendar
GET /api/v1/tasks/my
```

Filters:

- Date range.
- Status.
- Branch.
- Department.
- Assignee.
- Priority.
- Overdue.
- Template ID.

Calendar returns task instances, not schedule definitions.

## Task actions

```text
POST /api/v1/tasks/{id}/assign
POST /api/v1/tasks/{id}/start
POST /api/v1/tasks/{id}/block
POST /api/v1/tasks/{id}/resume
POST /api/v1/tasks/{id}/complete
POST /api/v1/tasks/{id}/approve
POST /api/v1/tasks/{id}/return
POST /api/v1/tasks/{id}/cancel
POST /api/v1/tasks/{id}/clone
PATCH /api/v1/tasks/{id}
```

## Checklist actions

```text
PATCH /api/v1/tasks/{taskId}/items/{itemId}
POST  /api/v1/tasks/{taskId}/items/{itemId}/attachments
DELETE /api/v1/tasks/{taskId}/items/{itemId}/attachments/{attachmentId}
```

Business rules:

- Employees update tasks assigned to them or tasks assigned to their department when policy permits.
- Supervisors manage tasks for authorized departments.
- Managers manage all tenant tasks.
- A task cannot complete while required items are incomplete.
- An item requiring evidence cannot complete without evidence.
- Approval-required tasks become `PendingApproval` after employee completion.
- Non-approval tasks become `Completed`.
- Approval and return are Supervisor/Manager actions.
- Returned tasks can resume work.
- Completed/approved tasks cannot be silently edited.
- Every status change writes TaskStatusHistory and AuditLog.
- Overdue is derived consistently or maintained by a documented scheduled process; avoid contradictory status logic.
- Clone creates a new task, never copies old evidence, completion metadata, comments, or status.

Tests:

- Full state transition matrix.
- Required evidence.
- Approval flow.
- Unauthorized employee actions.
- Department assignment.
- Clone behavior.
- Calendar filters.
- Cross-tenant and soft-deleted resource behavior.

---

# Batch 6 — Task schedule management and occurrence generation

Implement schedule endpoints:

```text
GET    /api/v1/task-schedules
POST   /api/v1/task-schedules
GET    /api/v1/task-schedules/{id}
PATCH  /api/v1/task-schedules/{id}
DELETE /api/v1/task-schedules/{id}
POST   /api/v1/task-schedules/{id}/activate
POST   /api/v1/task-schedules/{id}/deactivate
POST   /api/v1/task-schedules/{id}/generate
```

Implement `ITaskOccurrenceGeneratorService` in Service.

Supported MVP recurrence:

- Daily every N days.
- Weekly every N weeks on selected weekdays.
- Monthly every N months on a selected day.
- Start and optional end date.
- Local start and due times interpreted in branch/organization timezone.
- Do not implement full RRULE parsing yet.

Implement a hosted background service in API that invokes the Service generator.

Rules:

- Default generation horizon: 30 days, configurable.
- Unique index makes generation idempotent.
- Generator uses UnitOfWork only.
- Generator copies template snapshots into each task.
- Deactivating a schedule stops new generation but does not delete created tasks.
- Editing a schedule affects future generation. Do not change completed or started tasks.
- Define and document policy for not-started generated tasks after schedule edits. Prefer conservative behavior: existing generated tasks remain unless an explicit future-regeneration action is requested.
- Timezone and daylight-saving behavior must be tested/documented.

Tests:

- Daily/weekly/monthly occurrence calculations.
- End date.
- Idempotency.
- Concurrent generation.
- Timezone boundary.
- Duplicate constraint.
- Deactivation.
- Template snapshot copy.

Update architecture documentation with the schedule flow.

---

# Batch 7 — Department order template APIs

Implement:

```text
GET    /api/v1/order-templates
POST   /api/v1/order-templates
GET    /api/v1/order-templates/{id}
PATCH  /api/v1/order-templates/{id}
DELETE /api/v1/order-templates/{id}
POST   /api/v1/order-templates/{id}/clone
POST   /api/v1/order-templates/{id}/activate
POST   /api/v1/order-templates/{id}/deactivate
```

Item operations:

```text
POST   /api/v1/order-templates/{id}/items
PATCH  /api/v1/order-templates/{id}/items/{itemId}
DELETE /api/v1/order-templates/{id}/items/{itemId}
POST   /api/v1/order-templates/{id}/items/reorder
```

Rules:

- Source and target departments belong to the same organization and branch.
- Source and target differ.
- UnitCode Custom requires CustomUnitLabel.
- Quantities are non-negative.
- Template edits never change historical orders.
- Employees can read active templates available to their source department.
- Managers manage templates; supervisors may manage templates for authorized departments only if the authorization matrix enables it.

Tests:

- Department validation.
- Custom unit validation.
- Clone.
- Tenant isolation.
- Historical snapshot protection.

---

# Batch 8 — Department order workflow APIs

Implement creation:

```text
POST /api/v1/department-orders
POST /api/v1/order-templates/{templateId}/create-order
```

Creation request supports:

- Selecting a subset of template items.
- Requested quantities.
- Optional custom items only when template permits.
- Required time.
- Priority.
- General and item notes.

Copy item snapshots.

Queries:

```text
GET /api/v1/department-orders
GET /api/v1/department-orders/{id}
GET /api/v1/department-orders/incoming
GET /api/v1/department-orders/outgoing
```

Filters:

- Date range.
- Status.
- Source department.
- Target department.
- Assignee.
- Priority.
- Late/overdue.

Actions:

```text
POST /api/v1/department-orders/{id}/view
POST /api/v1/department-orders/{id}/accept
POST /api/v1/department-orders/{id}/assign
POST /api/v1/department-orders/{id}/start
PATCH /api/v1/department-orders/{orderId}/items/{itemId}
POST /api/v1/department-orders/{id}/mark-ready
POST /api/v1/department-orders/{id}/deliver
POST /api/v1/department-orders/{id}/confirm-receipt
POST /api/v1/department-orders/{id}/reject
POST /api/v1/department-orders/{id}/cancel
POST /api/v1/department-orders/{id}/attachments
```

Rules:

- Source department creates and confirms receipt.
- Target department accepts, prepares, marks ready, and delivers.
- Managers can supervise all tenant orders.
- Supervisors operate only within authorized departments.
- Employees act only for their departments and assignments.
- Marking ready requires all non-unavailable items to be ready and a documented policy for unavailable items.
- Fulfilled and received quantities are non-negative.
- Received quantity cannot exceed delivered/fulfilled quantity unless a documented exception workflow exists. For MVP, reject it.
- Receipt requires delivery.
- Rejection requires a reason.
- Every transition writes history and audit records.
- Generate an organization-scoped human-readable order number safely without race conditions.
- Creating a linked task is optional; do not create one automatically unless the request explicitly asks for it.

Tests:

- Complete happy path.
- Partial/unavailable item flow.
- Unauthorized source/target actions.
- Quantity rules.
- Delivery before receipt.
- Concurrency on order numbers and status.
- Cross-tenant access.
- Snapshot preservation.

---

# Batch 9 — Complaint APIs

Implement:

```text
GET    /api/v1/complaints
POST   /api/v1/complaints
GET    /api/v1/complaints/{id}
PATCH  /api/v1/complaints/{id}
POST   /api/v1/complaints/{id}/assign
POST   /api/v1/complaints/{id}/start-review
POST   /api/v1/complaints/{id}/request-information
POST   /api/v1/complaints/{id}/respond
POST   /api/v1/complaints/{id}/close
POST   /api/v1/complaints/{id}/messages
POST   /api/v1/complaints/{id}/attachments
```

Rules:

- Employees can submit complaints.
- Visibility is enforced in every query.
- Management-only complaints are never listed to unauthorized staff.
- Submitter can see their own complaint when visibility permits.
- Internal messages are visible only to authorized management users.
- Assignment targets an authorized Manager/Supervisor.
- Closing records the actor and time through history/audit strategy.
- Generate a tenant-scoped complaint number safely.
- Do not add complaint categories.

Tests:

- Visibility matrix.
- Internal message filtering.
- Assignment.
- Status flow.
- Cross-tenant access.
- Unauthorized listing does not leak existence.

---

# Batch 10 — Manual subscription and platform administration APIs

Create a separate platform route area, authentication policy, and controller group.

## Platform authentication

Implement separate platform login/refresh/logout/me endpoints or a clearly separated claim/policy model:

```text
POST /api/v1/platform/auth/login
POST /api/v1/platform/auth/refresh
POST /api/v1/platform/auth/logout
GET  /api/v1/platform/auth/me
```

Do not allow organization users to call platform endpoints.

## Plans

```text
GET    /api/v1/platform/subscription-plans
POST   /api/v1/platform/subscription-plans
PATCH  /api/v1/platform/subscription-plans/{id}
POST   /api/v1/platform/subscription-plans/{id}/activate
POST   /api/v1/platform/subscription-plans/{id}/deactivate
```

## Organizations and subscriptions

```text
GET  /api/v1/platform/organizations
GET  /api/v1/platform/organizations/{id}
GET  /api/v1/platform/organizations/{id}/subscription
POST /api/v1/platform/organizations/{id}/subscription/activate
POST /api/v1/platform/organizations/{id}/subscription/extend
POST /api/v1/platform/organizations/{id}/subscription/change-plan
POST /api/v1/platform/organizations/{id}/subscription/suspend
POST /api/v1/platform/organizations/{id}/subscription/reactivate
POST /api/v1/platform/organizations/{id}/subscription/expire
```

## Manual payments

```text
GET  /api/v1/platform/manual-payments
POST /api/v1/platform/manual-payments
GET  /api/v1/platform/manual-payments/{id}
POST /api/v1/platform/manual-payments/{id}/confirm
POST /api/v1/platform/manual-payments/{id}/reject
POST /api/v1/platform/manual-payments/{id}/refund
```

Business rules:

- All changes write SubscriptionHistory and PlatformAuditLog.
- Payment and activation can be one atomic use case when explicitly requested, but records remain distinct.
- Trial, active, grace-period, expired, suspended, cancelled, and complimentary states have validated transitions.
- A scheduled Service process updates expired trials/subscriptions and grace periods.
- Organization data is never deleted due to expiration.
- Enforce plan feature and user/branch limits.
- Define a centralized `ISubscriptionAccessService` returning access mode such as:
  - Full.
  - GraceLimited.
  - ReadOnly.
  - Blocked.
- Write operations in tenant Services must call this access policy consistently.
- Authentication and read endpoints needed for renewal remain available in read-only state.

Tests:

- Transition matrix.
- Trial expiration.
- Grace period.
- Read-only enforcement across tasks, orders, complaints, members, and branches.
- Payment recording.
- Platform role permissions.
- Tenant users cannot access platform routes.
- Platform audit completeness.

---

# Batch 11 — In-app notifications and audit integration

Implement notifications for important events:

- Task assigned.
- Task due soon.
- Task overdue.
- Task returned.
- Order received.
- Order accepted.
- Order ready.
- Order delivered.
- Complaint assigned or responded.
- Subscription expiring.
- Subscription expired.

Endpoints:

```text
GET  /api/v1/notifications
POST /api/v1/notifications/{id}/read
POST /api/v1/notifications/read-all
GET  /api/v1/notifications/unread-count
```

Rules:

- Notifications are user-scoped.
- Store the final localized system text plus structured parameters.
- User-created titles remain unchanged.
- Use `PreferredLanguage`, falling back to organization default.
- Do not create translation tables.
- Notification failure should not corrupt the main transaction; define whether outbox-like reliability is needed. For MVP, use a simple documented post-commit strategy or a lightweight outbox only if justified.
- Audit important mutations, not every read.

Tests:

- Language fallback.
- User scoping.
- Unread count.
- No cross-user access.
- Main operation remains consistent if notification delivery fails.

---

# Batch 12 — Basic report services and APIs

Reports must query only through UnitOfWork/Repository abstractions. Do not query DbContext from Service.

Implement paginated/detail reports and summary DTOs.

## Task reports

```text
GET /api/v1/reports/tasks/summary
GET /api/v1/reports/tasks/by-department
GET /api/v1/reports/tasks/by-assignee
GET /api/v1/reports/tasks/overdue
```

Metrics:

- Total tasks.
- Completed/approved.
- In progress.
- Overdue.
- Cancelled.
- Completion rate.
- On-time completion rate.
- Average completion duration where data is available.

Filters:

- Date range.
- Branch.
- Department.
- Assignee.

## Department order reports

```text
GET /api/v1/reports/department-orders/summary
GET /api/v1/reports/department-orders/by-route
GET /api/v1/reports/department-orders/top-items
GET /api/v1/reports/department-orders/late
```

Metrics:

- Total orders.
- Completed/received.
- Rejected/cancelled.
- Late orders.
- Average acceptance time.
- Average preparation time.
- Average delivery-to-receipt time.
- Most requested item snapshots.

## Complaint reports

```text
GET /api/v1/reports/complaints/summary
```

Metrics:

- Total.
- Open.
- Closed.
- Average time to first review.
- Average time to close.

Respect complaint visibility and role authorization.

## Platform reports

```text
GET /api/v1/platform/reports/subscriptions/summary
GET /api/v1/platform/reports/payments/summary
```

Metrics:

- Trialing, active, grace, expired, suspended.
- Trials expiring soon.
- Subscriptions expiring soon.
- Confirmed manual payments by period and currency.

Technical requirements:

- Avoid loading entire tables into memory.
- Add repository projection/aggregate capabilities without leaking IQueryable.
- Add indexes if report query plans reveal a need.
- Define date boundaries and timezone behavior clearly.
- Return zero-safe rates.
- Add tests with known datasets and expected metrics.

Update documentation with metric definitions.

---

# Batch 13 — OpenAPI, API examples, and final test coverage

1. Add XML comments or endpoint descriptions where useful.
2. Add OpenAPI schemas and JWT security.
3. Add examples for key requests/responses.
4. Confirm all endpoints return DTOs.
5. Add integration tests for:
   - Manager flow.
   - Supervisor flow.
   - Employee task flow.
   - Department order flow.
   - Complaint privacy.
   - Platform subscription flow.
   - Expired read-only tenant.
   - Cross-tenant attempts.
6. Add concurrency tests for schedule generation and order numbering.
7. Add validation tests for important DTOs.
8. Confirm cancellation tokens propagate.

Run:

- Formatting.
- Restore.
- Build.
- Unit tests.
- PostgreSQL integration tests.
- API integration tests.
- Fresh migration application.
- OpenAPI generation.

Search the repository and prove:

- No DbContext injection in Service or API.
- No IQueryable exposed outside Repository.
- No entity returned directly by controllers.
- No shift/category/tag/translation entity was added.

---

# Batch 14 — Documentation and completion report

Complete or update:

- `README.md`.
- `docs/architecture.md`.
- `docs/data-model.md`.
- `docs/api-conventions.md`.
- `docs/authorization-matrix.md`.
- `docs/setup.md`.
- `docs/testing.md`.
- `docs/reports.md`.
- `docs/task-workflows.md`.
- `docs/department-order-workflows.md`.
- `docs/subscription-workflows.md`.
- Relevant ADRs.

Include:

- Authentication flow.
- Refresh-token rotation.
- Tenant resolution.
- Role matrix.
- Task state diagram.
- Order state diagram.
- Subscription state diagram.
- Scheduler operation.
- Report formulas.
- Environment variables.
- Seeded development users.
- Curl or HTTP examples.
- Migration commands.
- Test commands.

Final response must report:

1. Implemented endpoints grouped by module.
2. Business rules implemented.
3. Authorization policies.
4. Background jobs.
5. Reports.
6. Migrations.
7. Commands and test results.
8. Known limitations.
9. Exact next step: execute `prompts/03-frontend-project.md`.

## Intentionally excluded

- Inventory.
- Attendance/shifts.
- Categories/tags.
- Content translation.
- Online payment provider.
- WebSockets/chat.
- AI.
