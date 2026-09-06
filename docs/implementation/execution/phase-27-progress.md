# Phase 27 Progress - AI Gateway, Privacy and Cost Runtime

Date: 2026-09-04

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
- Added a durable UTC-day AI usage ledger at the gateway boundary. Cloud calls
  reserve a bounded token estimate after preview and consent, reconcile
  provider-reported token usage and calculated cost after completion, and
  release reservations on cancellation or failure. State is persisted through
  atomic versioned JSON and the in-memory gate remains authoritative if storage
  is unavailable.
- Added durable user-configurable provider profiles. Profiles persist only
  platform credential references, validate provider-specific endpoint allowlists,
  atomically replace the settings file, and support deterministic listing and
  deletion. The desktop composition registers the store under the runtime data
  directory.
- Verified and closed the local retention/erasure journey: Privacy Center
  history export/deletion, immutable-audit preservation, local embedding
  erasure, and provider-profile deletion are wired and test-backed; see
  `evidence/phase-27-privacy-journey-2026-09-04.md`.
- Verified the rendered school AI policy editor: all three bounded policy
  inputs, save action, and service-boundary persistence are covered by the
  Avalonia render test; see `evidence/phase-27-policy-ui-2026-09-04.md`.

## Verification

- `dotnet build OgmaLibrary.sln --configuration Release --no-restore`
  passed with 0 warnings and 0 errors.
- AI gateway/contract/privacy and composition slice: 19 passed.
- Architecture suite: 41 passed.
- `SchoolAdminScaffoldTests` and `ClassroomCredentialStoreTests`: 17 passed.
- Desktop app Debug build: 0 warnings, 0 errors.
- Phase 02 composition and payload-preview model slice: 8 passed.
- Provider resilience and health-persistence slice: 4 passed.
- Phase 27 usage-budget, provider-resilience and gateway slice: 17 passed,
  including gateway rejection before provider invocation.
- School policy editor render/binding slice: 1 passed.
- Local gate reconciliation completed; all repository-only Phase 27 controls
  are covered by the evidence records listed in
  `evidence/phase-27-local-gate-reconciliation-2026-09-04.md`.

## Remaining phase gate

The provider-profile persistence/validation, local retention/erasure, and
policy-editing UX subgates are closed by focused tests. Provider-specific
retention/terms acceptance, cloud-provider conformance, and physical
accessibility evidence remain before phase 27 closure.

The Aug-39 Definition of Done now records enforced single-gateway egress,
disabled-by-default core independence, exact preview/consent, and provider
failure isolation as closed. The combined OS-secret/cost/deletion criterion
remains unchecked because physical platform-secret custody is still
`NOT ASSESSED`, despite passing local cost and deletion tests.
