# ADR-0004: Buy the virtual waiting room instead of building it

- **Status:** Accepted
- **Date:** 2026-11-04

## Context

High-demand on-sales need a virtual waiting room that holds visitors before the sale and admits them at the rate checkout can process. Options:

| Option | Notes |
|---|---|
| Build it (Redis sorted set + signed tokens + edge check) | Full control. SeatGeek and Ticketmaster built their own, but queueing is their core business and they have the teams to run it. |
| Queue-it | Specialised SaaS, connectors for most CDNs and backends, priced per event or subscription. |
| Cloudflare Waiting Room | Good if Cloudflare is already the CDN, because it requires it in front of the site. |
| Cloud provider solution | Azure has no native waiting room; AWS retired its reference solution in 2025. |

## Decision

Use a SaaS waiting room (Queue-it as the default candidate), enabled only for on-sales flagged as high demand. The platform only has to validate the signed queue token at the gateway.

## Consequences

- No queueing infrastructure to build, test and operate for an event that happens a few times a month.
- A per-event or subscription cost, and a dependency on a vendor for the busiest moments.
- The token check at the gateway keeps the integration thin, so the vendor can be replaced.
- The waiting room does not replace capacity planning: the platform still pre-scales and limits tickets per buyer.
