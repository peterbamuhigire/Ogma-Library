# Phase 16: Reading Advisor, grounded answers and reading plans

## 1. Header

| Field | Value |
|---|---|
| Wave | D, Search and AI |
| Size | 7–10 engineering days |
| Depends on | Phase 14 (local semantic retrieval), Phase 15 (gateway, provider profiles, persisted privacy tier, mounted Privacy Center) |
| Owner decisions | D-05 (local semantic engine), D-06 (providers and exact model IDs) |
| Primary defects | K50 |
| Requirements | AI-003 (P), AI-007 (P), AI-008 (W, extractive only), AI-004 (B), AI-005 (B), AI-010 (B), SEARCH-004 (P), SEARCH-005 (P) |

## 2. Why this phase exists

The Advisor is the product's headline feature, and today it cannot produce anything. In the
audit, pressing **Recommend** returned "AI advisor unavailable: AI features are disabled", and
**Ask locally** stayed disabled (K50, MEASURED). The causes are structural:

- `AdvisorService` throws `AiDisabledException` whenever the privacy tier is `Offline`, which is
  the default (`src/OgmaLibrary.Application/Ai/AdvisorService.cs:31-67`). As a result the offline
  `DeterministicAdvisorFallback` (`src/OgmaLibrary.Infrastructure/AI/Advisor/DeterministicAdvisorFallback.cs`,
  called from `RecommendationPipeline.cs:106-124`) is never reached.
- The view models hard-code `"openai"`/`"gpt-test"`
  (`src/OgmaLibrary.App/ViewModels/Ai/RecommendationPanelViewModel.cs:376`,
  `src/OgmaLibrary.App/ViewModels/Ai/ReadingPlanViewModel.cs:155`).
- `LocalEvidenceAnswerPipeline` (`src/OgmaLibrary.Infrastructure/AI/Advisor/LocalEvidenceAnswerPipeline.cs:43-70`)
  returns "No matching local evidence" by default, because page evidence needs content consent
  that no UI records. Its answer text is hard-coded English.
- `RecommendationPanelViewModel.cs:419-435` and `ReadingPlanViewModel.cs:156-201` mutate bound
  state off the UI thread (K72), so the panel can crash the app.

Phase 15 makes providers, consent and the tier real. This phase makes the Advisor useful **with
or without a provider**, grounded in the user's own library, safe against hostile PDF text, and
measured by an evaluation set rather than by opinion.

## 3. Objectives and exit criteria

1. **The offline path works.** With no provider configured and the tier at Offline, *Recommend*
   returns ranked local recommendations from the deterministic fallback plus local semantic
   retrieval (Phase 14), labelled "Offline suggestions". The user never sees an exception message.
2. **The provider path works.** With a provider profile configured (Phase 15), recommendations
   and answers go through `AiGateway`. The model comes from the profile; there are no hard-coded
   provider or model strings anywhere in `src` (checked by grep in CI).
3. **Grounded answers.** Every answer sentence that states a fact about a book carries a citation
   (book, page). Clicking a citation opens the reader at that page. If the evidence is
   insufficient, the answer abstains ("I could not find this in your library") instead of guessing.
4. **Reading plans.** *Generate* produces a plan of 3–10 steps. Each step names a book in the
   catalogue, a page range or chapter (from the TOC where one exists) and a reason. Plans can be
   saved, reopened and deleted, and they work offline in a reduced deterministic form (ordered by
   topic match, then length).
5. **Prompt-injection defence.** Text extracted from PDFs is treated as untrusted data. An injection
   corpus of at least 15 hostile PDFs (instructions embedded in page text, metadata and TOC titles)
   produces no instruction-following, no out-of-library citations and no egress beyond the previewed
   payload.
6. **Evaluation gate.** A golden set of 30–50 cases runs offline in CI, with a fixed corpus and
   frozen expected outputs. Thresholds: citation precision ≥ 0.9; faithfulness ≥ 0.7, with an alert
   and a failed gate below it; correct abstention on ≥ 90 % of unanswerable cases;
   recommendation hit@5 ≥ 0.7 on labelled intents.
