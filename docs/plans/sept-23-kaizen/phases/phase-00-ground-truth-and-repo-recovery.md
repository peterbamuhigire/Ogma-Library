# Phase 00: Ground truth, repository and toolchain recovery

## 1. Header

| Field | Value |
|---|---|
| Wave | A: Stabilise |
| Size | 2–3 engineering days |
| Depends on | — (first phase) |
| Owner decisions | **D-01** (the deleted test projects), **D-02** (plan authority) |
| Primary defects | K01, K02, K03, K04, K06, K91 |
| Requirement IDs | NFR-OGMA governance and build gates; no FR changes |

## 2. Why this phase exists

The owner's working tree cannot build the solution. The two largest test projects (210 files)
are deleted and not committed, while `OgmaLibrary.sln:26,28` still references them. The build
therefore fails with `MSB3202 The project file ... was not found` (K01). The SDK is not pinned,
and an in-place update of SDK 10.0.401 made the first build of this audit fail for reasons
unrelated to the code (K02). The format gate fails on import ordering (K03). The core suite
takes 18 minutes because 50k-scale benchmarks run with the unit tests. One OCR test fails on a
temp-file lock, and the test host raised a Windows Firewall prompt (K04). The build output also
ships diagnostics and mobile runtimes, and about 77 stale `src/*/tmp` folders sit in the tree
(K06). Four overlapping plans disagree about status (K91).

Nothing else in this plan can be verified until a clean checkout builds, the gates are fast and
deterministic, and the plan authority is unambiguous.

## 3. Objectives and exit criteria

1. `dotnet build OgmaLibrary.sln -c Release` succeeds on the owner's working tree **and** on a
   clean clone, with the pinned SDK.
2. Every CLAUDE.md gate passes. `dotnet format --verify-no-changes` reports 0 diagnostics.
3. The fast test suite (everything except `Category=Performance`/`Benchmark`) runs in
   **≤ 6 minutes** locally and passes 100 %. The performance suite runs in a separate nightly
   CI job.
4. No test binds to a non-loopback interface; a full test run produces no firewall prompt.
5. The OCR test `PackagedTesseract_RecognizesExpectedWordsFromGeneratedScannedFixture` passes
   10 of 10 consecutive runs.
6. `CLAUDE.md`, `docs/plans/aug-39/README.md` and
   `docs/implementation/execution/00-execution-status.md` state the D-02 authority.
7. There are no untracked `src/*/tmp` build directories, and `.gitignore` prevents recurrence.

## 4. Skills to load before starting

- `C:\wamp64\www\chwezi-dev-engine\skills\sdlc-meta\implementation-status-auditor\SKILL.md`: the verified triage state machine used to classify prior claims.
- `C:\wamp64\www\chwezi-dev-engine\skills\sdlc-meta\advanced-testing-strategy\SKILL.md`: test layers, flake policy ("a flaky test is a defect with an owner and deadline").
- `C:\wamp64\www\chwezi-dev-engine\skills\devops-cloud\cicd-pipelines\SKILL.md`: splitting fast and nightly pipelines.
- `C:\wamp64\www\chwezi-dev-engine\skills\languages\csharp-dotnet-development\SKILL.md`: SDK pinning and the project boundary map.
- `C:\wamp64\www\chwezi-dev-engine\skills\sdlc-meta\kaizen-improvement-system\SKILL.md`: the baseline must be frozen before improvement.

## 5. Scope in / scope out

**In:** test-project recovery, SDK pin, format fix, test categorisation, loopback-only test
listeners, OCR test flake, tmp cleanup, `.gitignore`, plan-authority edits, execution-status
reset, CI fast/nightly split.

**Out:** any product behaviour change; the Release packaging trim (moved to Phase 26, though
this phase records the list of unwanted files); new tests other than those needed to prove the
flake fix.

## 6. Work breakdown

**T00.1: Resolve D-01 (deleted tests).** Ask the owner. Recommended: `git restore tests/`. This
restores the 181 files of `tests/OgmaLibrary.Tests` and the 29 of `tests/OgmaLibrary.Tests.Ui`
exactly as committed at `0ad3c0c`. If the owner intends a rewrite, remove both projects from
`OgmaLibrary.sln` in the same commit and open Phase 01 replacement tasks. Never leave the
solution referencing missing projects.
*Acceptance:* `git status --short tests/` is empty or the sln no longer references the
projects; `dotnet build OgmaLibrary.sln -c Release` has 0 errors.

**T00.2: Pin the SDK.** Add `global.json` at the repo root:
`{"sdk":{"version":"10.0.401","rollForward":"latestPatch","allowPrerelease":false}}`, using the
version that is installed and verified on the reference machine. Record in
`docs/developer-guide/README.md` how to install that SDK. CI uses `actions/setup-dotnet` with
`global-json-file: global.json`.
*Acceptance:* `dotnet --version` in the repo prints the pinned band; CI logs the same version.

