# Execution status and evidence

Updated: 2026-09-08
Status: Active; P0 boundary slice in progress
Owner: Ogma engineering owner

This ledger records implementation evidence without converting a passing test
into a standards or physical-platform claim. It is intentionally separate from
the backlog so the queue remains stable while evidence changes.

## Current slice

| Item | Status | Evidence | Remaining gate |
|---|---|---|---|
| PDF capability profile and render-policy contracts | PARTIAL | `src/OgmaLibrary.Application/Reader/IPdfRenderer.cs`; `PdfReaderContractTests` | Profile serializer, per-document diagnostics, approved fixture matrix |
| Brokered input identity | PARTIAL | `PdfInputBroker`; `Phase10PdfInputBrokerTests` | Persist source handle through every derived-artifact contract |
| Worker copy consistency | IMPLEMENTED for covered path | `PdfWorkerClient.CopyInputToSandbox`; PDF worker isolation tests | Physical Windows/macOS escape evidence and independent review |
| Direct parser/writer bypass guard | IMPLEMENTED for named services | `Architecture_PdfOperations_EnterApprovedBoundaries`; worker-backed TOC, metadata, ISBN and write-back | Expand the allowlist/guard to all PDF-producing paths |
| Page geometry and render cache identity | PARTIAL | `PdfPageGeometry`, `RenderRequest.CacheFingerprint`, focused contract tests | Complete page-box transforms, overlay corpus and visual oracle |
| Text/search false-match correction | PARTIAL | `InDocumentSearchService`; focused reader tests | Unicode/font/reading-order corpus and OCR uncertainty propagation |

## Verification record

Environment: Windows NT 10.0.26200.0, .NET SDK 10.0.101
Repository revision at start of this slice: `245c8018e71d8a26cbf67ec8ac4264c170ee07ae`

Passing commands:

- `dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --no-restore --filter "FullyQualifiedName~PdfReaderContractTests|FullyQualifiedName~Phase10PdfInputBrokerTests|FullyQualifiedName~InDocumentSearchTests"`
- `dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --no-build --filter "FullyQualifiedName~PdfWriteBackTests|FullyQualifiedName~Phase15WriteBackSafetyTests"`
- `dotnet test tests/OgmaLibrary.Tests.Architecture/OgmaLibrary.Tests.Architecture.csproj --no-restore`

Observed focused totals: 15 reader/security tests, 13 write-back tests and 42
architecture tests. These results prove the listed code paths on this machine;
they do not close macOS, accessibility, licensed-corpus or signed-release
gates.

## Next execution order

1. Finish the source-handle contract for extraction and derived assets.
2. Add typed document capability diagnostics to the worker response and reader
   status surface.
3. Build the lawful mixed-PDF corpus and geometry/text oracle.
4. Re-audit worker containment on reference Windows and macOS hosts.
