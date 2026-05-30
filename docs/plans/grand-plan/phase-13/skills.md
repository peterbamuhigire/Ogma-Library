# Phase 13 — Skills & Slash Commands

---

## Primary skills

### `ai:ai-rag-patterns`

- **Tasks:** P13-WP2-T2..T7 — recommendation pipeline design.
- **Why:** The pipeline is a RAG (Retrieval-Augmented Generation) pattern: local
  catalogue as the corpus, metadata enrichment as the retrieval step, AI provider
  as the ranker/explainer. The skill provides proven patterns for retrieval,
  context construction, and response grounding.
- **Artifact:** `CatalogueReader`, `MetadataEnricher`, `RecommendationResponseParser`
  designed per RAG best practices; anti-hallucination via `ProvenanceValidator`.

### `ai:ai-evaluation`

- **Tasks:** P13-WP9-T1..T4 — evaluation harness.
- **Why:** VERIFIABILITY-FAIL requirements need a principled evaluation framework
  that separates structural gating from quality assessment.
- **Artifact:** Eval script, query fixtures, benchmark JSON, and eval README.

### `ai:ai-output-design`

- **Tasks:** P13-WP7-T3, P13-WP8-T3 — recommendation card and reading plan layout.
- **Why:** AI-generated content requires specific display conventions: confidence
  visual encoding, explanation scaffolding, provenance citations — all per
  "AI output design" best practices.
- **Artifact:** Annotated design rationale in `RecommendationPanelView.axaml`
  and `ReadingPlanView.axaml`.

### `ai:ux-for-ai`

- **Tasks:** P13-WP7-T4, P13-WP8-T4 — confidence badges, difficulty badges, UX.
- **Why:** Confidence and difficulty must be displayed honestly without causing
  confusion or decision paralysis. Explainability UX ("Why?") must feel natural.
- **Artifact:** UX rationale comment in `RecommendationCardViewModel` and
  `PlanStepViewModel`.

### `ai:ai-feature-spec`

- **Tasks:** P13-WP4-T2, P13-WP2-T4 — prompt templates.
- **Why:** Structured prompt design for recommendation and reading-plan generation;
  output schema specification; temperature and max-token tuning advice.
- **Artifact:** `prompts/recommendation.txt`, `prompts/reading-plan.txt` with
  inline spec comments.

### `claude-api`

- **Tasks:** P13-WP4-T4 — Anthropic reading-plan call with prompt caching.
- **Why:** The large book-metadata context block sent with reading-plan requests
  benefits from ephemeral caching; same approach as Phase 12 `AnthropicProvider`.
- **Artifact:** `ReadingPlanAnthropicAdapter` using `cache_control: ephemeral`
  on the candidate-metadata block.

### `frontend-design:frontend-design`

- **Tasks:** P13-WP7-T3, P13-WP8-T3 — Avalonia views.
- **Why:** Recommendation cards and reading-plan steps must meet the premium
  calm-control aesthetic; confidence badges and difficulty chips use design tokens.
- **Artifact:** `RecommendationPanelView.axaml`, `ReadingPlanView.axaml` referencing
  `AVALONIA-STANDARDS.md` conventions.

---

## Always-on skills

| Skill | How applied |
| --- | --- |
| `superpowers:test-driven-development` | Structural oracle tests written before implementations |
| `superpowers:verification-before-completion` | `dotnet test` + eval mock run before claiming any WP done |
| `superpowers:requesting-code-review` + `/code-review` | After each WP; WP11 final review |
| `superpowers:systematic-debugging` | Any structural-validator test failure |
| `documentation-generation:docs-architect` | HLD §7 update after WP6 |

---

## Slash commands

| Command | When |
| --- | --- |
| `/code-review` | P13-WP11-T4, after each WP merge |
| `/verify` | After WP7 and WP8: run app to confirm recommendation panel and reading-plan view render on Windows and macOS |
| `/simplify` | After WP2 pipeline and WP6 composition |
