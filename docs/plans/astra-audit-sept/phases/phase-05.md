# Phase 05: first use, roots, import and processing feedback

Status: planned. Owner: ingestion/desktop engineer; reviewer: librarian QA.
Dependencies:03/04. Estimate:5-8 person-days. Findings:F23 plus first-use F01-F08 impacts.
Requirements: LIB family; old phases02-08/17. Skills:E1/E4/K1/D8; [routes](../06-skills-sources-and-kaizen.md).

## Outcome and hypothesis

A new user can open one PDF immediately or add a folder, understand what is happening and recover without learning the job pipeline. Clear units and durable sessions will prevent misleading scanned-book counts and duplicate work.

## Work slices

1. Test first use with a clean data directory, no network and no AI keys. Offer Open PDF and Add library folder with a short explanation that originals stay under the user's control. Do not require account/provider setup.
2. Trace direct-open and folder-pick paths through MainShellViewModel, library settings/root services, ingestion orchestrator and worker stages. Record whether direct-open registers, copies or references the source, and make that behavior explicit.
3. Separate discovery, successful import, duplicate/unchanged skip, failed file and background enrichment/indexing. Fix F23 only after tracing where `_filesCompleted` increments; do not relabel stage counts as unique books.
4. Support multiple/relinked roots through existing canonical contracts; detect unavailable or permission-denied paths. Avoid database placement on an unreliable shared filesystem by inheriting an obsolete README pattern.
5. Show progress without blocking reading of completed books. Provide cancel, safe retry, quarantine explanation and activity details. Preserve completed identity and leases across cancellation/restart.
6. Make configuration errors actionable in the startup recovery view. Avoid exposing raw exception strings, private paths or secrets in routine status; keep redacted diagnostics export available.

## Test matrix

Empty folder; one PDF; nested folders; duplicate content; renamed/moved root; read-only root; Unicode/long names; symlink/junction escape; inaccessible network share; corrupt/password PDF; concurrent watcher events; process interruption; disk full; canceled picker; source deleted during import. Use authorized/synthetic fixtures only.

## Acceptance

- A first-use participant opens the supplied book without AI, account setup or assistance.
- Counters reconcile to the unique fixture manifest and processing-stage records; no status says12 books when3 unique books were imported.
- Cancel/restart does not duplicate catalogue identity or erase progress; retry affects the failed scope only.
- Source paths outside approved roots are rejected; originals' hashes remain unchanged for read/import operations.
- Usable books appear progressively; UI remains responsive against the canonical2k/reference-machine budgets, with real timings recorded.
- Native folder/file dialogs and relink work on Windows/macOS, including cancellation and denied permissions.

## Recovery

Retain a verified backup before any schema or identity reconciliation change. Quarantine ambiguous relocation instead of guessing matches. Pause the importer when counts/invariants diverge; reading existing books must remain available. Re-measure during metadata/OCR phases and after crash/restart drills.
