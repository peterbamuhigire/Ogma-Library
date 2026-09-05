# Phase 13 Progress - Bibliographic Provider Gateway

Date: 2026-09-05

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
- Verified the current official Open Library and Google Books/API guidance for
  low-volume use, caching, application identification, quota, attribution,
  linking, result integrity, location restrictions, licensing, and privacy;
  see `evidence/phase-13-provider-terms-2026-09-04.md`.
- Added the desktop book-detail provider attribution/link path for Google Books
  and Open Library. URLs are generated from normalized ISBNs on fixed HTTPS
  hosts, revalidated before launch, and exposed with localized accessible
  labels; see `evidence/phase-13-attribution-links-2026-09-05.md`.
- Corrected runtime composition so deterministic metadata enrichment consumes the
  durable provider gateway cache before provider adapters, while retaining lookup
  provenance and audit persistence in the aggregator. Evidence:
  `evidence/phase-13-gateway-runtime-wiring-2026-09-05.md`.
- Carried the gateway's `IsStale` state through a versioned database migration,
  bounded book-detail projection, and localized enrichment-tab freshness rows.
  SQLite-safe bounded ordering, migration rollback, and detail-consumer tests
  pass; see `evidence/phase-13-stale-label-2026-09-05.md`.
- Performed a read-only live Open Library probe: the metadata endpoint returned
  HTTP 200 without redirect, while the tested cover URL redirected to an
  Archive.org host and was correctly rejected by the exact provider allowlist.
  See `evidence/phase-13-live-provider-probe-2026-09-05.md`.

## Remaining phase gate

The official-source documentation, local attribution/link subgate, and local
stale-label subgate are complete. Written legal/privacy owner review, archived
release evidence, live cover-provider acceptance (including the observed
cross-host redirect), and physical release/UI acceptance remain before phase
13 closure. Quota and circuit state are observable in the provider health
contract; the live metadata endpoint reachability probe is recorded but does
not close cover acquisition.
