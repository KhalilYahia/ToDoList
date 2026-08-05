# Frontend architecture

## Scope and contract source

The OpsManager web client is a Next.js App Router application in
`frontend/ops-manager-web`. Its source of truth is the backend OpenAPI 3.1
document exposed at `http://localhost:5291/openapi/v1.json`, not handwritten
copies of backend DTOs.

The contract inspection completed before scaffolding found 117 paths and 121
schemas. Backend tests passed with 29 tests successful and 7 PostgreSQL/Docker
checks skipped because Docker was unavailable.

Generated API types are checked in under `src/lib/api/schema.ts`. The fetch
wrapper adds the in-memory bearer token, includes refresh cookies, understands
RFC 7807 responses, forwards `AbortSignal`, and makes at most one refresh
attempt. On reload, the auth provider explicitly restores the session through
the API-issued HttpOnly refresh cookie before loading current-user data.

## Backend contract summary

All business endpoints are under `/api/v1`. Paginated responses have
`items`, `page`, `pageSize`, and `totalCount`. List filters use query
parameters. Errors are RFC 7807 Problem Details with a stable `code`, a
`traceId`, and optional validation errors.

Tenant authentication uses:

- `POST /auth/register-organization`
- `POST /auth/login`
- `POST /auth/refresh`
- `POST /auth/logout`
- `GET /auth/me`

Tenant login requires `organizationId`, `email`, and `password.`
Registration returns an authenticated session. Access tokens stay in memory.
The rotating refresh token is an HttpOnly, SameSite Strict cookie scoped to
`/api/v1`.

Platform authentication is separate at `/platform/auth/*` and returns platform
claims. The current backend uses the same refresh-cookie name and path for
tenant and platform sessions, so the browser cannot retain both refresh
sessions at once. Switching realms explicitly clears the in-memory session and
the UI treats the two shells as mutually exclusive.

The implemented resource families are:

| Family | Contract |
|---|---|
| Organization | `/organization`, `/branches`, `/departments`, `/members` |
| Tasks | `/task-templates`, `/task-schedules`, `/tasks`, `/tasks/calendar`, `/tasks/my`, workflow actions and evidence uploads |
| Orders | `/order-templates`, `/department-orders`, incoming/outgoing lists, item updates, workflow actions and uploads |
| Complaints | `/complaints`, messages, assignment, workflow actions and uploads |
| Reports | Task, order, and complaint summary/breakdown endpoints under `/reports` |
| Subscription operations | Tenant access metadata in `/auth/me`; platform plan, organization subscription, payment, and report endpoints |
| Notifications | Paginated notifications, unread count, mark-one-read, and mark-all-read |

Implemented string enums mirrored in localized UI messages are:

- organization status: `Active`, `Suspended`, `Archived`
- organization role: `Manager`, `Supervisor`, `Employee`
- subscription access: `Full`, `GraceLimited`, `ReadOnly`, `Blocked`
- task priority: `Low`, `Normal`, `High`, `Urgent`
- task status: `NotStarted`, `InProgress`, `Blocked`, `PendingApproval`,
  `Returned`, `Completed`, `Cancelled`
- task item status: `Pending`, `Completed`, `Skipped`
- evidence mode: `None`, `Optional`, `Required`
- recurrence: `Daily`, `Weekly`, `Monthly`
- task assignment mode: `SingleUser`, `SelectedUsers`, `AllDepartmentMembers`
- order status: `Draft`, `Submitted`, `Accepted`, `Preparing`, `Ready`,
  `Delivered`, `Received`, `Rejected`, `Cancelled`
- order item status: `Pending`, `Preparing`, `Ready`, `Fulfilled`,
  `PartiallyFulfilled`, `Rejected`
- units: `Each`, `Kilogram`, `Gram`, `Liter`, `Milliliter`, `Meter`,
  `Centimeter`, `Box`, `Package`, `Custom`
