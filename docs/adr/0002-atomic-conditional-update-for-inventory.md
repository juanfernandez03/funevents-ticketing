# ADR-0002: Prevent overselling with an atomic conditional update

- **Status:** Accepted
- **Date:** 2026-11-04

## Context

Many buyers can try to take the last tickets of an event at the same time. Options considered:

| Option | Problem |
|---|---|
| Read availability, check in code, then write | Race condition: two requests read "1 left" and both sell it. |
| Optimistic concurrency (row version) with retries | Correct, but under heavy contention most requests fail and retry, wasting work. |
| Pessimistic `SELECT ... FOR UPDATE` | Correct, but needs two round trips while holding the lock. |
| Distributed lock (Redis) | Another moving part, and the database still has to be correct. |
| **Single conditional `UPDATE`** | None of the above. |

## Decision

Decrement the counter with one statement that only succeeds when enough tickets remain:

```sql
UPDATE events
SET available_tickets = available_tickets - @quantity
WHERE id = @event_id AND available_tickets >= @quantity;
```

If it affects zero rows, the reservation is rejected with `409 Conflict`. The update and the reservation insert run in the same transaction. A `CHECK (available_tickets >= 0)` constraint guards against any future code path that forgets the condition.

In EF Core this is `ExecuteUpdateAsync` with a `Where` filter, wrapped in the configured execution strategy.

## Consequences

- PostgreSQL serialises the competing updates on the row lock and re-evaluates the `WHERE` clause after acquiring it, so the counter can never go negative.
- One round trip for the check-and-write, no retry loop.
- Use EF Core's [`ExecuteInTransactionAsync` / `verifySucceeded` pattern](https://learn.microsoft.com/ef/core/miscellaneous/connection-resiliency#option-3---add-state-verification) with a reservation UUIDv7 and timestamp generated once per request: after an ambiguous commit, check that reservation id before retrying, even without an idempotency key.
- A single row per event becomes a hot spot for extreme on-sales. The mitigation (inventory pools per section or channel) keeps the same statement.
- An integration test must fire many concurrent reservations at an event with few tickets and assert that exactly the available number succeed.
