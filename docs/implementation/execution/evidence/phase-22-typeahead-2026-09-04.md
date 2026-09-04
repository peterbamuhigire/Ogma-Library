# Phase 22 type-ahead evidence

Date: 2026-09-04

The global search view debounces keystrokes for 150 ms, cancels the previous
request, ignores out-of-order results, and preserves selected-result navigation.
The Avalonia test `SearchViewModel_QueryDebouncesAndOpenSelectedNavigates`
passed 1/1 on 2026-09-04.

This closes the type-ahead service/UI gate only. Facets, paging, highlighting,
correction suggestions, full-text fallback integration, and the 50,000-book
benchmark remain open.
