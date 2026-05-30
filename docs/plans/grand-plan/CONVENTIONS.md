# Phase Folder Conventions

Every `phase-NN/` folder is authored to this template so the plan reads as one
coherent document. Consistency is a feature: a contributor should be able to
open any phase and find the same sections in the same order.

## Files in every phase folder

| File | Required | Contents |
| --- | --- | --- |
| `README.md` | ✅ | The phase brief — the sections below, in order. |
| `tasks.md` | ✅ | Granular work breakdown: work packages → tasks, each with ID, description, dependencies, rough estimate, and the requirement/NFR/CTRL IDs it satisfies. |
| `skills.md` | ✅ | The skills & slash commands to use in this phase, *why*, and *when*. Mirrors `SKILLS-INDEX.md` but phase‑scoped with concrete invocation guidance. |
| `testing.md` | ✅ | The phase test plan: which of the 9 test layers apply, the fixtures, the deterministic oracles, and the phase's slice of the golden‑corpus/perf gates. |
| `icons.md` | ✅ for any phase with UI surface; otherwise a stub stating "no new UI icons" | The **icon manifest**: every new button/menu/surface icon, its meaning, the proposed style/color, size variants, and a **procurement checklist** to ask the owner to buy the premium PNGs. |
| `risks.md` | optional (fold into README if short) | Phase‑specific risks, mitigations, and the R‑tier of each. |

## `README.md` section order (mandatory)

1. **Title & one‑line mission** — `# Phase NN — <Name>` + a single sentence.
2. **Status & metadata** — tier(s) (MVP/V1/V2), estimate, owner, the original
   PRD build‑phase it maps to (if any), platforms (always Windows + macOS).
3. **Objectives** — 3–7 outcome statements (what is true when the phase is done).
4. **Scope** — *In scope* / *Explicitly out of scope* bullet lists.
5. **Requirements covered** — a table: `ID | Tier | Summary | Verified by`.
6. **Dependencies** — *Depends on* (prior phases/decisions) and *Unblocks*.
7. **Architecture & approach** — the design for this phase: components, bounded
   contexts touched, interfaces introduced, data/schema changes, and the
   cross‑platform (Win/macOS) approach. Reference HLD sections and ADRs by ID.
8. **Work breakdown (summary)** — the work packages (detail in `tasks.md`).
9. **Cross‑cutting checklist** — explicit ✅ lines for: colorful icons +
   manifest; i18n (en/fr strings externalized); accessibility (keyboard + SR);
   privacy/egress (if AI/network); reversibility (if destructive); performance
   budgets touched; bounded‑context tests; documentation.
10. **Definition of Done** — the global DoD (README §6) **plus** phase‑specific
    exit criteria, each a binary, verifiable statement.
11. **Skills to use** — a short pointer list (full detail in `skills.md`).
12. **Deliverables** — the concrete artifacts (projects, files, ADRs, docs,
    fixtures, benchmarks) this phase produces.
13. **Risks** — top risks + mitigations (or pointer to `risks.md`).
14. **Owner asks** — the explicit decisions/sign‑offs and **icon procurement
    requests** this phase needs from Peter.
15. **Change log** — dated entries for any revision to this phase folder.

## Writing standards

- **Traceability everywhere.** Cite requirement IDs (FR‑…, NFR‑…, CTRL‑OGMA‑…),
  ADRs (ADR‑000N), HLD sections, and golden‑corpus fixtures by name. A claim
  with no ID behind it is a smell.
- **Deterministic acceptance.** Prefer "verified by automated test X asserting
  oracle Y" over prose. Where a requirement is `VERIFIABILITY‑FAIL`, state the
  structural sub‑claim that *is* gated.
- **Cross‑platform first.** Every phase explicitly states how it stays green on
  **both Windows (WebView2) and macOS (WKWebView)**; native‑interop and WebView
  differences are called out.
- **Tie skills to work.** When you name a skill, say which task it informs and
  what artifact it should produce. Don't list skills decoratively.
- **Calm, exact tone.** Match the reference docs: active voice, measurable
  thresholds, no marketing superlatives. "Fast/intuitive/seamless" without a
  number is rejected.
- **Markdown hygiene.** ≤ 120‑char lines where practical, ATX headings, fenced
  code blocks, tables for ID maps. Keep each file focused; link rather than
  duplicate (`SOURCE-SUMMARY.md` is the canonical digest).
- **ASCII by default** unless a glyph is already in the reference set.

## Icon manifest format (`icons.md`)

Each phase that adds UI lists icons in a table:

```
| Icon key | Used on | Meaning | Style/color note | Sizes | Status |
| --- | --- | --- | --- | --- | --- |
| ic_scan_library | Library toolbar | Start/rescan a library root | Outlined, oak‑amber accent | 16/24/32/48 @1x‑3x | ⬜ to procure |
```

Status values: `⬜ to procure` (ask owner) → `🟨 placeholder in use` → `✅ premium PNG wired`.
End every `icons.md` with an **Owner procurement request** block summarizing the
exact icon set to buy, the style tokens from `ICON-SYSTEM.md`, and the target
sizes/density for Windows + macOS (and HiDPI/Retina).

## Definition of Done — reusable checklist (copy into each README §10)

- [ ] Every in‑scope FR/NFR/CTRL ID has a passing test or a tagged gap.
- [ ] Golden‑corpus suite green; no open R1/R2 defect.
- [ ] `dotnet format --verify-no-changes`, `dotnet build` (warnings = errors),
      `dotnet test`, and architecture tests all pass.
- [ ] Builds and tests pass on **both Windows and macOS** CI runners.
- [ ] New user strings externalized and present in **en + fr**; pseudolocale CI
      check passes.
- [ ] Every new control has a colorful icon **and** an accessible label;
      keyboard + screen‑reader walkthrough passes; `icons.md` complete.
- [ ] ADRs/decisions recorded; reference docs updated; hybrid validation gate
      passes where applicable.
- [ ] Performance budgets touched are instrumented and within budget (or trend).
- [ ] `/code-review` (and `security-review` for security/privacy phases) done;
      findings resolved.
