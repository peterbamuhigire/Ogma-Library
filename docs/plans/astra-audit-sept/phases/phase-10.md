# Phase 10: metadata, canonical identity and safe curation

Status: planned. Owner: metadata/catalogue engineer; reviewer: librarian/data-safety QA.
Dependencies:05-07. Estimate:6-10 person-days. Findings:F13/F14/F24 plus open writeback gates.
Requirements:META family, CAT work/edition obligations; old phases09/11-16.
Skills:E1/E4/E7/R1/R2; [routes](../06-skills-sources-and-kaizen.md).

## Outcome

Metadata improves findability while keeping book identity, manual corrections and source files safe. Enrichment remains a reviewable proposal, not an invisible overwrite.

## Work slices

1. Reconcile work/edition/asset/occurrence behavior and duplicate grouping in repositories and read models. Define duplicate files versus different editions clearly in the UI.
2. Verify ISBN detection/provenance, source precedence and manual override protection. Preserve raw observed values and normalized canonical values without presenting confidence as certainty.
3. Connect provider configuration/capability to review UI. Show exact fields sent, source attribution, freshness, quota/offline state and conflict rationale. Verify current provider terms through Digital Research before enabling a live integration.
4. Make manual metadata/tag/rating/status edits straightforward in phase07's expanded editor. Validate field lengths, language/year/ISBN syntax, optimistic concurrency and undo. Bulk preview must show selected count and per-item changes.
5. Exercise `PdfWriteBackService` with explicit consent, before/after diff, backup, source-hash guard, read-only/locked file detection, atomic promotion and verified undo. Metadata edit in the catalogue must not silently write the PDF.
6. Invalidate derived cover/text/search/embedding artifacts after relevant changes using versioned lifecycle contracts, not indiscriminate destructive rebuilds.

## Edge and interruption cases

Conflicting providers, wrong edition/ISBN, no metadata, stale cache, provider denial, canceled bulk apply, source modified during preview, backup disk full, process kill during promotion, permission changes, undo after later user edit and multiple file occurrences.

## Acceptance

- Canonical identity and metadata fields reconcile across grid/list/detail/search/3D; manual overrides survive provider refresh.
- Provider outage leaves local catalogue/read functionality intact and labels stale evidence honestly.
- Bulk preview/apply/undo affects exactly selected records; conflicts do not overwrite newer edits.
- Real Windows/macOS writeback interruption rehearsal preserves either verified original or verified replacement, with recoverable backup and audit evidence.
- Source change invalidates only affected derived generations and never leaves citations falsely bound to old content.
- Provider/privacy terms and attribution have source/date/reviewer records; no current compliance assertion based on a historical document alone.

## Recovery

Back up catalogue and originals before writeback experiments; use authorized disposable copies. Do not roll back by deleting originals or blindly replacing changed files. Quarantine unresolved identity conflicts. Re-measure incorrect-enrichment rate, review effort and undo success during librarian acceptance.
