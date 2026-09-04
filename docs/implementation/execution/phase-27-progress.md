# Phase 27 Progress - AI Gateway, Privacy and Cost Runtime

Date: 2026-08-30

## Delivered in this increment

- Composed the provider-neutral `IAiGateway` in the desktop runtime through a
  single fail-closed registration path.
- Registered a disabled provider and offline-by-default privacy state, so AI
  remains unavailable until explicit runtime configuration and consent exist;
  catalogue, reader and search remain independent of AI availability.
- Added a fail-closed non-UI preview gate that cancels outbound calls rather
  than silently permitting egress.
- Enforced provider/request identity matching and required local completion
  provenance for LocalOllama requests.
- Added bounded AI request payload contracts for query text, metadata fields,
  content chunk count and content chunk size.
- Updated composition and architecture evidence to require the disabled gateway
  runtime rather than silently omitting gateway composition.
- Added active provider resilience: bounded per-attempt timeouts, one
  configurable transient retry, per-provider circuit opening, and observable
  retry/failure snapshots. The provider factory applies this decorator to all
  non-disabled providers.
- Added provider-specific egress allowlists for custom endpoints and rejected
  embedded endpoint credentials; Ollama custom endpoints remain loopback-only.
- Existing school AI key management is now recorded as delivered for this
  gate: provider keys are stored through the platform credential abstraction,
  returned only as configured/timestamp status, replaced through save, deleted
  explicitly, and cleared from mutable input buffers.
- Wired the interactive desktop composition to `AvaloniaPreviewGate`, so the
  exact payload-preview dialog is reached before cloud egress. Background and
  test composition remains fail-closed, and each modal preview disposes its
  localization subscription when closed.
- Added versioned, atomic JSON persistence for redacted provider health
  counters and circuit expiry. Startup restores operational state; persistence
  failures are swallowed so they cannot weaken fail-closed behavior or block a
  provider call.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore`
  passed with 0 warnings and 0 errors.
- AI gateway/contract/privacy and composition slice: 19 passed.
- Architecture suite: 41 passed.
- `SchoolAdminScaffoldTests` and `ClassroomCredentialStoreTests`: 17 passed.
- Desktop app Debug build: 0 warnings, 0 errors.
- Phase 02 composition and payload-preview model slice: 8 passed.
- Provider resilience and health-persistence slice: 4 passed.

## Remaining phase gate

Explicit user-configurable provider profiles, durable token/cost budgets,
retention and erasure journey, and cloud-provider conformance remain before
phase 27 closure.
