# Phase 37 Evidence - Synthetic Hostile PDF Failure Boundary

Date: 2026-09-06

## Gate addressed

The static safety scan retained a medium follow-up because malformed/hostile
PDF exception behavior lacked a repeatable corpus test at the production
child-process boundary.

## Corpus and assertions

`PdfWorker_HostileMalformedCorpus_FailsSafelyAndRemainsAvailable` sends seven
bounded malformed inputs through `IsolatedPdfRendererFactory` and the
production PDF worker:

- truncated header/body;
- impossible stream length;
- malformed, oversized xref declaration;
- NUL-filled body;
- deeply nested array syntax;
- a fake JavaScript/OpenAction containing an outside-sandbox path marker;
- invalid FlateDecode stream data.

For each input, the acceptance oracle requires either a zero-page safe result
or the stable `PDF worker operation failed.` exception. Exception output must
not disclose the fixture path or the embedded escape marker, no outside file
may appear, and a fresh valid PDF session must render immediately afterward.

## Verification

```text
dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj \
  --configuration Release --no-restore \
  --filter "FullyQualifiedName~PdfWorker_HostileMalformedCorpus" \
  --logger "console;verbosity=normal" -m:1

Passed: 1
Failed: 0
Skipped: 0
Duration: 8.34 seconds
```

The one test performs seven hostile-input/recovery cycles.

## Gate disposition

The repeatable synthetic malformed-PDF exception/redaction/recovery subgate is
closed. This does not replace a maintained third-party hostile corpus,
independent containment review, physical platform testing, or long-duration
soak; those Phase 37 release gates remain open.
