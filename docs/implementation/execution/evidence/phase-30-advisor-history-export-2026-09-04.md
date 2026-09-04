# Phase 30 advisor history export evidence

Date: 2026-09-04

The privacy persistence contract now exports only non-deleted advisor history
entries as JSON to a caller-owned stream. Ordering is deterministic and is
performed after materialisation so the path remains compatible with SQLite's
`DateTimeOffset` query limitations. The Privacy Center exposes a JSON save-file
flow; cancellation and unavailable storage providers do not write a partial
export.

Verification: the focused AI persistence/privacy run passed 11/11 tests,
including visible-history-only export, hard-delete retention behavior, and the
Privacy Center export command. Physical Windows file-picker and accessibility
walkthrough evidence remains `NOT ASSESSED`.

Remaining Phase 30 gates include feedback consent, human-labeled thresholds,
quarantined live-provider evaluation, full-shell accessibility/keyboard
evidence, and the final AI retrieval freeze.
