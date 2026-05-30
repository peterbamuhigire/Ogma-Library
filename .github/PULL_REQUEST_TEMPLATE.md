<!--
Ogma Library pull request. Keep PRs small enough to review in one sitting.
Every PR traces to one or more baselined requirement IDs (FR-…, NFR-…,
CTRL-OGMA-…, ADR-000N) per the Hybrid traceability chain.
-->

## Summary

<!-- What does this PR do, in one or two sentences? -->

## Requirement traceability

- **Implements / closes:** <!-- e.g. FR-LIB-003, NFR-OGMA-005, ADR-0004 -->
- **Phase:** <!-- e.g. Phase 05 — Ingestion -->
- **Branch:** `feature/<phase-ID>-<slug>` <!-- per docs/governance/BRANCH-STRATEGY.md -->

## Change Impact Analysis (CIA)

- [ ] **(a) Bounded contexts affected:** <!-- Catalogue / Ingestion / Reader / Search / AI / Bookshelf / Settings&Security / Packaging / Host -->
- [ ] **(b) FR/NFR/ADR IDs touched:** <!-- list -->
- [ ] **(c) Baselined requirement altered?** No / Yes → owner sign-off + ADR amendment linked: <!-- link -->
- [ ] **(d) New user-facing strings externalized in en + fr** (no hard-coded strings); pseudolocale check passes.
- [ ] **(e) New interactive controls have a colorful icon + an accessible (text/aria) label;** keyboard + screen-reader operable.
- [ ] **(f) Reversibility:** any destructive operation has backup → diff → verify → restore (or N/A).
- [ ] **(g) Privacy/egress:** any off-device call routes through the AI gateway with payload preview + audit (or N/A).

## Verification (must pass before review)

- [ ] `dotnet format --verify-no-changes`
- [ ] `dotnet build` (warnings are errors)
- [ ] `dotnet test` (incl. golden-corpus suite) — green on **Windows and macOS**
- [ ] Architecture tests pass (domain has no outward deps; single AI egress chokepoint)
- [ ] `python -m engine validate Ogma-Library` exits 0 (where applicable)
- [ ] Performance budgets touched are within budget (or recorded as trend)

## Risk

- **R-tier:** R1 (data-loss) / R2 (privacy) / R3 (perf) / R4 (recoverability) / R5 (functional)
- R1/R2 failures are unwaivable release blockers.

## Sign-off

- [ ] All commits are **DCO signed-off** (`git commit -s`) — see `DCO.md`.
- [ ] Commit messages follow Conventional Commits — see `docs/governance/CONVENTIONAL-COMMITS.md`.
