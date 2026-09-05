# Phase 8 — Navigation and interchange

**Depends on:** Phases 4–7; canonical phases 11, 20–23.
**Outcome:** the reader preserves useful PDF navigation without unsafe actions.

## Work

- Expand TOC extraction to nested outlines, named/explicit destinations, page
  coordinates, target zoom and unresolved-target diagnostics.
- Add PDF page labels distinct from physical zero-based indices and localize
  their display without losing the raw label.
- Resolve internal links and expose safe history/back-forward navigation.
- Classify external links, attachments and launch actions; route them through
  explicit user policy and never execute arbitrary launch actions.
- Add metadata/XMP/catalog language and attachment inventory with size limits;
  keep file metadata distinct from user-approved canonical metadata.

## Experiment and exit

Use outline/destination/link/label fixtures and compare every target with a
reference reader or inspected expected result. Exit when unresolved targets
degrade to a correct physical-page fallback, labels survive search/jump/export,
and unsafe actions are visible as blocked—not silently followed.
