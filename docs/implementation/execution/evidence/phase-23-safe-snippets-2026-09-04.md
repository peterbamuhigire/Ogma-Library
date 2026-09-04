# Phase 23 safe snippets and page-jump evidence

Date: 2026-09-04

FTS results now carry a plain-text `Snippet`, structured highlight spans, and a
`SearchPageJumpTarget` only for page-derived chunks with a valid zero-based
page index. The UI binds the plain-text value, while clients that render
highlights can use the bounded spans without interpreting markup.

Verification:

- `SearchSnippetParserTests`: 2 passed.
- `FtsIndexServiceTests`: 8 passed, including plain-text snippet and page-jump
  assertions.

Remaining Phase 23 gates cover full-text UI mode/reader journeys, no-index and
progress states, observability, side-by-side rebuild swap, and 50,000-book
latency evidence.
