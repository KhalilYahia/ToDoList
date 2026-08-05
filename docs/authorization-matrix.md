# Authorization matrix

Organization and platform identities are separate JWT claim models. Hidden frontend navigation is never considered authorization.

| Capability | Manager | Supervisor | Employee | Platform support | Platform administrator |
|---|---:|---:|---:|---:|---:|
| Read tenant organization/branches/departments | Yes | Scoped | Scoped | No | No |
| Update tenant organization and departments | Yes | No | No | No | No |
| List/add/update/delete organization branches through the platform API | No | No | No | No | Yes |
| Read members | Yes | Scoped | No | No | No |
| Create/change/suspend members or roles | Yes | No | No | No | No |
| Manage task/order templates and schedules | Yes | Read/use | Read/use active | No | No |
| Create task distributions | Yes | Scoped departments | No | No | No |
| View task copies | All tenant copies | Authorized-department copies | Own copies only | No | No |
| Execute task/checklist/evidence | Own copy only | Own copy only | Own copy only | No | No |
| Reassign/cancel manageable task copies | Yes | Scoped departments | Cancel own only | No | No |
| Approve/return tasks | Yes | Scoped | No | No | No |
| Create outgoing department orders | Yes | Scoped source | Member source | No | No |
| Prepare/deliver incoming orders | Yes | Scoped target | Member target/assignment | No | No |
| Confirm order receipt | Yes | Scoped source | Member source | No | No |
| Submit complaints | Yes | Yes | Yes | No | No |
| View management-only/internal complaint data | Yes | Scoped | No | No | No |
| Tenant reports | Yes | No in MVP | No | No | No |
| Read platform organizations/plans/payments/reports | No | No | No | Yes | Yes |
| Mutate plans/subscriptions/payments | No | No | No | No | Yes |

“Scoped” means the Service validates organization, active membership, branch/department ownership, assignment, and resource visibility. Managers span their tenant only. Supervisors span departments they supervise or actively belong to. Employees cannot elevate their role or assign themselves broader scope.

An employee task lookup, workflow action, checklist mutation, or evidence mutation for another employee's copy returns 404, including when both employees share a department. Managers can view all copies in their tenant. Supervisors can view and manage copies only in departments they supervise or actively belong to. Cross-tenant resources are always hidden. Frontend controls mirror these rules but the Service remains authoritative.

Subscription access is centralized:

- `Full`: reads and writes.
- `GraceLimited`: current MVP reads and writes while renewal warnings are shown.
- `ReadOnly`: authentication and reads continue; tenant mutations return 402.
- `Blocked`: tenant access is rejected.

Feature keys additionally gate tasks, department orders, complaints, and reports.
