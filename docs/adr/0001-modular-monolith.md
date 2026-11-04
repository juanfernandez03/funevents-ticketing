# ADR-0001: Start as a modular monolith

- **Status:** Accepted
- **Date:** 2026-11-04

## Context

FunEvents needs a ticketing platform that serves several channels and survives on-sale spikes. The natural reflex is microservices "because of scale". The real scaling problems of ticketing, though, are:

1. The surge of visitors in the first minutes of an on-sale.
2. Write contention on the inventory of a popular event.

Neither is solved by splitting the code into services. The first is solved at the edge (waiting room, CDN, pre-scaling); the second in the data model (conditional updates, inventory pools).

## Decision

Build one deployable, split internally into modules (Catalog, Inventory & Reservations, Orders & Payments, Customers & Partners). Each module owns its tables and exposes an explicit interface; modules do not read each other's tables. The application is stateless and runs as several autoscaled instances.

## Consequences

- One pipeline, one deployment, local transactions inside a module, simple debugging.
- Horizontal scaling works from day one. What is lost is scaling a single module independently.
- Module boundaries must be enforced by convention and review, since the compiler does not prevent shortcuts.
- *Inventory & Reservations* is the planned first extraction if it ever needs independent scaling or releases.