7. **Budgets.** Offline recommend p95 ≤ 1.5 s at 2k books on the reference machine. Provider answer
   time-to-first-token is displayed; there is a 30 s hard timeout with a retry option. The estimated
   cost is shown before sending, and the actual cost afterwards (Phase 15 price table).
8. **Output design passes.** AI output follows the design engine's AI-output patterns: disclosure
   label, source list, confidence wording, edit-and-retry, copy, and feedback (thumbs plus reason,
   stored locally only with consent).

## 4. Skills to load before starting

- `C:\wamp64\www\chwezi-dev-engine\skills\ai\ai-rag-patterns\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\ai\ai-security\SKILL.md` (prompt injection, untrusted context)
- `C:\wamp64\www\chwezi-dev-engine\skills\ai\ai-evaluation\SKILL.md` (golden set, release gates)
- `C:\wamp64\www\chwezi-dev-engine\skills\ai\ai-model-gateway\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\ai\ai-observability-and-debugging\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\ai\ai-cost-and-metering\SKILL.md`
- `C:\wamp64\www\chwezi-dev-engine\skills\ai\ux-for-ai\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\04-web-and-ui-design\ai-output-design\SKILL.md`
- `C:\wamp64\www\design-system-skills\skills\10-content-design-and-ux-writing\error-empty-and-system-messaging\SKILL.md`
- `C:\wamp64\www\srs-skills\05-testing-documentation\04-ai-eval-harness-spec\SKILL.md`
- `C:\wamp64\www\srs-skills\06-deployment-operations\10-ai-hallucination-slo-doc\SKILL.md`
- `C:\wamp64\www\digital-research-engine\docs\continuous-improvement\kaizen-currentness-gate.md`
  (re-confirm the model IDs selected in Phase 15 before freezing the evaluation baseline)
- `C:\wamp64\www\chwezi-dev-engine\skills\frontend-ux\avalonia-desktop-development\SKILL.md`

**Typeface decision:** answers use Public Sans (body), citations use Public Sans at the Small token
with the accent colour, and quoted page evidence uses Spectral italic. No new fonts.

## 5. Scope

**In scope:** `AdvisorService` tier handling, `RecommendationPipeline`, `DeterministicAdvisorFallback`,
`LocalEvidenceAnswerPipeline`, `AiPayloadBuilder`, the Advisor and Reading Plan views and view models,
citation navigation into the reader, plan persistence, the injection defence, the evaluation harness,
and AI telemetry (local, redacted).

