# ADR 0005: Task schedule and history semantics

## Status

Accepted

## Context

Task schedules exposed both structured recurrence fields and a free-form recurrence rule, while weekly values had ambiguous public/private naming. Task status history also reused its persistence creation timestamp as the workflow event timestamp. Those overlaps made the domain contract unclear and could erase the distinction between when an event happened and when it was stored.

## Decision

- Recurrence is limited to the structured `Daily`, `Weekly`, and `Monthly` variants. The free-form rule field is removed.
- A schedule owns a normalized `_weekdays` array and exposes it through the read-only `Weekdays` collection. EF Core persists the backing field to the existing `weekdays smallint[]` column.
- `TaskStatusHistory.OccurredAt` is required and immutable. It represents the workflow event time; inherited `CreatedAt` remains the persistence audit time.
- History indexes begin with `organization_id` and include `task_id` and `occurred_at` so common tenant-scoped timelines are supported.
- Existing history is preserved by backfilling `occurred_at` from `created_at` during migration.

## Consequences

The domain, API, frontend enum contract, and database all express the same recurrence capabilities. Weekly schedules are deterministic, and delayed persistence no longer changes event chronology. Supporting a richer recurrence language later requires a new explicit domain and storage design rather than reviving an unvalidated string field.
