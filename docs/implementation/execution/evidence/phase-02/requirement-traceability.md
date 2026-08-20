# Phase 2 requirement traceability

| Requirement | Workflow and implementation evidence | Failure evidence | Test evidence | Phase status |
| --- | --- | --- | --- | --- |
| NFR-OGMA-001 | Window is assigned to `DesktopShellWindow` before `ComposeRuntime`; graph validation/view-model construction use `Task.Run`; coordinator spans record startup work | Composition failures become a safe shell; cancellation is not converted to failure | `Architecture_ColdStart_YieldsShellBeforeCompositionAndAvoidsSyncStartupWaits`; headless bootstrap render | Implemented architecturally; physical W-REF-01 cold-start P95 remains NOT ASSESSED until release performance phases |
| NFR-OGMA-005 | Existing page-cache contract and reader registrations preserved in the `reader` module | PDF worker unavailability degrades processing, not catalogue access | Full reader/UI regression suite | Preserved; physical cached page-turn P95 remains NOT ASSESSED here |
| NFR-PROD-001 | External metadata, AI, 3D and classroom Host are disabled by default; migration/catalogue/reader/search core compose without network calls | Optional providers and local worker prerequisites cannot block the catalogue | `DefaultMatrix_ResolvesAllModules_WithoutExternalProvidersOrAi`; optional-failure coordinator/UI tests | COMPLETE for Phase 2 startup scope |
| NFR-PROD-009 | Typed OS-neutral paths, platform web-view/password selection and explicit local capability probes | Invalid paths fail with portable, redacted errors; platform capability is reported rather than assumed | path/worker validation tests; architecture runtime-branch tests | COMPLETE for Phase 2 composition scope |

Trace chain for each row is: requirement → startup workflow → composition or
coordinator service → shell state → safe failure behavior → executable test.
No requirement is marked release-complete where the canonical SRS requires later
physical reference-device or cross-platform evidence.
