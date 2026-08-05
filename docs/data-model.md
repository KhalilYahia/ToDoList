# Data model

The PostgreSQL schema uses `snake_case` table and column names, UUID primary keys, `timestamptz` moments, string enum values, `numeric(18,2)` money, and `numeric(18,3)` quantities. Most operational aggregates have audit timestamps; historical operational roots use soft deletion.

## Organization and identity

- `organizations` own branches, departments, memberships, operational data, subscriptions, notifications, and tenant audit logs.
- `branches` belong to an organization. A filtered unique index allows at most one active, non-deleted primary branch per organization.
- `departments` belong to a branch and organization; supervisor assignment is optional.
- `users` are global login identities with a filtered unique normalized email and an explicit `must_change_password` invitation flag. Password hashes are never API DTOs.
- `organization_members` connect users to organizations with Manager, Supervisor, or Employee roles and are unique per organization/user.
- `user_departments` connect users to tenant departments; an active user/department relationship is unique.
- `refresh_tokens` store only token hashes, exactly one organization/platform owner, an organization binding for tenant tokens, token-family IDs, revocation reasons, and replacement links. The owner check constraint prevents mixed platform/tenant sessions.

The MVP deliberately uses email as the required login identifier. Phone is profile data, not a unique login, until a later decision changes this.

## Tasks

`task_templates` own reusable checklist definitions. They do not store a branch or a default assignee, and `default_department_id` is nullable. Retired checklist definitions are marked inactive so historical task snapshot references remain valid. Schedules reference templates and define validated daily, weekly, or monthly recurrence data. Weekly values are normalized by the domain into a distinct, sorted, read-only `Weekdays` collection and persisted from its `_weekdays` backing field to PostgreSQL `smallint[]`. Recurrence is represented only by validated structured fields.

`TaskDistribution` records why a set of task copies was created: tenant, branch, department, source template/schedule, assignment mode, occurrence, timing, and creator. It has no workflow status, checklist, evidence, or executable owner. `OperationalTask`, mapped to `tasks`, is the executable root and has exactly one effective employee owner for all new work. Multi-assignment creates one task row per resolved employee under one distribution; it is not a task-to-user join.

```text
TaskDistribution
|
+-- OperationalTask -- User A
|   +-- TaskItem A1
|   `-- TaskItem A2
|
+-- OperationalTask -- User B
|   +-- TaskItem B1
|   `-- TaskItem B2
|
`-- OperationalTask -- User C
    +-- TaskItem C1
    `-- TaskItem C2
```

Each task is an execution snapshot with its own copied title, description, priority, approval requirement, schedule times, `task_items`, attachments, status history, assignment history, and optimistic-concurrency token. One employee's progress, evidence, reassignment, approval, or cancellation cannot mutate a sibling copy.

Assignment mode is stored as a string enum:

- `SingleUser`: exactly one selected eligible employee.
- `SelectedUsers`: two or more distinct eligible employees.
- `AllDepartmentMembers`: no stored selection; resolve every currently eligible active employee in the department.

`task_schedule_assignees` stores fixed assignees for SingleUser and SelectedUsers schedules. AllDepartmentMembers schedules intentionally store no assignee rows and resolve the current active employee population on every generation run. A filtered unique distribution index prevents a second distribution for the same schedule occurrence. Filtered task indexes enforce one copy per distribution/assignee and one scheduled occurrence per assignee.

Task attachments store URLs and metadata only. Entity guards enforce due-after-start, recurrence rules, explicit state transitions, checklist completion, and required-evidence completion metadata. `OperationalTask`, `TaskItem`, and `TaskSchedule` map a `Version` property to PostgreSQL's implicit `xmin` system column for optimistic concurrency.

`task_status_history.occurred_at` records the immutable business-event time and is indexed with organization and task identifiers. The inherited `created_at` remains a separate persistence audit timestamp. The refinement migration backfills existing event times from their prior creation timestamps without deleting history.

Migration `20260727012341_AddIndependentMultiUserTaskAssignment` preserves every assigned legacy task by creating a one-copy SingleUser distribution with the same ID as the task. Legacy unassigned task rows remain unassigned with a nullable distribution solely as historical administrative records; the new creation path cannot create such a row. Existing schedules inherit their explicit assignee, or their former template default, as a fixed SingleUser schedule. Schedules with neither become AllDepartmentMembers. No arbitrary employee is assigned during migration.

## Department orders

Order templates define selectable items between different departments in one branch. Template items are retired with `is_active` instead of deleted. Actual `department_orders` and `department_order_items` store item name, description, unit, and quantity snapshots; they do not track stock. The schema enforces different source/target departments and non-negative quantities. Order number is unique per organization. Delivery and receipt have separate actors/timestamps, and Domain guards prevent receipt before delivery.

Indexes support target department/status, source department/creation time, and aggregate child lookup.

## Complaints

Complaints are numbered uniquely per organization and carry management-only or participant visibility. Messages record the internal flag, and attachments reference either a complaint or optional message. Service projections exclude internal messages and inaccessible complaint roots before DTO mapping.

## Subscriptions and platform data

- `subscription_plans` store feature maps as JSONB without placing JSON dependencies in Domain.
- `organization_subscriptions` store trial, active period, grace, suspension, and cancellation state at organization level.
- `subscription_history` and `manual_payments` preserve administrative changes and payment periods. The history actor is nullable only for the scheduled lifecycle processor.
- `platform_users` are separate from organization users.
- Notifications use JSONB parameters; tenant and platform audit logs use JSONB snapshots.

Constraints validate subscription/trial/payment periods, non-negative prices/payments, and plan limits. Common indexes cover organization/status and expiration.

## Tenant ownership and deletion

Tenant-owned child rows carry `organization_id` even when a parent relationship could infer it. This enables uniform global filtering and tenant-aware indexes. Foreign keys use restrictive deletion for historical safety. Default repository deletion is allowed only for `ISoftDeletable`; hard deletion requires explicit Repository infrastructure code.
