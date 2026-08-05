# ADR 0006: Independent task distribution

## Status

Accepted.

## Context

A shared executable task assigned through a many-to-many user relation would also share status, checklist progress, evidence, timestamps, approval state, and concurrency state. One employee could therefore complete or alter work belonging to every assignee. A single schedule-assignee column also cannot represent selected users or a dynamic department population.

## Decision

Every executable `OperationalTask` has one employee owner. Multi-assignment creates one independent task and checklist snapshot per eligible employee. `TaskDistribution` groups the creation event and source metadata but owns no executable workflow state.

Assignment modes are SingleUser, SelectedUsers, and AllDepartmentMembers. Fixed schedule assignees use `TaskScheduleAssignee`; AllDepartmentMembers stores no fixed users and resolves current active department employees for every occurrence-generation run. Creation of the distribution, copies, items, initial histories, notifications, and audit entry is transactional.

Employees can discover and mutate only their own copy. Managers can view/manage tenant copies, and Supervisors can view/manage copies in authorized departments, but execution actions always require ownership. Reassignment changes one copy and is historical; it never merges or duplicates sibling work.

## Consequences

- Checklist, evidence, workflow, approval, timestamps, and optimistic concurrency are isolated per employee.
- Reports distinguish distribution count from execution-copy count.
- Storage grows linearly with assignees, which is intentional and keeps aggregates simple and auditable.
- Scheduled idempotency requires unique distribution-occurrence and per-assignee-copy keys.
- Existing assigned tasks migrate to one-copy SingleUser distributions. Existing unassigned tasks remain nullable legacy administrative records instead of receiving an invented owner.
- Rolling back to the former schema cannot faithfully represent multi-user schedule configuration. The down migration restores an old schedule assignee only when exactly one fixed user exists and preserves task rows with a non-unique old occurrence index.
