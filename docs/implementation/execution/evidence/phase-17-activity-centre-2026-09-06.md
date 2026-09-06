# Phase 17 Activity Centre Evidence - 2026-09-06

## Scope

This increment closes the local software gate for privacy-safe background-job
visibility and operator control. It does not claim physical reference-machine,
full-application crash, or long-duration soak evidence.

## Delivered

- `ActivityCentreViewModel` and `ActivityCentreView` are reachable through the
  existing Index Manager surface.
- The view shows bounded queue, running, paused, failed, dead-letter, and attempt
  totals plus the 100 most recent payload-free job rows.
- Pending jobs can be cancelled; terminal failed jobs can be retried. Dead-letter
  and active jobs remain visible but cannot be blindly requeued or cancelled.
- Both state transitions use conditional database updates. Retry clears stale
  completion, schedule, lease, and failure data, and records a payload-free
  `JobRetryQueued` audit event.
- Diagnostic JSON is written only to a user-selected file and comes from the
  existing redacted runtime projection. Payload, lease-owner, and free-form
  error fields are excluded.
- Failure codes are validated before state mutation as bounded lowercase
  machine identifiers; path-, query-, whitespace-, and token-bearing values are
  rejected so the remaining exported/audited field cannot become a free-text
  escape hatch.
- English and French labels, status text, button names, and a headless render
  test cover the new interaction surface.

## Verification

| Command | Result |
| --- | --- |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | PASS - 0 warnings, 0 errors |
| `dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~Phase17JobRuntimeTests` | PASS - 11/11 |
| `dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~ReleaseAcceptanceContractTests` | PASS - 1/1 |
| `dotnet test tests/OgmaLibrary.Tests.Ui/OgmaLibrary.Tests.Ui.csproj --configuration Release --no-restore --filter FullyQualifiedName~ActivityCentreViewModelTests` | PASS - 3/3 |

The acceptance-contract test now creates an isolated evidence package whose
declared digest is computed from the bytes under test. This removes checkout
line-ending behavior from the validator test while preserving digest-tamper,
schema-freeze, unknown-property, and commit-binding assertions. A failing valid
case now includes the child validator's stdout and stderr.

## Remaining evidence

- Full-application queue kill/restart across all active handler types:
  `NOT ASSESSED`.
- Cooperative cancellation for non-OCR active handlers: `NOT ASSESSED`.
- Physical Windows and macOS reference-machine behavior: `NOT ASSESSED`.
- Long-duration queue and worker soak: `NOT ASSESSED`.
