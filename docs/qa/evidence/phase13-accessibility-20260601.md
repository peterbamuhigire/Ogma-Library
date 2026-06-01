# Phase 13 Accessibility Evidence

Date: 2026-06-01
Scope: AI recommendation panel and reading-plan panel.

## Checks

| Surface | Evidence |
| --- | --- |
| Recommendation query field | `AutomationProperties.Name` is bound to the localized query prompt. |
| Recommendation load button | `AutomationProperties.Name` is bound to the localized load label. |
| Recommendation cards | Each card exposes an accessible label containing rank, book id, and confidence band. |
| Recommendation provenance | Provenance is hidden behind a toggle and remains text-readable when expanded. |
| Reading-plan goal field | `AutomationProperties.Name` is bound to the localized goal prompt. |
| Reading-plan generate button | `AutomationProperties.Name` is bound to the localized generate label. |
| Reading-plan steps | Each step exposes an accessible label containing rank, resolved title, and difficulty band. |
| Reading-plan checkpoints | Each checkpoint exposes its description as the automation name. |
| Error state | Disabled-AI and parse errors remain in view-model state and render as text, not silent failures. |

## Verification

- `AdvisorViewRenderTests` headless-renders the recommendation and reading-plan
  views.
- `AdvisorDisabled_CatalogueBrowse_Unaffected_InUiLayer` verifies the disabled
  AI state is recoverable and leaves the panel interactive.

## Manual Note

No live screen-reader session was available in this unattended run. The committed
evidence covers keyboard-reachable controls, automation names, renderability, and
error-state text. A narrated screen-reader walkthrough should be repeated before
public binary release packaging.
