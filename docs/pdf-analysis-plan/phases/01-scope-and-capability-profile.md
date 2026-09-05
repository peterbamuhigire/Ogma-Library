# Phase 1 — Scope and capability profile

**Depends on:** canonical phases 1, 10, 11, 37–39.
**Outcome:** a versioned statement of what Ogma supports, degrades or refuses.

## Work

- Convert the minimum profile in [the standard model](../02-pdf-standard-model.md)
  into a machine-readable capability matrix.
- Separate PDF 2.0 reader behavior from PDF document conformance, PDF/UA-2,
  PDF/A-4 and application safety policy.
- Define feature statuses: supported, degraded, refused, failed, not present.
- Assign an owner, test family, user impact and release gate to every feature.
- Add a currentness record for ISO 32000-2 errata, engine versions, native
  assets, OCR data and platform builds.
- Freeze the public wording until the profile and known-limit list exist.

## Code and docs

Add `PdfCapabilityProfile`, `PdfFeature`, `PdfFeatureResult` and a profile
serializer in Application; expose the result through the worker/document
diagnostic path and reader status surface. Update ADR-0004, the canonical plan,
root README and release notes so they do not make broader claims.

## Evidence and experiment

Baseline the current profile from code and fixtures. Hypothesis: explicit
feature statuses will reduce silent failures and support questions. Measure the
percentage of opened documents with a non-empty diagnostic and the number of
unsupported features surfaced to users. Run a 20-fixture profile smoke test.

## Exit criteria

- Profile approved by owner and linked to standards/source register.
- Every profile feature has a fixture or is explicitly `NOT_ASSESSED`.
- UI has a visible but calm degraded/refused state with recovery guidance.
- CI rejects a release if the profile, dependency or errata version is missing.
