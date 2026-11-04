# ADR-0006: .NET 10, Minimal APIs and Aspire

- **Status:** Accepted
- **Date:** 2026-11-04

## Context

The assessment asks for .NET 8 or later with ASP.NET Core Minimal APIs, and optionally PostgreSQL and Aspire.

## Decision

- **.NET 10 (LTS).** Supported until November 2028. .NET 8 and .NET 9 reach end of support in November 2026, and .NET 11 is a shorter-support release.
- **Minimal APIs** with the built-in validation added in .NET 10 (`AddValidation`), so DataAnnotations rules are applied before the handler runs and invalid input returns a standard `400` ProblemDetails.
- **Built-in OpenAPI document** (`AddOpenApi`) with Scalar as the interactive UI in development.
- **PostgreSQL** through EF Core and the Npgsql provider.
- **Aspire 13** to orchestrate PostgreSQL, the API and the console client locally, with OpenTelemetry and health checks from ServiceDefaults.

## Consequences

- `dotnet run --project src/FunEvents.AppHost` should start the whole system with only the .NET SDK and Docker installed.
- The Aspire dashboard shows traces from the console client through the API to PostgreSQL, which helps during the review.
- Integration tests do not depend on the AppHost: they start PostgreSQL with Testcontainers and host the API in memory with `WebApplicationFactory`.
