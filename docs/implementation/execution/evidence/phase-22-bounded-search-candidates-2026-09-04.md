# Phase 22 bounded exact-search candidates evidence

Date: 2026-09-04

`MetadataSearchService` now caps the exact-path candidate window at 1,000 rows
before loading related authors, metadata fields, and shelves. SQL ordering gives
exact title/identifier matches and title prefix/contains matches priority before
the deterministic title/book-ID tie-break. The existing scorer and final 50-result
contract remain unchanged over that bounded window.

This is a bounded-materialization safeguard, not a substitute for the required
50,000-book benchmark or full indexed/faceted paging design.
