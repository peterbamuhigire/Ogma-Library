# ADR 0020: Unified search contract for the Search destination

## Status

Accepted on 2026-09-25 (Sept-23 Kaizen Phase 13). Semantic availability follows the
owner-proxy decision D-05: the semantic provider seam stays, and semantic search is
optional.

## Context

The audit query battery (defects K40, K41 and K14 in
`docs/plans/sept-23-kaizen/03-defect-register.md`) found that the Search destination
used only `ISemanticSearchService`. That service fell back to exact search through
`ICombinedSearchService`, and three problems followed:

- Multi-word queries became one adjacent FTS5 phrase, so `dhow Zanzibar` and
  `Chwezi dynasty` found nothing.
- Results showed `Books.Title`. A scan never fills that column, because titles live
  in PDF metadata fields, so the audit saw *Untitled* results or no results for
  titles, authors and `Wanjirũ`.
- The panel never used typo tolerance or `field:` queries.
- The mode chip said "Semantic search active" with no provider. The provider probe
  (an HTTP call to a closed port) ran on every query.

The Aug-39 Phase 26 contracts (`semantic-search-v1`, `rrf-v1`, `hybrid-v1`) are
frozen, and the classroom Host and other callers still use them.

## Decision

1. Add a new versioned contract, **`IUnifiedSearchService`**
   (`unified-search-v1`), in Application. `UnifiedSearchService` in Infrastructure
   implements it, and the composition root binds it. The frozen v1 contracts stay
   unchanged for their callers.
2. **Pipeline:**
   - `SearchQueryParser` handles bare words, quoted phrases, `field:value` for
     title, author, isbn, year, tag, shelf, text, note and toc, and `-`
     exclusion.
   - Structured filters restrict all result lists.
   - Metadata matching folds case and diacritics (NFKD). Every word must match,
     prefixes count, and typo tolerance is a fallback with edit distance 0/1/2 by
     word length. It matches over titles resolved like the catalogue projection
     (title, then the best metadata title, then the file name), authors, ISBN,
     tags, shelves and the file name.
   - FTS5 requires all words in any order. The last word is a prefix, quoted text
     stays a phrase and exclusions become `NOT`. Every token is quoted, so user
     input cannot inject FTS5 syntax. Each book contributes at most 3 hits.
   - Semantic results are included only when the provider probe answers. The probe
     is cached for 60 s, and a query waits at most 250 ms for it.
   - Reciprocal rank fusion (k = 60) produces one result per canonical book id.
3. **Honest availability.** Each response reports `SemanticSearchState`
   (Unavailable, Preparing, Active or NotApplicable), whether typo tolerance was
   used, and index coverage. The UI derives its mode chip and status text from
   the response.
4. **Tokenizer.** `unicode61 remove_diacritics 1` already folds single-mark Latin
   letters such as `ũ` and `É` for the index and the query alike (verified by the
   oracle), so no tokenizer migration or index rebuild is needed. Metadata folding
   is done in managed code.
5. **Performance.** The FTS5 rank window is bounded before joining, and snippets
   are fetched with `+rowid IN (…)` so SQLite does not re-run the MATCH for every
   id. The folded catalogue snapshot is cached until a change signature of the
   catalogue tables differs, or for at most 20 s.

## Consequences

- The search panel, the relevance oracle and the G4 journey all use one pipeline.
  Classroom host search (CLIENT-007) can move to this contract in Phase 20.
- `FtsIndexService` now uses AND semantics for every caller. This is intended: the
  old phrase behaviour was the K40 defect.
- An edit that leaves the change signature unchanged (for example, a same-length
  text change with the same first and middle characters) can take up to 20 s to
  appear in search.
- The metadata snapshot is held in memory: about 2,000 books loaded in 200 ms and
  cached. The 50k-book budget is Phase 24.
