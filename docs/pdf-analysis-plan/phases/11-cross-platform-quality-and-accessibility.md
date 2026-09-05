# Phase 11 — Cross-platform quality, performance and accessibility

**Depends on:** Phases 2, 5–10; canonical phases 18–21, 37–39.
**Outcome:** the declared reader profile works on supported installed builds.

## Work

- Fix named reference Windows/macOS hardware and collect real page-open,
  preview, cached turn, scroll, zoom, memory and worker-recovery measurements.
- Run the acceptance corpus on both platforms, including native PDFium assets,
  OCR data and sandbox profiles.
- Test mouse wheel, precision trackpad, scrollbar, keyboard navigation, resize,
  high DPI, dark/light themes, localisation and reduced motion.
- Conduct physical Narrator and VoiceOver journeys for navigation, page status,
  zoom, search, outline, error, thumbnail and password controls.
- Test clean install, missing/corrupt native assets, worker crash, root removal,
  source replacement and restore.

## Exit criteria

All profile-critical fixtures pass or have approved degraded/refused outcomes;
performance budgets are recorded on reference hardware; no focus trap or blank
unlabelled state remains; platform and accessibility reports identify build,
device, version and reviewer.
