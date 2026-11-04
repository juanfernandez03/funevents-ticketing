# ADR-0008: CQRS at use-case level, without a separate read model

- **Status:** Accepted
- **Date:** 2026-11-04

## Context

Ticketing is read-heavy: people browse events and check availability far more often than they reserve. That makes CQRS (Command Query Responsibility Segregation) a natural candidate. It comes in very different sizes:

| Option | What it means | Fit for this assessment |
|---|---|---|
| **Full CQRS** | Separate read store (search index, denormalised tables) fed by events from the write side; possibly event sourcing | Over-engineering: needs a message bus, projections and eventual consistency for a brief that asks for a single reservation flow |
| **CQRS at use-case level** | Commands and queries are separate classes over the same database; queries project straight to DTOs | Small, clear, and leaves the door open |
| No separation | One service class with reads and writes mixed | Harder to read, test and evolve |

There is also a domain reason to be careful: a read model is eventually consistent, but **the decision to sell a ticket must be strongly consistent**. Splitting stores in the prototype would add risk exactly where the guarantee lives.

## Decision

- **Commands** change state: `ReserveTickets`. It runs in a transaction against the primary database.
- **Queries** only read: `GetEvent`, `GetReservation`. They use `AsNoTracking()` and project directly into the response DTOs, so no entity is loaded or tracked.
- Same database, same `DbContext`. No MediatR, no command/query bus, no separate read store, no event sourcing.

This separation comes for free from the Core-Driven structure (one class per action, see [ADR-0005](0005-core-driven-structure.md)).

## How it would evolve in a real system

The full architecture ([§6](../architecture.md#6-scalability-and-resilience)) already anticipates the next step:

1. The write side publishes events (`ReservationCreated`, `OrderConfirmed`) through the outbox.
2. A **read model** for catalog, search and availability display is built from those events: Redis, a denormalised table or a search index such as Elasticsearch, which Ticketmaster uses for seat-level data.
3. Availability shown to buyers may be a few seconds stale; **the reservation always checks the primary database**.
4. Commands keep the same code: only the queries move to the new read model.

## Consequences

- Readers can tell at a glance whether a class changes state.
- Queries are cheap and do not touch the change tracker.
- No eventual consistency in the prototype, so the "never oversell" guarantee stays simple to reason about and to test.
