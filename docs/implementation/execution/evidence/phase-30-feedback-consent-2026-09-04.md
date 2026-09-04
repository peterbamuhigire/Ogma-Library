# Phase 30 Advisor Feedback Consent Evidence

Date: 2026-09-04
Reviewer: Peter Bamuhigire, Lead Consultant

## Decision

The code-level feedback-consent and minimization subgate is closed. Feedback
cannot be stored without an explicit consent flag. The durable record contains
only a request SHA-256 hash, a 1–5 rating, a bounded reason code, and a UTC
timestamp; raw prompts, answers, endpoint data, and credentials are outside
the contract. Records are retained locally for 90 days and are atomically
persisted with a 10,000-entry bound.

The feedback UI, human-labelled evaluation set, quarantined live-provider
evaluation, accessibility evidence, and AI retrieval freeze remain open.

## Verification

Command:

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~Phase30AdvisorFeedbackTests|FullyQualifiedName~Phase30AdvisorQualityTests|FullyQualifiedName~PrivacyCenterViewModelTests" --verbosity minimal -m:1
```

Result: 14 passed, 0 failed, 0 skipped; build completed with 0 warnings and 0
errors.

Covered cases include consent denial without file creation, accepted bounded
feedback round-trip, raw-content exclusion, invalid hash/rating rejection,
cross-instance reload, threshold evaluation, and Privacy Center history
controls.
