# Phase 13 Stale Cache Evidence

Date: 2026-09-04

- `MetadataProviderGateway` preserves expired positive cache responses as
  `ProviderMetadataResult.IsStale = true` when refresh fails.
- Fresh responses continue to replace the expired entry; failed providers do
  not leak exception text into the cached response payload.
- The stale result remains a local fallback and is distinguishable to UI/API
  consumers from a fresh provider result.

Automated proof: `Phase13ProviderGatewayTests` passed 3/3, including normalized
cache reuse, negative failure caching, and expired-cache stale fallback.

Remaining Phase 13 gates are conditional ETag requests, quota/circuit telemetry,
provider conflict aggregation, and privacy disclosure evidence.
