# Prompt 03 — Create the Next.js Frontend and Implement the MVP UI

Use the repository skill `$ops-manager-project`.

## Objective

Create the frontend application for OpsManager using Next.js App Router, React, and TypeScript.

The frontend must consume the implemented backend OpenAPI contract and provide role-aware interfaces for:

1. Authentication and onboarding.
2. Organization, branches, departments, and members.
3. Task templates, task schedules, task instances, checklists, evidence, approvals, and calendar.
4. Department order templates and department order workflows.
5. Complaints.
6. Basic reports.
7. Subscription status for tenant managers.
8. Separate platform-administration pages for plans, subscriptions, payments, and platform reports.
9. Arabic, English, and Russian system UI.

User-created content is never automatically translated.

## Required frontend stack

- Next.js App Router.
- React.
- TypeScript strict mode.
- Tailwind CSS.
- TanStack Query for server state.
- React Hook Form and Zod.
- `next-intl` for localization unless the repository already uses an equivalent.
- A small accessible component foundation. Use an established component library only if it reduces code and is documented; do not import a large dependency set unnecessarily.
- Vitest and React Testing Library for unit/component tests if starting from an empty frontend.
- Playwright for critical end-to-end flows when the environment supports it.

Use the existing package manager. Prefer pnpm for a new project if installed.

## Mandatory rules

- Work as a senior frontend engineer.
- Inspect the backend OpenAPI contract before designing forms or types.
- Do not invent request fields or endpoint behavior that conflicts with the API.
- Keep server state in TanStack Query.
- Do not store access tokens in localStorage.
- Use feature-based folders.
- Keep pages and components reasonably small.
- Render role-based navigation, while relying on backend authorization as the source of truth.
- Support loading, empty, error, forbidden, offline/retry, and subscription read-only states.
- Arabic uses RTL; English and Russian use LTR.
- Do not add shifts, categories, tags, inventory, automatic content translation, or online payment checkout.
- Add tests and documentation in every batch.

---

# Batch 0 — Inspect backend contracts and create a frontend plan

Before scaffolding:

1. Read the repository skill, AGENTS.md, backend docs, and authorization matrix.
2. Run the backend tests if feasible.
3. Start or inspect the backend OpenAPI document.
4. List implemented endpoints, DTOs, enums, pagination format, Problem Details format, auth flow, refresh behavior, and subscription access states.
5. Identify any frontend-blocking API gaps. Do not silently work around broken contracts.
6. Produce a route map and component/query plan.
7. Add `docs/frontend-architecture.md` with the initial plan.

Acceptance criteria:

- The frontend plan maps directly to real API endpoints.
- Role and subscription-state behavior is documented.
- No UI implementation starts before API contract inspection.

---

# Batch 1 — Scaffold the Next.js application and tooling

Create:

```text
frontend/
  ops-manager-web/
```

Use Next.js App Router and TypeScript strict mode.

Configure:

- Tailwind CSS.
- ESLint.
- Prettier if it does not conflict with repository tooling.
- Type checking.
- Unit/component testing.
- Environment validation with Zod or an equivalent.
- `.env.example`.
- Absolute import aliases.
- CI-friendly scripts.

Recommended scripts:

- `dev`
- `build`
- `start`
- `lint`
- `typecheck`
- `test`
- `test:watch`
- `test:e2e` when Playwright is available
- `format`
- `format:check`

Create a feature-based structure such as:

```text
src/
  app/
    [locale]/
      (auth)/
      (tenant)/
      (platform)/
  components/
    ui/
    layout/
  features/
    auth/
    organization/
    members/
    departments/
    task-templates/
    task-schedules/
    tasks/
    department-orders/
    order-templates/
    complaints/
    reports/
    subscriptions/
    platform-admin/
    notifications/
  lib/
    api/
    auth/
    i18n/
    permissions/
    query/
    validation/
    utils/
  messages/
    ar.json
    en.json
    ru.json
  types/
```

