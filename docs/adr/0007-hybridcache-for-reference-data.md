# ADR-0007: Cache immutable lookups with HybridCache (in-memory now, Redis later)

- **Status:** Accepted
- **Date:** 2026-11-04

## Context

Every reservation translates two public codes into internal ids: the event code and the user code. During an on-sale, thousands of requests repeat the same event lookup within seconds, which is two extra queries per reservation on the busiest table.

Not everything can be cached:

| Data | Cacheable? | Why |
|---|---|---|
| Event code → event id | Yes | Immutable once the event exists |
| User code → user id | Yes | Immutable once the user exists |
| Available tickets | **No** | The reservation decision must always read the primary database, inside the transaction |
| Reservations | No | Written once, read rarely, must reflect the latest state |

## Decision

- Use **HybridCache** (`Microsoft.Extensions.Caching.Hybrid`, generally available since .NET 9) for the two lookups in `ReserveTickets`, with a 10-minute expiration.
- In the prototype it runs **in-memory only** (L1, per process).
- **In production, add Redis as the second level (L2)** by registering a distributed cache (Aspire: `AddRedis` in the AppHost, `AddRedisDistributedCache` in the API). HybridCache picks it up automatically, so the use case code does not change.
- **Never cache a miss.** If the lookup returns nothing, the entry is removed right away; otherwise an event created a minute later would stay "not found" until the entry expires.
- `GET /events/{code}` is not cached in the prototype, because it returns availability and the console client shows it right after reserving.

## Why HybridCache and not `IMemoryCache` or Redis directly

- **Stampede protection:** when an on-sale starts and the key is cold, only one request runs the query and the rest wait for its result. `IMemoryCache` runs the query once per concurrent request.
- **One API for both levels:** in-memory now, Redis later, same `GetOrCreateAsync` call.
- **Redis alone** would add a container and a network hop to the prototype for data that is immutable; each instance can safely keep its own copy.

## When Redis (L2) becomes worth it

- Several API instances behind the gateway and data that **changes**: event pages, seat maps, catalog listings. Then a shared cache and tag-based invalidation (`RemoveByTagAsync("event:ROCK-BCN-2027")`) keep every instance consistent and cut cold-start load after deployments.
- For the immutable lookups of this ADR, L1 alone is enough even with many instances.

## Consequences

- Two fewer queries per reservation on hot events.
- Each lookup factory owns an async DI scope and its database context, so shared cache work does not depend on the initiating request's lifetime; the reservation transaction still uses the request context.
- Codes are treated as immutable. Renaming an event code would need an explicit cache removal.
- With warnings as errors, check whether the build flags any HybridCache API as experimental (`EXTEXP` diagnostics) and suppress it explicitly where used.
- A test must prove that a miss is not cached: reserve for an unknown event (404), create it, reserve again (201).
