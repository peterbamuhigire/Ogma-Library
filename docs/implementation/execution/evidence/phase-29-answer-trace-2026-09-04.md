# Phase 29 Answer-Evidence Trace Evidence

Date: 2026-09-04

## Implementation

`LocalEvidenceAnswerPipeline` now optionally appends an immutable
`AnswerEvidenceTrace` audit event after each local answer attempt. The versioned
trace contains:

- a SHA-256 hash of the question;
- requested citation limit and content-aware permission;
- search-result and accepted-citation counts;
- the safe outcome (`no-local-evidence` or `extractive-local-evidence`); and
- bounded citation provenance: book, page/chunk anchor, source label, evidence
  version, and uncertainty label.

Question text, answer text, and evidence excerpts are not persisted in this
trace.

## Verification

```powershell
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~Phase29AnswerTraceTests|FullyQualifiedName~Phase29GroundedEvidenceTests" --verbosity minimal -m:1
```

Result: 6 passed, 0 failed.

The tests assert that question text and a private evidence excerpt are absent,
and validate both the cited-evidence and no-evidence outcomes.

## Scope boundary

Answer UI citation navigation, content-tier consent wiring in the shell,
unsupported-claim/abstention benchmarks, and physical UI evidence remain open.
