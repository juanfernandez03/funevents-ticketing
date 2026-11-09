# ADR-0003: Idempotency keys on reservation requests

- **Status:** Accepted
- **Date:** 2026-11-04

## Context

Reservations arrive over unreliable networks from browsers, box office apps and partner systems. When a response is lost, the caller retries. Without protection, a retry creates a second reservation and takes tickets twice. The console client will use the standard HTTP resilience pipeline, which retries on transient errors.

## Decision

`POST /events/{eventCode}/reservations` accepts an optional `Idempotency-Key` header. When sent, it must be one value of 1 to 64 visible ASCII characters with no comma (a repeated header arrives joined by commas); anything else is a `400`, never a silent "no key". The rule lives once in `FunEvents.Contracts` (`ApiHeaders.IsValidIdempotencyKey`) and both the API and the console client use it.

- The key is stored on the reservation itself, under a unique index. No extra table is needed.
- A repeated key with the **same** payload returns the original reservation with the original status code (`201`) and an `Idempotent-Replayed: true` header. Returning the first result, instead of an error, is what lets clients retry blindly.
- A repeated key with a **different** payload returns `422 Unprocessable Entity`, because it is a client bug.
- Two concurrent requests with the same key: one inserts, the other hits the unique index, its transaction rolls back (including the inventory decrement), and it replays the winner's result.

## Consequences

- Retries are safe for every client, including the resilience handler in the console client.
- Keys live as long as the reservation. In production, a TTL (24 hours to a few days) would bound the index; the prototype does not need it.
- Keys are unique globally because the prototype has no authenticated callers. Once partners authenticate, the key is scoped per client (`partner_id`, `key`), so two partners can never collide on the same key.
- The key is optional to keep the API easy to try. A production partner API would make it mandatory for writes.