- complaint status: `Submitted`, `UnderReview`, `InProgress`, `Resolved`,
  `Closed`, `Rejected`
- complaint visibility: `ManagementOnly`, `Participants`
- subscription status: `Trial`, `Active`, `GracePeriod`, `Expired`,
  `Suspended`, `Cancelled`, `Complimentary`
- billing mode: `Trial`, `Monthly`, `Yearly`, `Manual`
- payment method: `Cash`, `BankTransfer`, `CardTerminal`, `Other`
- payment status: `Pending`, `Confirmed`, `Rejected`, `Refunded`
- platform role: `Administrator`, `Support`

## Route map

Routes are locale-prefixed with `ar`, `en`, or `ru`.

| Route | API/query plan | Access |
|---|---|---|
| `/[locale]/login`, `/register` | Tenant auth mutations | Public |
| `/[locale]/dashboard` | Task/order/complaint reports, my tasks, incoming orders, notifications | Tenant |
| `/[locale]/settings/organization` | Organization query/update | Manager |
| `/[locale]/settings/branches[/[id]]` | Tenant branch list and read-only detail | Tenant read |
| `/[locale]/settings/departments[/new\|/[id]]` | Branch-aware department create/update/delete, supervisor selection, assigned-member list, and membership assignment | Manager writes; scoped read-only detail for other authorized roles |
| `/[locale]/settings/members[/new\|/[id]]` | Member create/update, organization role selection, multi-department assignment, activate, and suspend | Manager writes; Supervisor read-only |
| `/[locale]/task-templates[/new\|/[id]\|/[id]/edit]` | Template CRUD, clone, state, item and create-task actions | Tenant, mutations by policy |
| `/[locale]/task-schedules[/new\|/[id]/edit]` | Schedule CRUD, activation, generation | Tenant, mutations by policy |
| `/[locale]/calendar` | Task-instance calendar query | Tenant |
| `/[locale]/tasks`, `/tasks/new`, `/tasks/[id]`, `/my-tasks` | Task lists, detail, item/evidence and workflow actions | Tenant, scoped |
| `/[locale]/order-templates[/new\|/[id]\|/[id]/edit]` | Order-template CRUD, item and create-order actions | Tenant, mutations by policy |
| `/[locale]/department-orders`, `/new`, `/[id]`, `/incoming`, `/outgoing` | Order lists, detail, item and workflow actions | Tenant, scoped |
| `/[locale]/complaints`, `/new`, `/[id]` | Authorized complaints, messages and workflow actions | Tenant, privacy-scoped |
| `/[locale]/reports/tasks`, `/department-orders`, `/complaints` | Backend report endpoints only | Manager in MVP |
| `/[locale]/settings/subscription` | `/auth/me` access metadata | Manager |
| `/[locale]/notifications` | Notification list and read mutations | Tenant |
| `/[locale]/settings/profile` | Read-only current-user data and language switch | Tenant |
| `/[locale]/platform/login` | Platform auth mutation | Public platform realm |
| `/[locale]/platform` | Platform subscription/payment summaries | Platform |
| `/[locale]/platform/organizations[/[id]]` | Organization and subscription actions | Platform; writes administrator-only |
| `/[locale]/platform/organizations/[id]/branches[/new\|/[branchId]]` | Paginated branch list plus add, update, and delete operations | Platform administrator only |
| `/[locale]/platform/plans` | Plan list and mutations | Platform; writes administrator-only |
| `/[locale]/platform/payments` | Payments and status actions | Platform; writes administrator-only |
| `/[locale]/platform/reports` | Subscription and payment summaries | Platform |

## Component and query plan

The application has three shells:

1. Public/auth shell for registration and tenant/platform login.
2. Tenant shell with role-aware navigation, organization context,
   notifications, locale selector, and subscription banners.
3. Platform shell with its own session and navigation.

For managers, the tenant navigation groups Organization, Departments, and
Members inside an accessible, route-aware “Organization configs” collapsible
section on desktop and mobile.

