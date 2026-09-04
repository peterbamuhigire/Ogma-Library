# Phase 17 redacted job lifecycle events evidence

Date: 2026-09-04

`JobRuntimeService` now records structured local audit events for claim,
completion, failure, and expired-lease recovery. The event contract contains
stable lifecycle fields only: job type, attempt, lease duration, failure code,
retry scheduling, and dead-letter state. Job payloads, file paths, and exception
messages are excluded from these events.

Verification: `Phase17JobRuntimeTests.JobLifecycleEvents_AreStructuredAndRedacted` passes.

Remaining Phase 17 gates are conversion of the remaining polling workers,
resource-group limits, metrics/diagnostics export, and kill/restart load evidence.