**T00.3: Fix the format gate.** Run `dotnet format OgmaLibrary.sln --diagnostics IMPORTS` on
`src/OgmaLibrary.App/Startup/StartupTasks.cs` and
`src/OgmaLibrary.Workers/Pdf/PdfWorkerCommand.cs` only. Do not run a repo-wide fix-all over
user files. Add a `.gitattributes` rule (`* text=auto eol=lf` for `*.cs`, `*.axaml`, `*.md`) if
line-ending diagnostics reappear.
*Acceptance:* `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` exits 0.

**T00.4: Categorise slow tests.** Tag every test that runs longer than 5 s, or that exercises
10k/50k scale, with `[Trait("Category","Performance")]`. The existing 5 tests use
`Trait("Category","Benchmark")`; keep that category and treat both as slow. Known candidates
from the TRX timings: `MetadataSearchServiceTests.PerfBenchmark_*`,
`SemanticSearchServiceTests.PerfBenchmark_*`,
`DiscoveryServiceTests.DiscoveryService_EnumeratesFiftyThousandFilesWithBoundedChannel`,
`FtsIndexServiceTests.PerfBenchmark_*`, `Phase16VisualAssetDiskBudgetTests.*`,
`Phase16VisualAssetTests.VisualAssetService_PreferredLookupStaysBoundedAt50kBooks`,
`CatalogueReadModelTests.GetBookSummaries_50kServerSidePage_*`,
`OcrGoldenCorpusTests.OcrJob_VeryLargePdf_NoOutOfMemory`, and
`Phase15SmartShelfPerformanceTests.*`. Add `scripts/Test-Fast.ps1` (filter
`Category!=Performance&Category!=Benchmark`) and `scripts/Test-Performance.ps1`.
*Acceptance:* the fast suite takes ≤ 6 min; the performance suite still passes when run alone.

**T00.5: Loopback-only test listeners.** Find test code that binds `IPAddress.Any`,
`IPv6Any`, `0.0.0.0` or `ListenAnyIP` (the classroom/LAN tests, for example
`tests/OgmaLibrary.Tests/ClassroomClient/LibraryHostHttpClientTests.cs`), and bind
`IPAddress.Loopback` with port 0 instead. Where the production listener
(`KestrelHostModeListener`) takes its bind address from options, add an option and use loopback
in tests. Add an architecture-style test that scans test assemblies for `IPAddress.Any`.
*Acceptance:* a full test run on a machine without prior firewall rules raises no prompt;
`netstat -ano` during the run shows only `127.0.0.1` or `[::1]` listeners from `testhost`.

**T00.6: Fix the OCR test file lock (K04).** In `tests/OgmaLibrary.Tests/Ocr/Phase24RealOcrCorpusTests.cs`,
find which handle holds the temp file (Tesseract engine, the worker process or a
`FileStream`). Dispose the engine and wait for the worker exit before deleting. Use a
retrying delete helper (3 attempts, 200 ms) **only** for the test directory, and record the
underlying owner of the handle. If the production OCR path leaks the handle, fix it there and
note it for Phase 17.
*Acceptance:* the test passes 10 consecutive times with
`dotnet test --filter FullyQualifiedName~Phase24RealOcrCorpusTests`.

**T00.7: Tree hygiene.** Delete untracked `src/*/tmp/` directories after listing them. Add
`src/*/tmp/` to `.gitignore`. Record the list of unwanted Release outputs
(`Avalonia.Diagnostics.dll` and the ~40 non-target `runtimes/*` folders) in the Phase 26 backlog.
*Acceptance:* `git status --ignored` shows no `tmp` build folders.

**T00.8: Plan authority (D-02).** Update:
- `CLAUDE.md` (the "Authoritative sources" table): add
  `docs/plans/sept-23-kaizen/README.md` as the **execution sequence**; keep Aug-39 as the
  requirement accountability authority.
- `docs/plans/aug-39/README.md`: add a banner pointing to Sept-23 for sequencing.
- `docs/plans/astra-audit-sept/README.md` and `docs/kaizen/2026-09-10-product-kaizen-no-visual/05-final-report.md`: add a "superseded as an execution plan" note.
- `docs/implementation/execution/00-execution-status.md`: add a Sept-23 section with the
  measured baseline (raw 30/100) and a link to the defect register.
- Fix F25 now: the requirement matrix labels split view as FR-READ-010, but the SRS says
  FR-READ-012.

*Acceptance:* `./scripts/Test-RequirementAccountability.ps1` still passes; a fresh agent
reading `CLAUDE.md` finds exactly one execution sequence.

