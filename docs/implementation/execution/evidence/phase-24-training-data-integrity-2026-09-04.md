# Phase 24 Evidence — Tesseract Training-Data Integrity

Date: 2026-09-04
Scope: packaged local OCR asset integrity only

## Implemented control

`TesseractOcrProvider` now verifies every requested language pack before
creating a `TesseractEngine`. Verification requires the asset to exist, be
readable, and match an approved SHA-256 checksum. A language pack without an
approved checksum fails closed with a bounded integrity error.

The current package set contains only `eng.traineddata` from
`Tesseract.Data.English` 4.0.0. Its approved SHA-256 is:

```text
DAA0C97D651C19FBA3B25E81317CD697E9908C8208090C94C3905381C23FC047
```

The verifier intentionally does not claim that `fra`, `deu`, `ita`, or `spa`
are packaged merely because the language policy accepts those selectors.

## Verification

Command:

```text
dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --filter "FullyQualifiedName~Phase24OcrQualityTests" --no-restore --verbosity minimal -m:1
```

Result: 7 passed, 0 failed.

Covered cases include:

- restored English asset matches the approved checksum;
- a modified English asset is rejected;
- a language without an approved checksum is rejected.

## Still open

This evidence does not close real multilingual accuracy, CPU/memory corpus,
retry/resource telemetry, OCR UI quality, or cross-platform packaging gates.
