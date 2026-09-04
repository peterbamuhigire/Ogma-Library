# Appendix: Evidence Register and Worktree Boundary

## Recent phase evidence

- Full repaired solution suite: `docs/implementation/execution/evidence/full-suite-repair-2026-09-04.md`

- Phase 14 review UI evidence: docs/implementation/execution/evidence/phase-14-review-ui-2026-09-04.md
- Phase 13: `docs/implementation/execution/evidence/phase-13-provider-terms-2026-09-04.md`
- Phase 14/20 bulk tags: `docs/implementation/execution/evidence/phase-14-bulk-tags-2026-09-04.md`
- Phase 16: `docs/implementation/execution/evidence/phase-16-detail-cover-ui-2026-09-04.md`
- Phase 17: `docs/implementation/execution/phase-17-progress.md` and
  `tests/OgmaLibrary.Tests/Ingestion/Phase17StageWorkerTests.cs`
- Phase 21 reader portability bounds: `docs/implementation/execution/evidence/phase-21-portability-bounds-2026-09-04.md`
- Phase 19: `docs/implementation/execution/evidence/phase-19-directory-view-2026-09-04.md`
- Phase 19 filter/sort: `docs/implementation/execution/evidence/phase-19-filter-sort-ui-2026-09-04.md`
- Phase 20: `docs/implementation/execution/evidence/phase-20-curation-ui-2026-09-04.md`
- Phase 20 tag editor: `docs/implementation/execution/evidence/phase-20-tags-ui-2026-09-04.md`
- Phase 20 collection controls: `docs/implementation/execution/evidence/phase-20-collections-ui-2026-09-04.md`
- Phase 20 smart-shelf query evaluation: `docs/implementation/execution/evidence/phase-20-smart-shelf-2026-09-04.md`
- Phase 25 stale-vector status UI: `docs/implementation/execution/evidence/phase-25-stale-count-ui-2026-09-04.md`
- Phase 25 bounded retrieval memory: `docs/implementation/execution/evidence/phase-25-bounded-retrieval-2026-09-04.md`
- Phase 25 query-embedding cache: `docs/implementation/execution/evidence/phase-25-query-embedding-cache-2026-09-04.md`
- Phase 22: `docs/implementation/execution/evidence/phase-22-search-ui-2026-09-04.md`
- Phase 27: `docs/implementation/execution/evidence/phase-27-privacy-journey-2026-09-04.md`
- Phase 27 policy editor: `docs/implementation/execution/evidence/phase-27-policy-ui-2026-09-04.md`
- Phase 28 reference resolution: `docs/implementation/execution/evidence/phase-28-reference-resolution-2026-09-04.md`
- Phase 29: `docs/implementation/execution/evidence/phase-29-answer-ui-2026-09-04.md`
- Phase 29 citation navigation: `docs/implementation/execution/evidence/phase-29-citation-navigation-2026-09-04.md`
- Phase 29 consent: `docs/implementation/execution/evidence/phase-29-content-consent-2026-09-04.md`
- Phase 29 unsupported-claim benchmark: docs/implementation/execution/evidence/phase-29-abstention-benchmark-2026-09-04.md
- Phase 30: `docs/implementation/execution/evidence/phase-30-feedback-consent-2026-09-04.md`
- Phase 30 UI: `docs/implementation/execution/evidence/phase-30-feedback-ui-2026-09-04.md`
- Phase 31: `docs/implementation/execution/evidence/phase-31-3d-host-contract-2026-09-04.md`
- Phase 32: `docs/implementation/execution/evidence/phase-32-virtual-bookshelf-2026-09-04.md`; `docs/implementation/execution/evidence/phase-33-texture-residency-2026-09-04.md`
- Phase 34: `docs/implementation/execution/evidence/phase-34-classroom-host-2026-09-04.md`; `docs/implementation/execution/evidence/phase-34-local-load-smoke-2026-09-04.md`
- Phase 35: `docs/implementation/execution/evidence/phase-35-classroom-client-2026-09-04.md`
- Phase 36: `docs/implementation/execution/evidence/phase-36-school-admin-2026-09-04.md`
- Phase 37: `docs/implementation/execution/evidence/phase-37-security-hardening-2026-09-04.md`
- Phase 38: `docs/implementation/execution/evidence/phase-38-release-candidate-2026-09-04.md`
- Phase 39: `docs/implementation/execution/evidence/phase-39-release-acceptance-2026-09-04.md`

## Preserved user worktree files

The audit did not stage or alter these existing user-owned changes:

- Catalogue/reader/settings UI and view-model files under
  `src/OgmaLibrary.App`.
- Reader session/cache/PDF adapter files and related reader tests.
- Modified developer-guide images.
- `docs/implementation/execution/verification-2026-09-04.md`.
- `docs/pdf-standards/` and `tmp/`.

## Git handoff

The audit evidence itself is intended to be committed as a documentation-only
increment. After commit, verify `git status`, `git diff --cached --check`, and
remote parity. The user-owned files above must remain outside the commit.
- Phase 11 real PDF adapter corpus: `docs/implementation/execution/evidence/phase-11-real-pdf-corpus-2026-09-04.md`
- Phase 17 restart/recovery load: `docs/implementation/execution/evidence/phase-17-restart-recovery-load-2026-09-04.md`
