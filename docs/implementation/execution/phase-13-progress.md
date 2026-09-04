# Phase 13 Progress - Bibliographic Provider Gateway

Date: 2026-09-04

## Delivered in this increment

- Added `IMetadataProviderGateway` and a durable provider cache keyed by
  normalized ISBN/title/author request data.
- Added positive and negative TTL behavior, provider contract versioning and a
  256 KiB response retention ceiling.
- Normalized provider request payloads before external calls.
- Isolated provider failures and avoided persisting exception text as provider
  document responses.
- Added tests proving repeated normalized lookups use one provider call and
  failures create bounded negative cache entries.
- Expired positive cache entries now remain available as explicitly marked
  stale results when a provider refresh fails, preserving local catalogue
  usefulness without presenting stale data as fresh.
- Added regression coverage for stale fallback and refresh isolation.
- Added durable provider validators and conditional revalidation. Built-in
  providers send `If-None-Match`; a `304 Not Modified` refreshes TTL without
  replacing the cached representation.
- Added focused conditional-cache regression coverage.
- Added bounded provider health state with per-minute request accounting,
  rejection counts, consecutive-failure tracking, and a short circuit-open
  window exposed through `IMetadataProviderHealth` snapshots.
- Gateway calls now reserve quota, classify zero-confidence responses as
  failures, and preserve stale cache behavior when the circuit blocks refresh.

The shared rate-limited HTTP handler now reports retry counts into the same
provider health snapshot. Successful multi-provider aggregations now produce a
field-level conflict report and a privacy-safe `ProviderConflict` audit event;
candidate values remain available to review consumers but are excluded from the
durable audit payload.
- Provider backoff and retry telemetry is verified through the handler and
  gateway health contracts, including retry counts and provider failure state.
- Closed the code-level privacy-disclosure subgate: recorded provider requests
  contain only bibliographic lookup keys, use GET without a request body, and
  exclude notes/content; normalized durable caching prevents repeated
  disclosures for the same lookup.

## Remaining phase gate

Live-provider terms/privacy review and physical/network evidence remain before
phase 13 closure. Quota and circuit state are observable in the local health
contract; stale-cache status is present in the provider result contract for the
UI consumer.
