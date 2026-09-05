# Phase 32 Sharded 3D Asset URI Evidence

Date: 2026-09-05

## Result

The 3D scene projection now emits cover and spine URIs that match the
content-hash-sharded sidecar layout:

```text
ogma://assets/covers/<hash-prefix>/<hash>.jpg
ogma://assets/spines/<hash-prefix>/<hash>.jpg
```

`OgmaSchemeHandler` accepts nested shard segments only after validating every
segment and resolving the result beneath the selected asset-class root. Unsafe
segments and unknown asset classes remain fail-closed.

## Verification

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~OgmaSchemeHandlerTests|FullyQualifiedName~Bookshelf3DViewModelTests"
  Passed: 18, Failed: 0, Skipped: 0
```

## Gate disposition

Closed: repository-level 3D sidecar URI and safe shard-path contract.

Still open: native WebView2/WKWebView attachment, reference hardware,
cross-platform interaction, GPU budgets, and physical accessibility evidence.
