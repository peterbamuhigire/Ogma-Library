# Agent brief: invariants for executing the Sept-23 Kaizen plan

Read this before any phase. It restates the rules that apply to every phase, so each phase
document can focus on its own work.

## Read order

1. `CLAUDE.md` (repository root). Its rules win where this brief is silent.
2. [README.md](README.md): status register and current phase.
3. This brief.
4. The phase document in [`phases/`](phases/).
5. The defect rows it cites in [03-defect-register.md](03-defect-register.md).

## Non-negotiable rules

1. **Real-window proof.** A behaviour change is not done until the Phase 01 journey suite passes
   in the real application window at 1280×800 and 1920×1080, with before and after screenshots.
   Unit and headless tests are necessary but not sufficient. Until Phase 01 lands, use
   `evidence/tools/Invoke-OgmaUia.ps1`.
2. **Isolated data.** Never run the app against the owner's real library or data folder. Always
   set `OGMA_LIBRARY_DATA_DIR` to a fresh temporary directory, and use the synthetic corpus from
   `evidence/tools/New-SyntheticCorpus.py` (or its Phase 01 successor). Never commit private book
   content. Evidence records identifiers and results only.
3. **Preserve user work.** Do not discard uncommitted changes, rewrite shared history, or run
   destructive git commands. Decision D-01 (the deleted test projects) is the owner's.
4. **Architecture.** Dependencies point inward. Only the composition root binds implementations.
   Domain and Application do not initiate HTTP. The SQLite catalogue is the single source of book
   identity. The architecture tests are release gates. PDFs, filenames and paths are untrusted:
   keep them behind the isolated worker and path-validation boundaries.
5. **Async and threading.**
   - No new `async void` except event handlers that go through the Phase 02 safe-handler wrapper.
   - Library code uses `ConfigureAwait(false)`, and any view-model mutation afterwards is
     marshalled to `Dispatcher.UIThread`.
   - Every async API accepts a `CancellationToken`.
   - No synchronous IPC or blocking waits on the UI thread.
6. **Errors are visible and logged.** Use the Phase 02 logging abstraction with redaction. Do not
   write empty `catch` blocks. Show users localised, actionable messages, never raw `ex.Message`.
7. **Design system.**
   - Typography: Spectral for display, Public Sans for body, JetBrains Mono for code/data. These
     are licensed and compliant with the design engine. Never introduce a banned font (Inter,
     Roboto, Geist, Space Grotesk, Poppins, Montserrat and others on the design engine's list).
   - Use tokens from `src/OgmaLibrary.App/Themes/`. No hard-coded colours, `FontSize` or
     `Foreground`.
   - No placeholder icons or untraceable assets.
   - State the typeface decision in any UI phase record.
8. **Localisation.** Every user-facing string goes through the localisation service, in both
   English and French.
9. **Accessibility.** Every interactive control and list item has a meaningful accessible name
   (never a type name or record dump), a visible focus state and keyboard access.
10. **Evidence classes.**
    - Label claims MEASURED, CODE or HEURISTIC.
    - Physical, platform, signing, provider, assistive-technology and two-machine checks that
      were not run are `NOT ASSESSED`, with an owner. They are never passes.
11. **Commits.** Conventional Commits with DCO sign-off (`git commit -s`). Use small slices, and
    record a rollback point for each. Do not commit on `main` unless the owner authorises it;
    prefer a branch per phase.
12. **Proportionate documentation.** Produce one completion record per phase
    (`docs/implementation/execution/phase-sept23-NN-completion.md`) plus screenshots and logs
    under `docs/implementation/execution/evidence/sept-23-kaizen/phase-NN/`. Do not generate bulk
    evidence prose.
13. **Scope discipline.**
    - Do what the phase says.
    - If you find a new defect, add it to the defect register with the next free `K` ID and assign
      it to a phase; do not silently expand scope.
    - If a phase premise is wrong, stop and record it rather than forcing the plan.

## Standard gate commands

```powershell
./scripts/Test-RequirementAccountability.ps1
dotnet restore OgmaLibrary.sln --locked-mode
dotnet format OgmaLibrary.sln --verify-no-changes --no-restore
dotnet build OgmaLibrary.sln --configuration Release --no-restore
dotnet list OgmaLibrary.sln package --vulnerable --include-transitive
dotnet format analyzers OgmaLibrary.sln --verify-no-changes --no-restore --severity warn --verbosity minimal
dotnet test OgmaLibrary.sln --configuration Release --no-build -m:1 --filter "Category!=Performance"
# after Phase 01: the real-window journey suite (command defined in phase-01)
# when src/shelf3d changes:
#   cd src/shelf3d; npm ci; npm run typecheck; npm run build; npm run perf:budget
```

Before rebuilding, kill any running `OgmaLibrary.App` and `OgmaLibrary.Workers` processes;
running instances lock the output files (MSB3026/MSB3021).

## Golden journeys

Phase 01 is the authority for G1–G8 and their oracles (see
[phase-01 T01.8](phases/phase-01-real-window-acceptance-harness.md)). Later phases add G9–G12
to the same harness.

| ID | Journey | Added by |
|---|---|---|
| G1 | First run: empty state visible, call to action reachable at both sizes | 01 |
| G2 | Add library → scan → catalogue shows valid books with covers | 01 |
| G3 | Select → detail inspector → Read → turn pages → back to library | 01 |
| G4 | Search: title, author, body phrase, typo, Unicode | 01 |
| G5 | Resume: close mid-book, relaunch, resume within 60 s (UX-007) | 01 |
| G6 | Invalid files: 0-byte, non-PDF, truncated, password-protected handled honestly | 01 |
| G7 | Advisor: unconfigured → helpful state; configured/offline → grounded answer with citations | 01 (extended in 16) |
| G8 | Resilience: kill the PDF worker mid-read; corrupt settings; the app survives and logs | 01 |
| G9 | Metadata: edit, enrich, preview write-back, undo | 11 |
| G10 | Organise: shelves, smart shelf, bulk edit with undo, duplicates/editions | 10 |
| G11 | Classroom: Host starts, a client joins, browses, reads, searches; revoke | 19–20 |
| G12 | 3D shelf: renders with real covers, keyboard alternative, fallback when WebGL2 is absent | 18 |

Run journeys with `./tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1` (contract in
Phase 01, task T01.12). Filter by `-Journey G2` or `-Tag Reader`.
