# Phase 11: inspector header and primary-action measurement

Measured 2026-11-20 (slice 8) on head `3ffac4a8`, from the UIA bounds of the real window
(run `p11-final3`, Light and Dark, synthetic corpus, fresh data directory). Only element
identifiers and rectangles are used; no book content is recorded.

Method: header plus segments = bottom of `Detail.Section.Overview` minus the top of `Detail.Panel`;
divided by the height of `Detail.Panel`. The panel is 360 px wide (default width).

| State | Window | Panel height | Header plus segments | Share | Target |
|---|---|---|---|---|---|
| Ready (title, authors, year) | 1280×800 | 720 px | 271 px | **37.6 %** | ≤ 18 % (missed) |
| Ready (title, authors, year) | 1920×1080 | 1000 px | 271 px | **27.1 %** | ≤ 18 % (missed) |
| Ready (short header) | 1920×1080 | 1000 px | 252 px | 25.2 % | ≤ 18 % (missed) |
| Missing file | 1920×1080 | 1000 px | 412 px | 41.2 % | ≤ 18 % (missed) |

Before (`docs/plans/sept-23-kaizen/evidence/screens/j3-detail.png`, 1600×1000, pixel reading,
HEURISTIC): the title row, cover and seven tabs end at about y 430 in a panel spanning about
y 31 to 965, so about **43 %**. The plan's own estimate was about 35 %.

Result: improved (43 % → 38 % at the smallest reference size), but the ≤ 18 % target is
**not met**. Cause: the cover block, a two-line Spectral title, authors, the primary action, and
the status, rating and favourite row (D-57) all stack above the segments. Registered as K131.

## Primary-action candidates

| Measure | Before | After | Evidence |
|---|---|---|---|
| Filled (primary-styled) actions in the inspector | 3 competing (*Read* in the header, plus *Read*, *Enrich* and *Run OCR* as coloured footer buttons) | **1** (`Detail.Action.Read`, the only `Classes="primary"` button in `BookDetailView.axaml`) | `j3-detail.png`; `BookDetailView.axaml:114`; `Phase11InspectorStructureTests` (headless) |

The primary action is the state-driven one (*Read*, *Continue reading · p. N*, *Locate file…*,
*Unlock*); secondary actions are in the `Detail.Action.More` overflow menu.
