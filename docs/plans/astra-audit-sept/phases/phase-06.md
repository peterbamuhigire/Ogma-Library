# Phase 06: catalogue browsing and collections

Status: planned. Owner: catalogue/desktop engineer; reviewer: librarian and reader QA.
Dependencies:04/05. Estimate:5-8 person-days. Findings:F04/F13/F29.
Requirements:CAT family; old phases09/16/19/20. Skills:E1/D1/D2; [routes](../06-skills-sources-and-kaizen.md).

## Outcome

Users identify books by cover/title/author, change views, organize collections and return to their position in large libraries. Backend identity remains authoritative.

## Work slices

1. Inspect `CatalogueViewModel`, filter/view-state services, `CatalogueReadModel`, grid/list/directory views and ShelfSidebarViewModel. Establish one selection and query contract shared by all views.
2. Redesign cover cards around title/author and meaningful reading/availability state. Put detailed processing badges in contextual status or inspector rather than crowding fixed230px cards. Test missing cover and long/multilingual titles.
3. Review paging and actual realization. Current WrapPanel is not virtualized, but result paging bounds its workload. Benchmark then choose a virtualizing/adaptive implementation if needed; do not make an unsupported50k-card performance claim.
4. Make list a useful dense alternative with sortable metadata and keyboard selection. Directory view reflects canonical root-relative locations without implying that virtual collections move files.
5. Make Create collection a deliberate action and rename/delete contextual. Use clear confirmation/undo semantics; collection deletion must not delete PDFs. Define Favorites, Reading, Finished and smart-shelf behavior against actual services.
6. Persist view/sort/filter/collection/scroll and preserve focus across page loads. Bulk actions operate on explicit selection with counts, preview and partial-failure results.

## Acceptance and failure cases

- Grid/list/directory display the same filtered identity set and selection; no duplicate work/edition confusion without an explicit grouped representation.
- 20 rapid filter/sort/page changes cannot show stale results from a previous request; current selection remains valid or is clearly reset.
- Create/rename/delete and membership survive restart; deleting a collection changes no source-file hash.
- Empty/no-match/loading/offline/missing-cover/missing-file states have distinct explanations and actions.
- Keyboard selects/opens a book, changes view and performs supported collection operations; assistive names describe title and state.
- 2k and 50k read-model/page-load benchmarks identify hardware, sample runs, p95, memory and realized controls. Relevant canonical thresholds hold.

## Recovery and measurement

Keep UI paging changes independent of schema. Snapshot persisted view state and collection membership before migration; preserve old names/IDs or map explicitly. Roll back if identity counts, membership or reading context change unexpectedly. Measure find-book time, erroneous selections, frame stalls and return-context loss before/after.
