# Data model

The PostgreSQL schema uses `snake_case` table and column names, UUID primary keys, `timestamptz` moments, string enum values, `numeric(18,2)` money, and `numeric(18,3)` quantities. Most operational aggregates have audit timestamps; historical operational roots use soft deletion.

## Organization and identity

- `organizations` own branches, departments, memberships, operational data, subscriptions, notifications, and tenant audit logs.
- `branches` belong to an organization. A filtered unique index allows at most one active, non-deleted primary branch per organization.
- `departments` belong to a branch and organization; supervisor assignment is optional.
- `users` are global login identities with a filtered unique normalized email. Password hashes are never API DTOs.
- `organization_members` connect users to organizations with Manager, Supervisor, or Employee roles and are unique per organization/user.
- `user_departments` connect users to tenant departments; an active user/department relationship is unique.
- `refresh_tokens` store only token hashes and support revocation and rotation.

The MVP deliberately uses email as the required login identifier. Phone is profile data, not a unique login, until a later decision changes this.

## Tasks

`task_templates` own reusable checklist definitions. Schedules reference templates and define daily, weekly, monthly, or reserved custom recurrence data. Weekly weekdays use PostgreSQL `integer[]`; arbitrary RRULE parsing is deferred.

`tasks` are execution snapshots with copied title, description, priority, approval requirement, schedule times, and related `task_items`. Historical task text therefore does not change when a template is edited. A filtered unique index on `(task_schedule_id, occurrence_date, scheduled_start_at)` prevents duplicate generated occurrences. Additional indexes cover organization/date, assignee/status, and department/status.

Task attachments store URLs and metadata only. Entity guards enforce due-after-start, recurrence rules, state transitions, and required-evidence completion metadata.

## Department orders

Order templates define selectable items between different departments in one branch. Actual `department_orders` and `department_order_items` store item name, description, unit, and quantity snapshots; they do not track stock. The schema enforces different source/target departments and non-negative quantities. Order number is unique per organization. Delivery and receipt have separate actors/timestamps, and Domain guards prevent receipt before delivery.

Indexes support target department/status, source department/creation time, and aggregate child lookup.

## Complaints

Complaints are numbered uniquely per organization and carry management-only or participant visibility. Messages record the internal flag, and attachments reference either a complaint or optional message. Prompt 02 must filter internal messages using Service authorization checks.

## Subscriptions and platform data

- `subscription_plans` store feature maps as JSONB without placing JSON dependencies in Domain.
- `organization_subscriptions` store trial, active period, grace, suspension, and cancellation state at organization level.
- `subscription_history` and `manual_payments` preserve administrative changes and payment periods.
- `platform_users` are separate from organization users.
- Notifications use JSONB parameters; tenant and platform audit logs use JSONB snapshots.

Constraints validate subscription/trial/payment periods, non-negative prices/payments, and plan limits. Common indexes cover organization/status and expiration.

## Tenant ownership and deletion

Tenant-owned child rows carry `organization_id` even when a parent relationship could infer it. This enables uniform global filtering and tenant-aware indexes. Foreign keys use restrictive deletion for historical safety. Default repository deletion is allowed only for `ISoftDeletable`; hard deletion requires explicit Repository infrastructure code.
