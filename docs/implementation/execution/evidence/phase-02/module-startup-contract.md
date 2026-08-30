# Phase 2 module and startup contract

## Authority and scope

This contract implements Phase 2 of the approved 39-phase roadmap and the HLD
composition rules. It does not activate external AI, complete 3D hosting or
change the catalogue schema. Later phases may extend a module through its public
service contracts, but must preserve deterministic registration, local-first
startup and the catalogue's independence from optional capabilities.

## Deterministic module order

| Order | Module | Responsibility | Required to compose |
| ---: | --- | --- | --- |
| 1 | `core-platform` | Benchmarking, localization, platform web-view and password bindings | Yes |
| 2 | `catalogue-processing` | Catalogue persistence, ingestion, metadata core, search core and workers | Yes |
| 3 | `classroom` | Existing Host/Client service contracts; listener remains inactive | Yes |
| 4 | `reader` | Isolated PDF renderer and reader/annotation services | Yes |
| 5 | `shell` | Catalogue/reader/search view-model graph | Yes |
| 6 | `startup` | Ordered tasks, capability probes, coordinator and startup shell | Yes |

The executable order is exposed by `CompositionRoot.RegisteredModuleNames` and
locked by `Architecture_CompositionModules_AreOrderedAndExternallyDisabledByDefault`.
`CompositionRoot.cs` remains the single orchestrator and delegates registrations
to the six module registrars.

## Typed runtime configuration

`OgmaRuntimeOptions` is the non-secret configuration boundary. Supported
environment adapters are:

| Setting | Default | Effect |
| --- | --- | --- |
| `OGMA_LIBRARY_DATA_DIR` | OS application-data location | Catalogue and derived-data root |
| `OGMA_LIBRARY_ROOT` | Data directory | Compatibility library root until Phase 5 |
| `OGMA_PDF_WORKER_PATH` | Packaged worker discovery | Explicit isolated worker file |
| `OGMA_ENABLE_METADATA_PROVIDERS` | `false` | Register Open Library/Google Books adapters |
| `OGMA_ENABLE_3D_SHELF` | `false` | Permit runtime capability detection only |
| `OGMA_ENABLE_CLASSROOM_HOST` | `false` | Offer Host controls; never auto-start listener |

Paths must be absolute, an explicitly configured worker must exist and booleans
accept only `true`, `false`, `1` or `0`. Validation errors contain the setting
name and safe guidance, never the configured value. Credentials are deliberately
absent and remain in OS-backed secret stores.

## Startup task order and failure contract

| Order | Task | Criticality | Idempotent outcome | Failure behavior |
| ---: | --- | --- | --- | --- |
| 1 | `catalogue.migration` | Required | Current migrations applied | Block catalogue; PDFs unchanged; retry/export available |
| 2 | `jobs.recovery` | Optional | Interrupted work requeued | Open catalogue; processing paused; retry available |
| 3 | `workers.start` | Optional | Hosted workers started once | Open catalogue; background work unavailable; retry available |

`ApplicationStartupCoordinator` is cancellable, serializes concurrent attempts,
stops successfully started resources in reverse order and never places exception
messages in its report. Required failure stops later tasks. Optional failure does
not change `CanOpenCatalogue` to false.

Composition and full view-model construction run outside the Avalonia UI thread.
The lightweight bootstrap window is assigned first, then the dispatcher yields a
frame before starting composition. Migration, recovery and worker activation
occur only through the coordinator.

## Capability matrix

| Capability | Default | Probe behavior | Catalogue impact |
| --- | --- | --- | --- |
| External metadata | Disabled | No network call; reports `disabled_by_default` | None |
| External AI | Disabled | Reports `phase_27_required`; no gateway/pipeline registered | None |
| Search index | Detection pending | Deferred until catalogue opens | None |
| 3D shelf | Disabled | If enabled, runtime WebGL/native detection still required | None |
| Classroom Host | Disabled | No listener starts automatically | None |
| Isolated PDF worker | Local prerequisite probe | Reports available/unavailable without opening a PDF | Processing only |

The PDF worker has one DI registration shared by ingestion and reader services;
an explicit path cannot be shadowed by a default registration.

## Observability and privacy

Startup records named timing spans, stable codes, safe summaries and task
durations. Diagnostic export contains task/capability status only. It excludes
configured paths, exception messages, PDF contents, prompts and credentials.
Capability probing performs no external network access.

## Contract tests

- `Phase02CompositionTests`: default and explicit metadata matrices, graph
  validation, AI absence and redacted options.
- `Phase02StartupCoordinatorTests`: required/optional failure, cancellation,
  retry, redaction and reverse shutdown.
- `ApplicationStartupTests`: real migration, worker lifecycle and catalogue query.
- `ArchitectureTests`: deterministic modules, default capability policy and
  UI-thread cold-start shape.
- `StartupShellRenderTests`: bootstrap, blocked and partial degraded visual states.
