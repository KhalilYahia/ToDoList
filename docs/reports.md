# Reports

All tenant report dates are UTC `[from, to)`. Rates are percentages rounded to two decimals and return zero when the denominator is zero. Durations are minutes and return `null` when no qualifying records exist.

## Tasks

- `DistributionCount` counts distinct non-null task distributions.
- `TaskExecutionCount`/`Total` counts employee-owned task copies.
- `PendingExecutionCount` is execution copies minus Completed and Cancelled copies.
- Completion rate: `Completed execution copies / Total execution copies`.
- On-time completion: completion timestamp at or before due time.
- Average completion duration: `CompletedAt - StartedAt`.
- Overdue: due before now and not Completed or Cancelled.
- Department and assignee breakdowns are paginated.

A distribution assigned to five employees is one distribution and five executions. If two employees complete, completion is 2/5; it is never reported as one completed distribution. Distribution-level completion is not inferred unless every non-cancelled copy is complete.

## Department orders

- Completed means Received.
- Late means required time passed before a terminal delivery/receipt/rejection/cancellation state.
- Acceptance: `AcceptedAt - RequestedAt`.
- Preparation: `ReadyAt - AcceptedAt`.
- Receipt handoff: `ReceivedAt - DeliveredAt`.
- Route and top-item projections are grouped after the Repository returns only filtered report columns; entities and `IQueryable` never leave Repository.

## Complaints

- Open is every non-Closed complaint.
- First-review duration: `ReviewedAt - CreatedAt`.
- Close duration: `ClosedAt - CreatedAt`.
- Tenant complaint reports are Manager-only in the MVP, preventing supervisor visibility aggregation from leaking management-only cases.

## Platform

Subscription summary counts trial, active/complimentary, grace, expired, and suspended subscriptions plus 30-day expirations. Payment summary includes only confirmed payments within `[from, to)`, grouped by currency; currencies are never converted.