**T00.9: CI split.** In `.github/workflows/ci.yml`, change the test step (currently
`dotnet test OgmaLibrary.sln ... -m:1` at line 97) to the fast filter, and add a scheduled
nightly workflow for the performance category on `windows-latest` and `macos-latest`.
*Acceptance:* a pull request run finishes the test step in ≤ 10 min per OS; the nightly run is visible.

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| Solution does not build (K01) | Test folders deleted, not committed; sln unchanged | Restore or de-reference (D-01) | Build succeeds | MSB3202 → 0 errors | build log | Owner wanted deletion | Re-delete after sln edit |
| Non-reproducible toolchain (K02) | No `global.json` | Pin SDK band | Same SDK everywhere | unpinned → pinned | `dotnet --info` | Machine lacks the SDK | Loosen `rollForward` |
| Format gate red (K03) | Two files mis-ordered | Targeted fix | Gate green | 2 errors → 0 | format log | None | Revert commit |
| 18-min suite (K04) | Benchmarks mixed with unit tests | Categorise and split | Faster feedback | 18 min → ≤ 6 min | TRX timings | Benchmarks rot | Nightly job alerts |
| Firewall prompt (K04) | Tests bind all interfaces | Loopback binding | No prompt | prompt → none | netstat capture | Hidden prod dependency | Option default unchanged |
| Conflicting plans (K91) | No supersession record | Authority edits | One sequence | 4 plans → 1 sequence + 1 matrix | CLAUDE.md diff | Confusion over history | Revert doc commit |

## 8. Test plan

- **Unit:** none new except the loopback architecture test (T00.5) and the stabilised OCR test.
- **Negative:** temporarily rename a test csproj in a scratch branch and confirm the build fails
  loudly (it should); confirm the fast filter does not silently exclude uncategorised tests
  (compare counts: fast + performance = total).
- **Flake check:** run the fast suite 3 times in a row; any failure is a defect with an owner.

## 9. Acceptance commands

```powershell
dotnet --version
./scripts/Test-RequirementAccountability.ps1
dotnet restore OgmaLibrary.sln --locked-mode
dotnet format OgmaLibrary.sln --verify-no-changes --no-restore
dotnet build OgmaLibrary.sln --configuration Release --no-restore
dotnet list OgmaLibrary.sln package --vulnerable --include-transitive
dotnet format analyzers OgmaLibrary.sln --verify-no-changes --no-restore --severity warn --verbosity minimal
./scripts/Test-Fast.ps1          # dotnet test ... --filter "Category!=Performance&Category!=Benchmark"
./scripts/Test-Performance.ps1   # nightly; locally on demand
```

### Definition of Done additions

The completion record includes the before and after test counts (1,161 → same or higher), the
fast/performance split counts, and the test step duration per OS.

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence |
|---|---|---|
| D-01 answer | Owner | Blocks T00.1 and every later phase |
| macOS CI runner performance suite timing | Engineering | Nightly result only; not a gate for this phase |

## 11. Risks and mitigations

- *The deletion was deliberate and partial work is lost.* Take a `git stash`-free safety copy
  (`git diff --stat` saved to the completion record) before restoring.
- *Categorisation hides a real regression.* The nightly performance job must be required before
  any release candidate (Phase 26).
- *Authority edits confuse history.* Edit by adding banners only; never rewrite prior records.

## 12. Execution prompt

```
## Prompt 00 - Recover a buildable, fast, single-authority baseline
You are restoring the Ogma Library repository to a verifiable baseline in C:\wamp64\www\Ogma-Library.
Read these files first, in this order:
1. C:\wamp64\www\Ogma-Library\CLAUDE.md
2. docs/plans/sept-23-kaizen/README.md
3. docs/plans/sept-23-kaizen/AGENT_BRIEF.md (invariants)
4. docs/plans/sept-23-kaizen/03-defect-register.md (K01-K06, K91)
5. docs/plans/sept-23-kaizen/phases/phase-00-ground-truth-and-repo-recovery.md
Then load skills by reading SKILL.md directly: implementation-status-auditor, advanced-testing-strategy,
cicd-pipelines, csharp-dotnet-development (paths in section 4).
Work plan:
A. Serial prerequisites (you own): obtain the owner's D-01 and D-02 answers; do not guess D-01.
B. Then T00.2-T00.9 in order, one Conventional Commit with DCO sign-off per task.
File scope allowed: global.json, .gitignore, .gitattributes, OgmaLibrary.sln, tests/**, scripts/Test-*.ps1,
.github/workflows/**, the two files in T00.3, CLAUDE.md, the plan README banners listed in T00.8,
docs/implementation/execution/00-execution-status.md.
Do not change product behaviour. Do not run a repo-wide format fix-all.
Acceptance: every command in section 9 passes; record outputs in
docs/implementation/execution/phase-sept23-00-completion.md with the rollback commit.
A generated file or an unexecuted test is not completion. Never convert a failing test into an expected failure.
Recovery point: the commit before T00.1.
```
