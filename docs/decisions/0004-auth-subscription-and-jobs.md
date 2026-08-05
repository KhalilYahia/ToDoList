# ADR 0004: Authentication, subscription access, and hosted jobs

Status: Accepted

## Decision

Use short-lived JWT bearer access tokens and rotating opaque refresh tokens in Secure HttpOnly cookies. Store only refresh-token hashes, tenant/platform ownership, family IDs, revocation data, and replacement links.

Keep organization and platform claims/policies separate. Resolve tenant context only from validated claims or explicit internal scopes.

Centralize subscription decisions in `ISubscriptionAccessService` and require tenant mutation Services to call it.

Run schedule generation, subscription lifecycle processing, and due/expiration notification sweeps as API hosted services that invoke scoped Service workflows. Database constraints provide schedule idempotency.

## Consequences

- Token-family reuse can invalidate a stolen session family.
- Organization users cannot satisfy platform policies and vice versa.
- Read-only expiration behavior is consistent across feature Services.
- Hosted jobs are suitable for a single-instance MVP. Multi-instance production should add distributed job coordination and a notification outbox.
- Refresh-cookie clients must use HTTPS and credentialed CORS in production.