Do not create empty feature placeholders without immediate use.

Validation:

- Install dependencies.
- Lint.
- Typecheck.
- Run tests.
- Production build.

Update setup documentation.

---

# Batch 2 — API client, generated types, errors, and query infrastructure

Create a typed API layer based on OpenAPI.

Preferred approaches:

1. Generate types using `openapi-typescript` and write a thin fetch client.
2. Use another small OpenAPI client generator only if it integrates cleanly.

Requirements:

- Do not manually duplicate every backend DTO when generation is practical.
- Centralize base URL and environment settings.
- Add an authenticated request wrapper.
- Parse RFC 7807 Problem Details.
- Handle validation errors and field errors.
- Add request cancellation through AbortSignal.
- Add one automatic refresh attempt after an expired access token.
- Avoid infinite refresh loops.
- Keep access tokens in memory; rely on secure refresh-token cookies according to backend design.
- Send credentials when required.
- Never place tokens in URL, logs, localStorage, or query cache.
- Add query-key factories by feature.
- Configure TanStack Query defaults:
  - Reasonable stale time.
  - Limited retries for server errors.
  - No retries for validation/authorization failures.
  - Central mutation error handling where appropriate.
- Add paginated response helpers.

Create tests for:

- Problem Details parsing.
- Refresh behavior.
- Retry prevention.
- Query keys.
- Environment validation.

Document how OpenAPI types are regenerated.

---

# Batch 3 — Localization, RTL, design system, and application shell

Implement system UI localization for:

- Arabic.
- English.
- Russian.

Requirements:

- Locale-prefixed routes or another documented Next.js-compatible strategy.
- Arabic `dir="rtl"`.
- English and Russian `dir="ltr"`.
- Correct locale-aware dates, numbers, and relative time.
- Language selector.
- User preference from `/auth/me`, falling back to organization default, then a safe application default.
- User-created text remains unchanged.
- System codes such as statuses, roles, priorities, recurrence types, and units are translated in message files.
- No translation database or automatic translation API.

Create a simple design system:

- Buttons.
- Inputs.
- Selects.
- Checkbox/radio controls.
- Textarea.
- Form field wrapper.
- Dialog.
- Drawer/sheet for mobile.
- Dropdown menu.
- Tabs.
- Badge/status chip.
- Table.
- Pagination.
- Skeleton.
- Empty state.
- Alert.
- Toast.
- Confirm dialog.
- Date/time inputs.
- Accessible file uploader.
- Error summary.

Create application shells:

- Public/auth shell.
- Tenant shell.
- Platform-admin shell.
- Responsive side navigation and mobile navigation.
- Header with organization, current role, locale, notifications, and user menu.
- Read-only subscription banner.
- Trial/expiration warning banner for managers.

Accessibility:

- Keyboard navigation.
- Visible focus.
- Form labels.
- Dialog focus management.
- Appropriate ARIA only where native semantics are insufficient.
- Color is not the only status indicator.

Tests:

- RTL direction.
- Locale switching.
- Navigation accessibility.
- Component interactions.

---

# Batch 4 — Authentication, onboarding, and session UX

Implement pages:

```text
/[locale]/login
/[locale]/register
/[locale]/forgot-password or invitation setup only if backend supports it
```

Implement:

- Organization registration form.
- Login.
- Logout.
- Session bootstrap through `/auth/me`.
- Refresh handling.
- Protected route behavior.
- Platform login separation.
- Role-aware redirect.
- Suspended user/organization screens.
- Expired/read-only organization handling.
- Session-expired dialog.

Registration form fields must match backend:

- Organization name.
- Legal name optional.
- Timezone.
- Default language.
- Manager name.
- Email.
- Password.
- Phone optional.

Security:

- No token in localStorage.
- Do not expose sensitive API error details.
- Password managers and browser autocomplete should work.
- Use correct input types and autocomplete attributes.
- Prevent duplicate submissions.

