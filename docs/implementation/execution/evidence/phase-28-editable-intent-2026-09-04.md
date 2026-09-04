# Phase 28 editable interpreted intent evidence

Date: 2026-09-04

The advisor query editor is explicitly two-way bound. `RecommendationPanelViewModel`
reparses the current query on every edit and updates the interpreted topics,
exclusions, length, and difficulty before any recommendation request is sent.

Verification: `Phase30AdvisorQualityTests` passed the editable-intent regression,
and the existing advisor rendered-route test covers the visible intent panel.

Remaining Phase 28 gates include reference-book resolution beyond the deterministic
comparison hint, source-labeled evidence assembly, human-labeled quality
thresholds, and physical accessibility/performance evidence.
