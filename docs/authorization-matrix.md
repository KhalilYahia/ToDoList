# Authorization matrix

Authentication and feature policies are deferred to Prompt 02. The model reserves separate organization and platform roles; no endpoint should rely only on hidden frontend navigation.

| Capability | Manager | Supervisor | Employee | Platform administrator |
|---|---:|---:|---:|---:|
| Manage organization settings and memberships | Yes | No | No | Explicit support workflow only |
| Manage templates and schedules | Yes | Scoped | No | No |
| Work assigned tasks | Yes | Yes | Yes | No |
| Approve tasks | Yes | Scoped | No | No |
| Create and process department orders | Yes | Scoped | Scoped | No |
| View participant complaints | Yes | Scoped | Own/allowed | No |
| View management-only complaints | Yes | Scoped policy | No | Explicit support workflow only |
| Manage plans, payments, and subscriptions | No | No | No | Yes |

“Scoped” means the Service must validate organization, branch, department, membership, assignment, and resource visibility. Cross-tenant resources return 404. Expired organizations become read-only after grace while retaining read access; organization account status and individual user status remain independent.
