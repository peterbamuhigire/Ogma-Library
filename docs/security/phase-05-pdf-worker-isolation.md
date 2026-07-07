# Phase 05 PDF Worker Isolation

Date: 2026-07-07

## Boundary

Production PDF rendering, text extraction, and thumbnail/spine generation no longer run PDF parsing in the main app process.

- `CompositionRoot` registers `IsolatedPdfRendererFactory` for `IPdfRendererFactory`.
- `IsolatedPdfRendererFactory` delegates page count, render, rotation, and text-layer calls to `PdfWorkerClient`.
- `ThumbnailService` and `SpineService` ask `PdfWorkerClient` to render into a per-operation sandbox, then the parent process copies the completed asset to the sidecar path.
- `PdfiumAdapterFactory` and `PdfiumAdapter` remain available for worker-side execution and explicit tests.

## Worker Controls

- Worker process: `OgmaLibrary.Workers pdf-worker`.
- Per-operation sandbox: `%TEMP%/OgmaLibraryPdfWorker/<guid>` or the configured `PdfWorkerOptions.SandboxRoot`.
- Worker temp variables: `TMP`, `TEMP`, and `TMPDIR` point to the sandbox.
- Worker output validation: every output path is normalized and must remain inside the sandbox.
- Parent output handoff: the main process copies finished worker outputs from the sandbox to final sidecar paths only after a zero exit code.
- Timeout behavior: parent kills the worker process tree when the operation exceeds `PdfWorkerOptions.Timeout`.
- Windows process-spawn constraint: parent attempts to attach the worker to a Job Object with `ActiveProcessLimit = 1` and `KillOnJobClose`.

## Fault-Injection Coverage

`tests/OgmaLibrary.Tests/Security/PdfWorkerIsolationTests.cs` verifies:

- network operation requests are denied by the worker command surface;
- process-spawn operation requests are denied by policy;
- temp traversal attempts cannot write outside the per-operation sandbox;
- malformed PDFs return zero pages through the isolated renderer without crashing the app process;
- valid PDFs render through the worker subprocess path.

## Platform Notes

The Phase 05 implementation establishes a process boundary and app-level sandbox handoff. It does not claim a kernel-enforced network firewall on every platform. Windows receives an additional child-process Job Object constraint; macOS/Linux release hardening should evaluate native sandbox profiles during packaging. Reference-hardware performance evidence remains owned by the later performance/release gates.
