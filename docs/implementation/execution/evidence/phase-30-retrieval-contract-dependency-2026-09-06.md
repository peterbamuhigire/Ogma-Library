# Phase 30 Retrieval-Contract Dependency Closure

Date: 2026-09-06

Phase 30 consumes the semantic/hybrid retrieval boundary owned by Phase 26.
That upstream boundary is now frozen and executable at v1:

- semantic response: `semantic-search-v1`;
- metadata/full-text fusion: `rrf-v1`;
- hybrid ranking: `hybrid-v1`; and
- offline evaluation: `search-retrieval-evaluation-v1`.

The semantic DTO shape and default versions are guarded by
`Phase26SearchContractFreezeTests`; hybrid, fallback, evaluation, and combined
search behavior passed the associated 19-test slice. The authoritative proof
is [phase-26-search-contract-freeze-2026-09-06.md](phase-26-search-contract-freeze-2026-09-06.md).

This closes only Phase 30's dependency on a final retrieval contract. It does
not close quarantined live-provider evaluation, human-labeled quality evidence,
full-shell accessibility/keyboard evidence, or physical file-picker evidence.
