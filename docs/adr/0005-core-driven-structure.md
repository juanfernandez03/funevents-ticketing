# ADR-0005: Core-Driven structure for the prototype

- **Status:** Accepted
- **Date:** 2026-11-04

## Context

The prototype is small, will be reviewed and modified live, and must be easy to read. Typical .NET templates add layers (four-project Clean Architecture, MediatR, repositories over EF Core) that this size does not need.

## Decision

Follow the Core-Driven approach described by Iván Abad (NetMentor):

- **Entry point** (`Endpoints/`): Minimal API endpoints only route, bind and translate results into HTTP. No business logic.
- **Use cases** (`UseCases/`): one class per action (`ReserveTickets`, `GetReservation`, `GetEvent`) with a single `ExecuteAsync` method. Reads and writes are naturally separated. Use cases are concrete classes registered in DI; there is no mediator and no interface per use case.
- **External elements** (`Data/`): the EF Core `DbContext`, entities, configuration, migrations and seed data. The use cases inject the `DbContext` directly, since it already is a unit of work and a repository on top would only forward calls.
- Use cases return `Result<T>`; the endpoint maps errors to ProblemDetails in a single place.
- Use cases return DTOs from the `FunEvents.Contracts` project, never entities. The console client reuses the same contracts.

## Consequences

- Each feature lives in a handful of files; a change such as "cancel a reservation" touches one use case, one endpoint and maybe one entity.
- The core logic is tested end to end against a real PostgreSQL, because its correctness depends on the database (transactions, row locks, unique index). An in-memory provider would hide exactly what matters.
- If the module grows, the folders can become projects without changing the dependency direction.
