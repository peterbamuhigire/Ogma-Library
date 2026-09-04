# Phase 10 fail-closed resource-limit evidence — 2026-09-04

## Change

The PDF worker broker now treats failure to assign the Windows Job Object as a
startup failure. A Windows worker is never allowed to continue without the
configured memory, CPU-time, active-process, and kill-on-close limits. The same
requirement is applied to both one-shot commands and the persistent reader
worker session. If assignment fails, the process tree is terminated and the
operation returns a typed broker failure.

Non-Windows hosts retain the existing adapter-neutral path; this record does not
claim that a macOS sandbox profile exists or that OS policy has been independently
approved.

## Verification

Command:

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --no-restore --filter "FullyQualifiedName~PdfWorkerIsolationTests" --logger "console;verbosity=minimal"
```

Result: **PASS — 7 passed, 0 failed, 0 skipped**.

The suite covers blocked network/process diagnostics, traversal containment,
malformed input, valid rendering, output ceilings, and rendering from a worker
sandbox copy after source removal. The Debug build completed without compiler
errors.

Current-HEAD rerun of the Phase 10 broker/isolation and Phase 11 extraction
selectors passed 27 combined tests, with 0 failures and 0 skips.

## Remaining gates

- Physical Windows escape and resource-exhaustion evidence is still required.
- A real OS filesystem/network sandbox adapter is not established by environment
  flags or this Job Object; macOS profile evidence remains `NOT ASSESSED`.
- Independent security approval remains open.
