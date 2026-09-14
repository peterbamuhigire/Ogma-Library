# Phase 07: complete overhaul of the right book inspector

Status: planned. Owner: UX lead and desktop engineer; reviewer: Peter, reader and librarian.
Dependencies:02,04,06 selection contract. Estimate:5-8 person-days.
Findings:F02/F14/F29; Peter explicitly requested this overhaul.
Requirements:FR-CAT-004 and applicable META/READ organization fields.
Skills:E1/D1/D2/D5/D8; [routes](../06-skills-sources-and-kaizen.md).

## Outcome and design decision

The inspector answers: Which book is this? Can I read or resume it? What do I need to know or change? It appears only for a selected book, closes reliably and never consumes essential reading space. This phase is a redesign of information hierarchy, not just new colors around the current seven tabs.

## Work slices

1. Preserve phase02 visibility regression. Review `BookDetailView.axaml`, code-behind and BookDetailViewModel as one interaction, including async load, stale selection, Close and navigation.
2. Build compact identity summary: readable title, author, edition/year, modest cover, availability and reading position. Read/Resume is the primary action. Close is always visible with tooltip, name and keyboard path.
3. Prototype Overview/Notes/Details or another task-tested grouping. Retain file data, bibliography, enrichment, AI context, contents and provenance through well-labeled sections. Avoid seven wrapping oversized tab headings. Put advanced metadata review in an expanded editor when needed.
4. Provide a resizable dock with minimum/maximum bounds on large windows; switch to a dismissible overlay or dedicated details route when the remaining catalogue/reader would be too narrow. Preserve a stable primary-action area and one content scroll owner.
5. Make Enrich/OCR secondary actions with capability state, progress and effect disclosure. Writeback requires the existing explicit preview/backup flow. Keep read-only provenance separate from editable metadata.
6. Define immediate selected-book loading state; cancel outdated loads. Prevent prior book cover/title/metadata/action IDs from mixing during rapid selection. Return focus to the selected row/card on Close.

## Critical states

No selection: hidden. Selected/loading: skeleton with book identity if known. Missing source: metadata plus relink and unavailable-read explanation. Password protected: explicit unlock route. Long title/multiple authors: readable wrapping. Enrichment conflict/manual override: review state. Unsaved edit: save/discard decision. Offline provider: local reading unaffected. Source changed/writeback conflict: safe refusal and recovery.

## Acceptance

- Peter approves the actual before/after inspector using identical realistic content in Light/Dark, comfortable/compact and supported OS scaling.
- Close, Escape and route change dismiss correctly; catalogue width and focus restore; no blank inspector on fresh launch.
- Read/Resume is visible without scrolling at all supported panel sizes; advanced controls do not compete with it.
- Every previous information group remains findable through task tests; provenance/backup controls are not lost to simplification.
- 20 rapid selections with delayed responses never display or mutate the wrong book; selected identity matches every action.
- Native Windows/Mac and keyboard/screen-reader tests cover tabs/sections, scroll, unsaved changes, loading and errors.

## Recovery

Keep the view redesign separate from metadata-write semantics. Roll back individual presentation components if they hide critical information; retain the visibility fix. No schema migration should be needed solely to reorganize this panel. Re-measure book-identification time, read-action discovery and mistaken edits in phase19.
