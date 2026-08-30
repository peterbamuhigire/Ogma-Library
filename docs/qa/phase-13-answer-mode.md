# Phase 13 — Local-Evidence Answer Mode

Answer mode now has a production application path instead of a V2
`NotImplementedException` placeholder.

`LocalEvidenceAnswerPipeline` queries the existing semantic/exact search service,
selects only returned local snippets, and emits one `AnswerCitation` per passage
with a one-based page and local chunk identifier where available. If no local
passage is found it returns an explicit no-evidence response. It does not call an
external provider and therefore remains safe when cloud AI is disabled.

## Verification

```powershell
dotnet build OgmaLibrary.sln -c Release --no-restore
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~LocalEvidenceAnswerPipelineTests"
```

The test suite covers citation limits, page-number conversion, local-only answer
assembly, and the no-evidence response. Cloud/provider-generated answer synthesis
is intentionally not claimed by this implementation; any future provider path
must still route through the Phase 12 gateway and Phase 19 privacy controls.