Tests:

- Login success/failure.
- Registration validation.
- Session bootstrap.
- Refresh failure.
- Role redirect.
- Read-only access metadata.

---

# Batch 5 — Tenant dashboard and primary navigation

Create the tenant dashboard:

```text
/[locale]/dashboard
```

Show role-appropriate summary cards using report APIs:

- Today's tasks.
- Overdue tasks.
- Tasks pending approval.
- Incoming department orders.
- Orders ready for receipt.
- Open complaints for authorized users.
- Trial/subscription status for managers.

Add compact lists:

- My next tasks.
- Incoming orders.
- Recent activity or notifications.
- Items requiring attention.

Requirements:

- Do not calculate official metrics from partial client-side lists.
- Use report/summary endpoints.
- Provide clear empty states.
- Link cards to filtered pages.
- Use role and permission helpers.
- Show read-only restrictions without hiding historical data.

Create route-level error and loading states.

---

# Batch 6 — Organization, branches, departments, and members UI

## Organization settings

Route:

```text
/[locale]/settings/organization
```

Manager-only editing:

- Name.
- Legal name.
- Contact.
- Timezone.
- Default UI language.
- Logo if backend file upload is available.

## Branches

Routes:

```text
/[locale]/settings/branches
/[locale]/settings/branches/[id]
```

Features:

- List.
- Create.
- Edit.
- Activate/deactivate or soft delete.
- Primary branch indicator.
- Subscription branch-limit feedback.

## Departments

Routes:

```text
/[locale]/settings/departments
/[locale]/settings/departments/[id]
```

Features:

- Branch filter.
- Create/edit.
- Assign supervisor.
- Active state.
- Member count if API supplies it.

## Members

Routes:

```text
/[locale]/settings/members
/[locale]/settings/members/[id]
```

Features:

- Paginated list.
- Search and filters.
- Add/invite member according to API workflow.
- Role assignment.
- Department assignment.
- Activate/suspend.
- Preferred UI language.
- Protect the last manager in UI while backend remains authoritative.
- Display user/branch limits.

Permissions:

- Manager has management screens.
- Supervisor gets only authorized read views.
- Employee does not see management navigation.

Tests:

- Role visibility.
- Limit errors.
- Form validation.
- Pagination/filter URL state.
- Cross-role forbidden response handling.

---

# Batch 7 — Task template UI

Routes:

```text
/[locale]/task-templates
/[locale]/task-templates/new
/[locale]/task-templates/[id]
/[locale]/task-templates/[id]/edit
```

Features:

- Paginated/searchable list.
- Active/inactive filter.
- Department and branch filter.
- Create/edit form.
- Reorder checklist items.
- Required/optional item.
- Evidence mode.
- Instructional attachment upload.
- Default department, optional assignee, priority, duration, approval.
- Clone.
- Activate/deactivate.
- Delete with confirmation.
- “Use template” action that opens task creation.

Do not include categories, tags, or shifts.

UX:

- Keep form progressive and simple.
- Allow adding checklist items without modal overload.
- Preserve unsaved form state when a recoverable upload fails.
- Show that template changes do not modify existing tasks.

Tests:

- Item add/remove/reorder.
- Evidence selection.
- Validation.
- Clone and use-template navigation.
- Permission handling.

---

# Batch 8 — Task schedule and calendar UI

## Schedule routes

```text
/[locale]/task-schedules
/[locale]/task-schedules/new
/[locale]/task-schedules/[id]/edit
```

Schedule form supports only backend MVP recurrence:

- Daily every N days.
- Weekly every N weeks on selected weekdays.
- Monthly every N months on one day.
- Start date.
- Optional end date.
- Start and due times.
- Branch.
- Department.
- Optional assignee.
- Template.
- Active state.

Show a human-readable recurrence summary in the selected UI language.

## Calendar

Route:

```text
/[locale]/calendar
```

Provide:

