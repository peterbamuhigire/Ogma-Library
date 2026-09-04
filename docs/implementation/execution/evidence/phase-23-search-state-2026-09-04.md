# Phase 23 search progress and no-index evidence

Date: 2026-09-04

Semantic search now returns an explicit availability state: ready, no matches,
no local semantic index, or degraded exact fallback. The desktop search panel
renders `StatusText` as a visible polite status line, so a user can distinguish
an unbuilt local index from a query with no matches and from provider degradation.

Verification: the semantic-service suite passed 5/5 and the Avalonia
`SearchViewModelTests` suite passed 14/14, including the no-index state and the
existing debounce, stale-result, fallback, navigation, and rendering checks.

Physical screen-reader, magnifier, and cross-platform walkthrough evidence is
`NOT ASSESSED`; side-by-side rebuild swap and 50,000-book performance remain
open Phase 23 gates.
