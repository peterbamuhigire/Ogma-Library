# Phase 09: annotations, citations, reading memory and portability

Status: planned. Owner: reader/data engineer; reviewer: researcher/librarian QA.
Dependency:08. Estimate:6-10 person-days. Findings:F10/F12.
Requirements:FR-READ-007/008/011/013/014/015; applicable EXT import/export decisions.
Skills:E1/E4/D2/D4; [routes](../06-skills-sources-and-kaizen.md).

## Outcome

A reader can select meaningful text, highlight or note it, revisit it and export useful study material without changing the PDF or losing context. Database persistence alone is not completion.

## Work slices

1. Trace pointer/keyboard selection through TextLayerService, annotation coordinate transforms, repository persistence and overlay rendering. Use deterministic text/geometry fixtures and real PDF text layers.
2. Design a small contextual selection toolbar and one optional study rail. Move layer management and JSON portability out of the primary navigation row. Keep bookmark, note and citation actions discoverable by keyboard.
3. Make note editing, deletion/undo, color choice, layer create/rename/show/hide/merge and per-layer export explicit. Prevent active-page changes from saving a note to a different location.
4. Verify citations include book identity, author/title, page and selected text. Separate copy-to-clipboard from file export, expose format choice and explain missing bibliographic values.
5. Complete supported BibTeX/RIS/CSL JSON/Markdown outputs against independent parsers or downstream consumers. Bind export to selected items/layers, not current UI list order.
6. Validate import schema/version/size, duplicate handling and wrong-book mapping. Present preview and conflict choices; never silently overwrite private notes. Persist reading memory/outcome and progress per identity/profile.

## Hard cases

Rotated pages, nonstandard crop boxes, multi-line/multi-page selection, ligatures, Unicode and right-to-left text; image-only pages; deleted/moved PDFs; unsaved note during navigation; concurrent session changes; crash during persistence/export; malformed or oversized imported JSON; duplicate imported annotation IDs.

## Acceptance

- Select -> highlight/note/bookmark -> close -> reopen restores exact text, intended page/region and layer on Windows/macOS.
- Rotation/zoom/layout changes do not move annotations onto unrelated text; document original hashes remain unchanged.
- Clipboard and each promised export format include the correct selected content and parse independently; unsupported formats are explicitly unavailable.
- Import preview and conflicts are deterministic; repeat import does not duplicate accepted objects unexpectedly.
- Reading memory and progress remain scoped to book/profile; two students cannot read each other's notes.
- All study functions have keyboard equivalents and meaningful announcement; no color-only annotation identity.

## Recovery and remeasurement

Back up annotation stores before schema/coordinate changes; retain coordinate version and transform migration evidence. Roll back a migration on mismatch and preserve the original annotations for recovery. Re-measure citation/task completion and reopen accuracy in phase19 and crash drills in20.