- Month view.
- Week/list view for mobile.
- Date navigation.
- Filters by branch, department, assignee, status, priority.
- Task-instance cards.
- Open task detail.
- Create task with selected date prefilled.

Important:

- Calendar reads task instances, not schedules.
- Do not show future schedule rules as completed task instances unless generated by backend.
- Use locale-aware dates and RTL-safe layout.
- Drag-and-drop rescheduling is optional and should not be added unless backend semantics are explicit.

Tests:

- Recurrence form rules.
- Human-readable summary.
- Calendar filtering.
- Date navigation.
- Locale and RTL behavior.

---

# Batch 9 — Task list, details, execution, evidence, and approval UI

Routes:

```text
/[locale]/tasks
/[locale]/tasks/new
/[locale]/tasks/[id]
/[locale]/my-tasks
```

Task list:

- Pagination.
- Date range.
- Status.
- Branch.
- Department.
- Assignee.
- Priority.
- Overdue.
- Saved filters are not required.

Task creation:

- One-off task.
- Create from template.
- Assignment to department or user.
- Schedule and due time.
- Priority.
- Approval requirement.
- Review copied checklist before submit if API supports it.

Task detail:

- Header with status, priority, schedule, department, assignee.
- Checklist.
- Evidence attachments.
- Timeline/status history.
- Action buttons based on backend permissions and current state.
- Blocked reason.
- Approval/return panel.
- Read-only historical mode.

Employee workflow:

- Start.
- Complete checklist items.
- Add notes/evidence.
- Block/resume.
- Complete task.

Supervisor/manager workflow:

- Assign/reassign.
- Approve.
- Return with reason.
- Cancel.
- Clone.

Rules:

- Required evidence is clearly indicated.
- Client validation helps, but backend errors remain authoritative.
- Do not allow editing completed/approved tasks except actions explicitly allowed by API.
- Optimistic updates should be used only when rollback is safe.

Tests:

- Task transition UI.
- Required evidence.
- Approval.
- Return reason.
- Read-only state.
- Unauthorized action response.
- Mobile checklist usability.

---

# Batch 10 — Department order template UI

Routes:

```text
/[locale]/order-templates
/[locale]/order-templates/new
/[locale]/order-templates/[id]
/[locale]/order-templates/[id]/edit
```

Features:

- Source and target departments.
- Name and description.
- Allow custom items.
- Approval setting if implemented.
- Item name.
- Description.
- Unit code.
- Custom unit.
- Default/minimum quantity.
- Optional image.
- Item reorder.
- Clone.
- Activate/deactivate.
- “Create order” action.

System units are translated; custom units remain as entered.

Do not display inventory fields.

Tests:

- Source/target validation.
- Custom unit.
- Item reorder.
- Permission handling.

---

# Batch 11 — Department order workflow UI

Routes:

```text
/[locale]/department-orders
/[locale]/department-orders/new
/[locale]/department-orders/[id]
/[locale]/department-orders/incoming
/[locale]/department-orders/outgoing
```

Order creation from template:

- Show selectable items.
- Quantity controls.
- Optional custom items only when allowed.
- Priority.
- Required time.
- General note.
- Per-item notes.
- Review before send.

Incoming/outgoing views:

- Status.
- Source/target department.
- Required time.
- Late indicator.
- Assignee.
- Item count.

Order detail:

- Item-level requested, fulfilled, and received quantities.
- Item statuses.
- Status timeline.
- Attachments.
- Notes.
- Action buttons based on source/target role.

Actions:

- View/acknowledge.
- Accept.
- Assign.
- Start.
- Update item preparation.
- Mark unavailable.
- Mark ready.
- Deliver.
- Confirm receipt.
- Reject with reason.
- Cancel.

UX:

- Clearly distinguish “ready,” “delivered,” and “received.”
- Partial readiness must be understandable.
- Use confirmation for delivery and receipt.
- Preserve item snapshots.
- Do not imply stock availability.

