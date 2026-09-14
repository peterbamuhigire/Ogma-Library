# Phase 12: semantic retrieval with measurable relevance

Status: planned. Owner: retrieval engineer; reviewer: independent librarian/relevance assessor.
Dependencies:10/11. Estimate:6-10 person-days. Findings:F15/F16.
Requirements:SEARCH004/005, AI006; old phases25/26/28.
Skills:E2 RAG/E3 evaluation/E4 testing/R1/R2; [routes](../06-skills-sources-and-kaizen.md).

## Outcome

Natural-language retrieval finds relevant local books beyond literal matching while preserving permission, source freshness and a useful non-AI fallback. Ranking quality is demonstrated on a held-out corpus.

## Work slices

1. Inspect embedding generation/vector repositories, semantic search, hybrid ranking, frozen retrieval contract and existing evaluation fixtures. Record dimensions/model/version/chunk policy and all filter boundaries.
2. Assemble a licensed representative corpus and human-authored queries spanning school subjects, reader intent, edition ambiguity, multilingual terms and no-answer cases. Separate tuning, regression, adversarial and held-out sets.
3. Compare exact/fuzzy/FTS baseline with current semantic/hybrid ranking using the same corpus and queries. Preserve deterministic filters and permission restrictions before retrieval and before display.
4. Test stale/deleted/source-changed embeddings, model/dimension changes, interrupted rebuild and generation promotion. Prefer existing bounded scanning until measured latency/memory/relevance justifies ANN complexity.
5. Expose match type and source. A semantic similarity score is not probability of truth; UI labels must distinguish relevance from answer confidence.
6. Define model/index rollback and offline fallback. A missing local embedding model must not disable ordinary search or trigger undisclosed cloud calls.

## Acceptance

- Frozen retrieval-contract metrics meet their recorded thresholds on the held-out set; report Recall@k/nDCG or the chosen existing measures, with denominator, adjudication, corpus/model hashes and failure slices.
- Hybrid improves or preserves agreed primary relevance measure against lexical baseline without safety, subgroup, latency or memory regression.
- Zero unpublished/unauthorized/profile-private items in retrieval outputs or snippets under adversarial scope tests.
- Deleted/stale generation records are excluded; rebuild survives restart and promotes atomically; old index can be restored.
- 2k/50k benchmarks record cold/warm behavior, p95, peak memory, native hardware and quality tradeoff. Synthetic vectors alone do not close real-book quality.

## Recovery and review

Keep model/index versions and old generations until validated. Disable semantic ranking and fall back to lexical when source/version/integrity is uncertain. Independently review sample labels and citation targets; do not use the same generated examples to tune and certify relevance. Re-measure after any corpus, chunker, model or ranking change.
