# Phase 24 Packaged OCR Resource Observation

Date: 2026-09-06

## Scope

The existing generated scanned-PDF acceptance now records resource evidence
while exercising the production isolated PDF renderer and packaged English
Tesseract model. This closes the missing telemetry for that deterministic local
fixture only. It is not representative-corpus or reference-machine evidence.

## Gate and invariant

The renderer is configured with a 15-second CPU limit, a 30-second command
timeout, and the production default 768 MiB memory ceiling. The test requires
both peak working set and private-memory observations to be non-zero and no
greater than that configured ceiling. A ceiling breach fails the worker call;
the assertions also ensure telemetry was actually captured.

The test records, but does not convert into a universal performance claim:

- complete fixture wall time;
- test-host CPU time, which includes in-process Tesseract work;
- test-host peak working set; and
- isolated renderer peak working set and private memory.

## Current Windows observation

```text
pages=1
elapsed_ms=2082
testhost_cpu_ms=1516
testhost_peak_bytes=197496832
renderer_peak_bytes=58159104
renderer_private_bytes=23785472
```

The same execution retained full expected-word recall and confidence of at
least 0.75.

Command and result:

```text
dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Phase24RealOcrCorpusTests" --logger "console;verbosity=normal" -m:1
Passed: 1, Failed: 0, Skipped: 0
```

## Interpretation and residual gates

This is a repeatable generated-fixture baseline suitable for detecting major
resource regressions. It does not establish production throughput, corpus-wide
accuracy, or cross-platform native packaging. Representative real mixed-PDF
accuracy/resource evidence, supported macOS Tesseract assets, and physical
assistive-technology evidence remain `NOT ASSESSED`.
