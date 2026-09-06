# Phase 37 Job Diagnostic Redaction

Date: 2026-09-06
Reviewer: Peter Bamuhigire, Lead Consultant

## Finding and correction

The Library Health CSV and failed-job projection exposed the persisted
free-form `ErrorMessage`. Legacy messages can contain local paths, provider
details, tokens, or personal identifiers even though current worker boundaries
prefer safe messages.

The projection and CSV now expose only `FailureCode`. A legacy failed row with
no code is represented as `job_failed`; raw error text remains local database
state and does not cross this diagnostics boundary.

## Verification

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~HealthDashboardTests"
Passed: 10, Failed: 0, Skipped: 0
```

The regression seeds a Windows user path and token-like value in the stored
error while setting `provider_timeout` as its stable category. The export
contains the category and contains neither the student path fragment nor the
token value.

## Gate disposition

The failed-job health/CSV diagnostic redaction sub-gate is closed. Controlled
debug storage, retention, and physical operator export review remain governed
by the Phase 37 and release gates.