Tests:

- Create order.
- Custom item rules.
- Target workflow.
- Source receipt confirmation.
- Quantity validation.
- Partial/unavailable item display.
- Unauthorized actions.

---

# Batch 12 — Complaint UI

Routes:

```text
/[locale]/complaints
/[locale]/complaints/new
/[locale]/complaints/[id]
```

Features:

- Employee submission.
- Title and description.
- Optional target department.
- Visibility selection allowed by API.
- Attachment upload.
- Authorized complaint list.
- Status filters.
- Assignment for management.
- Message thread.
- Internal management notes clearly separated.
- Request information.
- Respond.
- Close.

Privacy requirements:

- Never render internal messages to unauthorized users.
- Do not infer hidden complaints from counts or errors.
- Show generic not-found behavior for inaccessible complaints.
- Avoid complaint categories.

Tests:

- Employee submission.
- Management-only privacy.
- Internal note filtering.
- Assignment and close flow.
- Forbidden/not-found behavior.

---

# Batch 13 — Reports UI

Routes:

```text
/[locale]/reports/tasks
/[locale]/reports/department-orders
/[locale]/reports/complaints
```

Use backend report endpoints.

## Task reports

Show:

- Total.
- Completed/approved.
- In progress.
- Overdue.
- Completion rate.
- On-time rate.
- Average duration.
- Department and assignee breakdowns.
- Paginated overdue table.

## Order reports

Show:

- Total.
- Received/completed.
- Rejected/cancelled.
- Late.
- Average acceptance/preparation/receipt durations.
- Top requested items.
- Source-to-target route breakdown.

## Complaint reports

Show authorized summary metrics and status breakdown.

Filters:

- Date range.
- Branch.
- Department.
- Assignee where applicable.

Visualization rules:

- Prefer clear cards, tables, and lightweight charts.
- Do not add a heavy chart library unless needed.
- Charts must have accessible text/table equivalents.
- Handle zero values and missing data honestly.
- Use backend metric definitions; do not recalculate with partial data.

Add export only if backend implements it. Do not fake CSV/PDF export from incomplete client data.

Tests:

- Filters.
- Empty data.
- Zero-safe rates.
- Accessibility of charts/tables.
- Role restrictions.

---

# Batch 14 — Subscription pages for tenant managers

Routes:

```text
/[locale]/settings/subscription
```

Show:

- Current plan.
- Trial, active, grace, expired, suspended, or complimentary status.
- Start/end/trial/grace dates.
- User and branch usage versus limits.
- Enabled features.
- Manual renewal instructions from configuration.
- Payment history if tenant API exposes safe records.

MVP has no online checkout.

Behavior:

- Trial warning.
- Expiring-soon warning.
- Grace-limited banner.
- Read-only banner and disabled mutation explanations.
- Employees should not see financial/platform details.
- Manager can still access subscription information when tenant is read-only.

Do not let frontend state alone enforce read-only mode; handle backend conflicts consistently.

---

# Batch 15 — Platform administration frontend

Create a separate platform shell and routes:

```text
/[locale]/platform/login
/[locale]/platform
/[locale]/platform/organizations
/[locale]/platform/organizations/[id]
/[locale]/platform/plans
/[locale]/platform/payments
/[locale]/platform/reports
```

Features:

## Dashboard

- Active/trial/grace/expired/suspended counts.
- Trials expiring soon.
- Subscriptions expiring soon.
- Payment summaries by currency.

## Organizations

- Search and filters.
- Organization details.
- Subscription state.
- Plan.
- User/branch usage.
- Activate.
- Extend.
- Change plan.
- Suspend/reactivate.
- Expire.
- View history and platform audit entries if API supports them.

## Plans

- Create/edit.
- Limits.
- Features.
- Grace period.
- Activate/deactivate.

## Manual payments

- Record.
- Confirm.
- Reject.
- Refund.
- Receipt metadata.
- Link to subscription/organization.

