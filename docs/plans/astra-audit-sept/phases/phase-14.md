# Phase 14: useful Advisor, local evidence and reading plans

Status: planned. Owner: AI/product lead; reviewer: independent librarian/teacher.
Dependencies:08/12/13. Estimate:6-10 person-days. Findings:F08/F15/F16.
Requirements:AI003/007/008 plus UX; old phases28-30.
Skills:E2/E3/D2/D8/R1/R2; [routes](../06-skills-sources-and-kaizen.md).

## Outcome

AI enhances reading decisions without competing with basic navigation or overstating what the system knows. Recommendations reference available local books, evidence links work and reading plans are educationally useful.

## Work slices

1. Simplify entry points around explicit tasks: find a suitable book, inspect local evidence, build a reading plan. Use current library/collection scope visibly. Separate local extractive answers from optional generative synthesis.
2. In RecommendationPanelViewModel/ReadingPlanViewModel, connect capability state to button enablement, explanation and setup action. Replace misleading ready states when provider use is disabled.
3. Preserve editable interpreted intent, filtering and cancellation. Make result cards explain why a book fits using actual metadata/content evidence, with edition/availability and Read/Inspect actions.
4. Audit LocalEvidenceAnswerPipeline source labels, page/chunk references and score presentation. Do not call concatenated snippets a complete answer or a ranking score truth confidence. Clearly distinguish a reader note from book text.
5. Reading plans retain ordered local books, rationale, difficulty/checkpoints and user-editable sequence. Do not invent content coverage or completion estimates unsupported by available evidence.
6. Version prompts/model/corpus/system policy and evaluate independently. Collect feedback only with consent and retention policy; show fallback when evidence is absent or provider fails.

## Failure cases

No suitable book; no indexed content; unsupported question; prompt injection in title/PDF/note; invented book/page; stale/deleted source; conflicting editions; provider timeout/rate limit; cancellation after partial output; model change; insufficient classroom quota.

## Acceptance

- Every recommendation and plan step references a current permitted local book; unavailable books are honestly labeled.
- Every citation opens the exact source/page or explicitly states the source has no page target.
- Held-out claim support and relevance meet phase01/12 contracts; independent reviewers assess helpfulness and abstention, not just valid JSON.
- Unsafe/injected document text cannot change privacy or tool permissions; unsupported questions abstain without fabricated sources.
- Disabled/offline/timeouts present useful local alternatives; cancellation preserves previous results and reading context.
- User tests demonstrate value over ordinary search for intended tasks and comprehension of local/cloud/extractive boundaries.

## Recovery

Retain previous prompt/model/index profile and evaluation evidence. Roll back or disable the failing enhancement on grounding/privacy regression while preserving library search and reading. Review failures by subject/language/role; aggregate score cannot hide a harmful subgroup or citation failure.