**Out of scope:** provider onboarding and key storage (Phase 15); embedding engine selection (Phase 14);
classroom Student Smart Search (Phase 19, which reuses this phase's grounding contract); fine-tuning.

## 6. Work breakdown

1. **Tier-aware Advisor service.** Change `AdvisorService` so that `Offline` routes to the local
   pipeline instead of throwing. Only an explicitly *disabled* Advisor (Settings → Search and AI → off)
   yields a disabled state, and that state is a result value, not an exception.
   *Acceptance:* unit tests for each tier (Offline, MetadataOnly, ContentAware) × provider
   present/absent.
2. **Remove hard-coded model identity.** Replace `"openai"`/`"gpt-test"` in both view models with the
   active provider profile from `IAiProviderProfileService`. Add a CI grep guard that fails on literal
   provider or model IDs under `src/OgmaLibrary.App`.
3. **Local recommendation quality.** Combine the deterministic fallback with Phase 14 semantic
   neighbours and metadata (subject, author, shelf, reading state) via reciprocal rank fusion. Return
   a reason per item ("shares topic *Great Lakes history* with books you finished").
4. **Grounded answer contract.** Define `GroundedAnswer { Sentences[], Citations[], Abstained, Evidence[] }`.
   Each citation carries `BookId`, `PageIndex` and a snippet hash. A post-generation validator drops or
   flags sentences whose citation does not match retrieved evidence, and abstains when nothing survives.
   Reuse the same contract in `ClassroomAnswerGrounder` (Phase 19).
5. **Consent-aware evidence.** When the tier is ContentAware, page evidence flows only after the
   Phase 15 payload preview. When it is MetadataOnly, answers use metadata and TOC only and say so.
   Localise all answer templates (no English literals in `LocalEvidenceAnswerPipeline`).
6. **Citation navigation.** Clicking a citation calls `OpenReaderAsync(bookId, pageIndex)` on the UI
   thread, and the reader highlights the snippet if coordinates exist.
7. **Reading plans.** Add a plan model (`ReadingPlan`, `ReadingPlanStep`) persisted in SQLite through a
   migration, with list/open/delete in the Reading Plan view. Provide a deterministic offline generator
   and a provider-enhanced generator. Use TOC entries to propose chapter ranges.
8. **Prompt-injection defence.** Wrap PDF-derived text in delimited, labelled data blocks. Strip or
   escape control sequences. Instruct the model that document content cannot change instructions.
   Validate outputs against the allowed-citation set, and never let retrieved text trigger tool calls
   or egress. Build `tests/fixtures/ai-injection/` with ≥ 15 synthetic hostile PDFs using the Phase 01
   corpus generator.
9. **Evaluation harness.** Add `tests/evaluation/advisor/` with 30–50 cases over the synthetic corpus:
   recommend (intent → expected books), answer (question → expected cited pages), unanswerable
   (→ abstain) and injection (→ refuse or ignore). Provide a runner with a JSON report and thresholds
   from §3.6. Offline cases run in CI; provider cases run on demand and are recorded as
   `NOT ASSESSED` when no key is present.
10. **Threading and failure safety.** Marshal all Advisor and Plan view-model mutations to the UI
    thread (Phase 02 helper). Map every failure to a localised state: no provider, timeout, budget
    exceeded, consent declined, no evidence.
11. **Output design.** Show disclosure ("Generated by <provider/model> from your library" or
    "Offline suggestion"), numbered source chips, abstention styling, retry, copy and feedback, and the
    cost line. Apply the design-engine AI-output checklist and the empty, loading and error states.
12. **Telemetry.** Record local, redacted traces (stage timings, retrieval counts, abstention, citation
    validity; never page text) through the existing durable trace store, visible in Diagnostics.

## 7. Kaizen action rows

| Gap | Root cause | Change | Hypothesis | Measure (before → target) | Evidence | Risk | Rollback |
|---|---|---|---|---|---|---|---|
| Recommend always fails | Offline tier throws before the fallback | Tier routes to the local pipeline | Users get useful suggestions offline | Successful recommend runs 0 % → 100 % of golden recommend cases | Harness screenshot and eval JSON | Weak offline relevance | Keep the fallback behind a Settings toggle |
| Answers ungrounded or empty | No consent UI; no citation validator | Grounded-answer contract and validator | Hallucinated claims are filtered | Faithfulness n/a → ≥ 0.7; citation precision ≥ 0.9 | `advisor-eval-report.json` | Over-abstention | Tune the threshold per case class |
| Hard-coded model | Placeholder never replaced | Profile-driven model with a grep guard | No stale model IDs ship | 2 literals → 0 | CI log | Profile missing | Disabled state with a Settings link |
| Injection exposure | PDF text concatenated into prompts | Data blocks plus an output allow-list | Hostile text cannot steer output | Injection cases passed: unknown → 100 % | Eval JSON | False positives on real text | Log-only mode for one release |
| No plans | Plan generation always threw | Deterministic and provider plans, persisted | Plans become a daily habit | Plans generated 0 → any intent in the golden set | Screenshots | TOC absent | Fall back to whole-book steps |

## 8. Test plan

- **Unit:** tier routing; fusion ranking; citation validator (valid, mismatched page, fabricated
  book); abstention; plan generator determinism; payload builder never includes page text below
  ContentAware.
- **Headless UI:** Recommend, Ask and Generate with a fake provider and with none; the citation click
  navigates; state updates happen on the UI thread (assert via dispatcher checks).
- **Real-window (Phase 01 harness):** journey J6 "ask the Advisor about the Great Lakes kingdoms,
  open the cited page, create a plan, restart, reopen the plan" at 1280×800 and 1920×1080, offline and
  with the fake provider.
- **Negative:** provider timeout, HTTP 429, budget exhausted, consent declined, empty library,
  1-book library, corrupt TOC, injection corpus.
- **Evaluation:** the full golden set, with the report stored under
  `docs/implementation/execution/evidence/sept-23-kaizen/phase-16/`.

## 9. Acceptance commands

```powershell
dotnet build OgmaLibrary.sln --configuration Release --no-restore
dotnet test tests/OgmaLibrary.Tests --configuration Release --no-build --filter "FullyQualifiedName~Advisor|FullyQualifiedName~ReadingPlan|FullyQualifiedName~Grounded"
dotnet test tests/OgmaLibrary.Tests.Ui --configuration Release --no-build --filter "FullyQualifiedName~Advisor"
pwsh ./tests/evaluation/advisor/Invoke-AdvisorEvaluation.ps1 -Mode Offline -FailBelowFaithfulness 0.7
./tests/OgmaLibrary.Tests.E2E/Invoke-GoldenJourneys.ps1 -Journey G7
Select-String -Path src/OgmaLibrary.App/**/*.cs -Pattern '"gpt-|"openai"' ; if ($?) { throw 'hard-coded model id' }
```

## 10. NOT ASSESSED and external dependencies

| Item | Owner | Consequence until measured |
|---|---|---|
| Provider-backed evaluation with a real key (D-06) | Owner, to supply a test key | Provider path ships as beta; offline path is the default |
| Human-labelled held-out set (not tuned on) | Owner and two reviewers | Score ≥ 80 for wave D stays conditional |
| Reference-machine latency at 2k/50k books | Phase 24 | Budgets are local measurements only |
| Model currentness at release | Phase 28 re-audit | Re-run the currentness gate before beta |

## 11. Risks and mitigations

- **Offline relevance feels weak.** Show reasons, allow "more like this", and make it easy to add a
  provider from the empty state.
- **The validator removes too much.** Report abstention separately in the evaluation and tune per
  case class; never trade faithfulness for fluency.
- **Cost surprises.** Pre-send estimate, a daily cap (Phase 15) and a visible running total.
- **Threading regressions.** Headless tests assert dispatcher access; the Phase 02 global handler
  catches the remainder and logs it.

## 12. Execution prompt

```
## Prompt 16 - Make the Reading Advisor, grounded answers and reading plans work
You are implementing Phase 16 in C:\wamp64\www\Ogma-Library. Read these files first, in order:
1. C:\wamp64\www\Ogma-Library\CLAUDE.md
2. docs/plans/sept-23-kaizen/README.md
3. docs/plans/sept-23-kaizen/AGENT_BRIEF.md (invariants)
4. docs/plans/sept-23-kaizen/phases/phase-16-advisor-and-reading-plans.md
5. docs/plans/sept-23-kaizen/03-defect-register.md (K50, K72)
Then read the skills in section 4 (the engines are not registered with the Skill tool; read the SKILL.md files).
Confirm Phases 14 and 15 are COMPLETE in docs/implementation/execution/00-execution-status.md; if not, stop and report.
Work plan: A. tasks 1-2 serially (service contract), B. tasks 3-7 (pipeline, UI, plans), C. tasks 8-9 (injection, evaluation), D. tasks 10-12.
Write a failing test before each fix. Run the section 9 commands and the Phase 01 journey J6 at both reference sizes.
Record provider-dependent results as NOT ASSESSED when no key exists. A tool invocation, a generated file or an unexecuted test is not completion.
Finish with docs/implementation/execution/phase-sept23-16-completion.md and update the README status register.
```
