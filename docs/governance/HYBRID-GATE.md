# Hybrid Validation Gate

The Hybrid (Water-Scrum-Fall) methodology requires that the signed SRS baseline
is in place before Agile delivery artifacts are generated, and that the chain
between the Waterfall baseline and the Agile build stays intact. The validation
gate enforces this.

## The command

```
python -m engine validate Ogma-Library
```

Run from the workspace that hosts the project engine. The gate must exit `0`
before opening a pull request that produces Phase-07-class (build/delivery)
outputs, and it runs in CI.

## What it checks (intent)

- The SRS baseline exists and is signed before downstream build artifacts.
- Requirement IDs referenced by stories/PRs resolve to baselined identifiers.
- The traceability chain (vision → PRD → SRS → build) has no dangling links.
- No Phase-07 output is generated ahead of the baseline sign-off.

## Status in this repository

As of Phase 00, the **engine that implements this gate is external** to this
application repository (it lives in the documentation/SDLC engine workspace).

- If the engine is available on the contributor's machine, run the command above
  and confirm exit `0`.
- If it is **not** installed locally, this is recorded as a tracked item: the
  gate is wired into CI where the engine is present, and local contributors rely
  on the CI run. Do not fake a local pass.

This document is the canonical reference for the gate's purpose and when it
blocks; operational steps live with the engine.

## Tracked item

- [ ] **TRACK-P00-GATE:** Confirm `python -m engine validate Ogma-Library`
  availability in CI and document the exact invocation path. Owner/engine
  maintainer to confirm. Until confirmed, the gate is enforced in CI only.
