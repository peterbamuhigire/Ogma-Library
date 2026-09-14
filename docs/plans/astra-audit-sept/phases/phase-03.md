# Phase 03: shell, navigation and capability-aware routes

Status: planned. Owner: UX lead/desktop engineer; reviewer: reader and librarian representatives.
Dependencies:02. Estimate:5-8 person-days. Findings:F03/F04/F06/F07/F15/F29.
Skills:E1, D2, D7 navigation, D8 states; [routes](../06-skills-sources-and-kaizen.md).

## Outcome

Users see where they are, find a book and reach supporting tools without decoding a toolbar of internal subsystems. The shell adapts to available space and current mode.

## Work slices

1. Inventory every visible destination and action in `CatalogueShellView.axaml`, `MainShellViewModel`, `ShellModule` and reader/AI/classroom views. Classify as primary destination, current-book action, background operation, settings or optional advanced tool. Record missing/inert routes.
2. Create low-fidelity layouts for Library, Reader, Search results, Settings/Privacy and School mode. Test first-use/find/read routes with representative users before styling. Keep one library search entry and separate in-document find.
3. Replace the fourteen-column toolbar with a constrained hierarchy: navigation/location, search, view/sort/filter, Add/Open and contextual actions. Use a real overflow menu when space is insufficient, with native keyboard semantics. Maintain minimum useful content width.
4. Make appearance, AI/privacy, library roots, processing/activity, help and school connection reachable. Do not require a command palette or environment variable to find ordinary settings. Keep diagnostics in an advanced section.
5. Gate destinations by actual mode, permission and runtime capability, not object existence. Standalone must not show a ready student-search action; unavailable Sharing must explain why or be omitted appropriately. Client role cannot expose host administration.
6. Persist only useful navigation state: selected collection/view/sort/filter and reader position. Route return restores context. Transient dialogs and error panels do not reopen accidentally.

## State and failure design

Provide no library, loading, empty results, stale/offline catalogue, unavailable source, permission-denied action, disabled AI, disconnected classroom and failed optional3D states. Loading cannot clear previously visible usable content unnecessarily. Back must have an explicit destination rather than navigate through internal toggles.

## Acceptance

- A route inventory has no advertised dead-end or orphan view; each mounted feature has a pointer and keyboard entry plus recovery/back path.
- No overlapping actionable bounds or clipped required controls at860x560,1180x760 and1600x900 client-area test sizes and OS scaling100/150/200%; adjust composition when effective width shrinks.
- Selected view/collection is visibly and programmatically selected; icon meaning is unambiguous and labeled.
- Find -> inspect -> read -> return preserves query/filter/scroll and selected book.
- Role/mode matrix verifies standalone, host, student, teacher/admin and offline states without granting extra permissions.

## Recovery and measurement

Separate shell navigation from domain changes. Preserve a tested legacy route behind an internal branch only while transitioning, not two competing public shells. Measure task steps, wrong-destination visits and assistance before/after. Reopen phase if phase07/08 reintroduces toolbar congestion.
