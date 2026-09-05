# Phase 13 Gateway Runtime Wiring Evidence

Date: 2026-09-05

## Finding and correction

The durable `MetadataProviderGateway` existed and had cache/TTL/revalidation
coverage, but the runtime metadata aggregator was still calling provider
adapters directly. That meant the interactive deterministic enrichment path did
not consume the gateway cache.

`MetadataProviderAggregator` now accepts the gateway as an optional runtime
dependency. Runtime composition uses the gateway result, then continues to
persist lookup provenance, conflict events, and review proposals through the
aggregator. The direct provider path remains available for isolated legacy/test
construction when no gateway is supplied. With external providers disabled, the
gateway has no providers and the flow remains empty/fail-closed.

## Verification

- `Phase13ProviderGatewayTests`: 6 passed, 0 failed, 0 skipped.
- The new `Aggregator_UsesGatewayCacheBeforeDirectProviders` proof verifies a
  gateway result is persisted as lookup provenance, the direct provider is not
  called, and the normal provider audit event is retained.
- Existing gateway tests continue to pass for normalized durable cache hits,
  negative cache entries, stale fallback, conditional ETag revalidation, and
  provider health/circuit accounting.
- Infrastructure Release build: 0 warnings, 0 errors.
- Full Release solution regression after the integration: 1,098 passed
  (902 core, 41 architecture, 155 UI), 0 failed, 0 skipped.

## Gate disposition

Closed locally: the runtime enrichment path now consumes the durable provider
gateway instead of bypassing its cache and resilience policy.

Still open: end-to-end stale-state presentation, legal/privacy owner review,
archived terms evidence, live provider/network evidence, and physical UI
acceptance.