Shared primitives cover fields, buttons, dialog/confirmation, badges, alerts,
tables, pagination, skeletons, empty/error states, toasts, tabs, file upload,
and responsive navigation. Feature pages compose those primitives and do not
own server-state caches.

Query keys are factories grouped by `auth`, `organization`, `tasks`,
`orders`, `complaints`, `reports`, `notifications`, and `platform`. Search,
filter, date, and page state is reflected in the URL where it is practical.
Mutations invalidate only the affected resource detail, list, summary, and
unread-count keys.

Forms use React Hook Form with Zod schemas that match the generated request
types. Browser validation improves feedback, but RFC 7807 responses remain
authoritative.

Task and schedule forms reuse an assignment selector. Selecting the department filters the member source to active Employee accounts. SingleUser renders one employee selector, SelectedUsers renders a searchable multi-select with removable chips and duplicate prevention, and AllDepartmentMembers explains that the backend resolves the current eligible population. A successful create mutation follows the first returned task while retaining the distribution response contract.

Task lists show each employee-owned execution as its own row, including assignee identity on management views. Task detail enables start/complete/checklist/evidence controls only for the current owner; management controls remain role- and department-scoped. These UI restrictions are supplementary to Service authorization.

## Authorization and subscription behavior

Navigation is a convenience, not an authorization boundary:

- Managers see organization administration, templates, schedules, reports,
  and subscription information. Department detail shows every assigned member
  and their organization-wide role; member forms assign department access with
  named checkboxes grouped by branch.
- Supervisors see operational pages and scoped template/schedule reads.
- Employees see their operational pages and complaint submission.
- Platform Support receives read-only platform pages.
- Platform Administrators receive platform mutations, including branch
  management from an organization detail page. Tenant branch screens remain
  read-only for organization managers.

The tenant shell consumes `access.mode` from `/auth/me`:

- `Full`: reads and writes.
- `GraceLimited`: reads and writes with a renewal warning.
- `ReadOnly`: historical reads stay visible; mutation controls are disabled
  with an explanation and backend `402` responses are handled globally.
- `Blocked`: show the suspended/access-blocked screen.

Feature and permission failures from the backend are still handled even when a
control was rendered optimistically.

## Localization and direction

`next-intl` loads system messages from `src/messages/{locale}.json`.
Arabic sets `dir="rtl"`; English and Russian set `dir="ltr"`. Dates, numbers,
relative time, enum labels, statuses, roles, priorities, recurrence terms, and
unit names are locale-aware. User-created titles, names, descriptions, notes,
and custom units are always rendered exactly as returned by the API.

Locale selection starts with the authenticated user's preferred language,
then organization default language, then English. A route locale selected by
the user is retained for the current navigation.

## Confirmed API gaps and UI boundaries

The frontend does not invent behavior for these gaps:

- No forgot-password, invitation acceptance, self-profile update, password
  change, or revoke-other-sessions endpoints. Those controls are omitted.
- Member creation uses a temporary password; there is no invitation workflow.
- Organization logo, plan item image, receipts, and similar fields accept URLs
  where defined. There is no organization-logo or order-image upload endpoint.
- Tenant subscription details expose only status/access/expiry metadata from
  `/auth/me`. Current plan, usage versus limits, feature list, renewal
  instructions, and tenant payment history are not exposed.
- Task and order DTOs expose milestone timestamps but no full history feed.
  Task/template/order/complaint uploads do not expose an attachment collection
  in detail DTOs, so existing filenames and delete identifiers cannot be
  listed after reload.
- Platform organization details do not expose user/branch usage, subscription
  history, or audit entries.
- Complaint reports expose summary values but no status-breakdown endpoint.
- There are no export endpoints, consolidated activity endpoint, or push
  notification contract.

Each affected page explains the unavailable data where useful and implements
only the supported subset. Closing these gaps requires a later backend batch,
not client-side reconstruction.
