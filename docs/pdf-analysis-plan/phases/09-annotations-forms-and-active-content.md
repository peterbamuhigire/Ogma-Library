# Phase 9 — Annotations, forms, signatures and active content

**Depends on:** Phases 5, 7 and 8; canonical phases 15, 21 and 37.
**Outcome:** interactive PDF features have an explicit safe, testable policy.

## Work

- Inventory annotation subtypes and appearance streams; decide which are
  rendered, selectable, editable or ignored with a visible reason.
- Decide whether widgets/forms are view-only first; never submit, calculate or
  execute form content without an explicit local policy.
- Keep JavaScript, launch, multimedia, embedded 3D and external actions
  disabled by default; expose safe internal links separately.
- Add signature detection and validation status. Never imply that re-opening a
  file validates its signatures; block or clearly warn before mutation.
- Reconcile PDF annotations with Ogma database annotations without duplication.
- Keep PDF write-back opt-in and separate from reader conformance; require
  backup, preview/diff, transactional write, reopen, validation, hash, restore
  and audit evidence.

## Experiment and exit

Use markup, link, widget, signature, JavaScript and embedded-file fixtures.
Verify that page appearance remains usable, unsafe actions do not execute, and
the UI names the exact limitation/recovery. Exit only when ADR-0008’s safety
conditions and the profile’s interactive status are explicit.