Security:

- Platform session and tenant session are clearly separated.
- Tenant users cannot navigate to or call platform pages.
- Destructive subscription actions require confirmation and reason where backend requires it.
- Never expose password or token data.

Tests:

- Platform-role navigation.
- Tenant user denial.
- Subscription actions.
- Payment forms.
- Confirmation dialogs.
- Platform reports.

---

# Batch 16 — Notifications and user preferences

Implement:

- Notification menu.
- Notification page if needed.
- Unread count.
- Mark one/all read.
- Links to related tasks/orders/complaints.
- Locale-aware system text.
- User-created titles unchanged.

User preferences:

- Preferred UI language.
- Profile information allowed by API.
- Password change only if backend implements it.
- Logout sessions if backend supports it.

Do not add push notifications unless backend and deployment requirements are explicitly available.

---

# Batch 17 — Responsive behavior, accessibility, performance, and polish

Perform a focused quality pass.

## Responsive behavior

Validate:

- Mobile navigation.
- Tables with mobile alternatives.
- Calendar on small screens.
- Task checklist actions.
- Department-order quantities.
- Complaint message view.
- Dialogs and drawers.
- RTL layouts.

## Accessibility

Validate:

- Keyboard-only use.
- Focus order.
- Form errors.
- Dialog focus trap and return.
- Accessible names.
- Contrast.
- Reduced motion.
- Screen-reader announcements for async success/errors.
- Charts have text equivalents.

## Performance

- Avoid unnecessary client components.
- Use Server Components where they provide real value without breaking auth/query behavior.
- Lazy load heavy screens.
- Avoid duplicate API requests.
- Paginate large lists.
- Optimize images.
- Keep bundle dependencies justified.
- Use query invalidation precisely.

Run Lighthouse or another available audit and document meaningful results without claiming perfection.

---

# Batch 18 — Testing and end-to-end flows

Unit/component tests:

- Forms.
- Validation.
- Status/action mapping.
- Permission helpers.
- Problem Details rendering.
- Locale/RTL behavior.
- Subscription access UI.

End-to-end tests when supported:

1. Register organization and enter trial.
2. Manager creates department and member.
3. Manager creates task template and schedule.
4. Employee completes a task with evidence.
5. Supervisor approves it.
6. Source department creates order.
7. Target prepares/delivers.
8. Source confirms receipt.
9. Employee submits complaint.
10. Manager responds/closes.
11. Platform admin records payment and activates/extends subscription.
12. Expired organization displays read-only state.

Use isolated test data and documented cleanup.

Run:

- Install.
- Lint.
- Typecheck.
- Unit tests.
- E2E tests.
- Production build.

Fix all introduced errors and warnings.

---

# Batch 19 — Documentation and completion report

Create/update:

- Frontend section in root `README.md`.
- `docs/frontend-architecture.md`.
- `docs/frontend-routing.md`.
- `docs/frontend-authentication.md`.
- `docs/frontend-i18n.md`.
- `docs/frontend-testing.md`.
- `docs/ui-permissions.md`.
- `.env.example`.
- OpenAPI type-generation instructions.

Document:

- Folder structure.
- Environment variables.
- Local startup with backend.
- Authentication and refresh behavior.
- Route protection.
- Role navigation.
- Subscription read-only mode.
- Localization and RTL.
- How to add a translation key.
- Why user-created content is not translated.
- Query/cache conventions.
- Form conventions.
- Testing commands.

Final response must include:

1. Created frontend tree.
2. Implemented routes.
3. API integration method.
4. Authentication/session strategy.
5. Localization and RTL behavior.
6. Role and subscription behavior.
7. Tests and build results.
8. Accessibility/performance findings.
9. Known limitations and recommended next work.

## Intentionally excluded

- Inventory.
- Attendance/shifts.
- Categories/tags.
- User-content translation.
- Online checkout.
- AI.
- Chat.
- Push notifications unless separately requested.
