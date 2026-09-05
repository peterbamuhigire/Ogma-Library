# PDF standard model for Ogma

## Normative layers

| Layer | What it governs | Ogma implication |
|---|---|---|
| ISO 32000-2:2020 (PDF 2.0) | PDF syntax, file structure, imaging model, pages, text, metadata, interactive features, security and conformance | Core processor capability profile |
| Current errata/technical extensions | Corrections and extensions to the PDF 2.0 reference | Pin the edition/errata used by the engine and corpus |
| ISO 14289-2:2024 (PDF/UA-2) | Tagged PDF 2.0 use for accessibility | Accessibility input/read-order goals; not a substitute for UI accessibility |
| ISO 19005-4 (PDF/A-4) | Preservation constraints based on PDF 2.0 | Recognise preservation documents and avoid mutating them casually |
| Application policy | Unsafe actions, privacy, passwords, network, attachments, JavaScript | Explicit safe reader behavior beyond syntax parsing |

## Minimum reader profile

The first profile should cover these capabilities and expose a result for each:

### File and object model

- header, body, classic xref, xref streams, object streams and trailer chains;
- incremental updates and effective latest object resolution;
- page tree inheritance and page count;
- compressed streams, filters, indirect references and bounded decompression;
- linearized and non-linearized local files;
- password-protected/encrypted files with no password leakage;
- malformed input with typed, page/document-scoped diagnostics.

### Imaging and page model

- effective MediaBox/CropBox and relevant box defaults;
- page rotation and coordinate transforms;
- resources, fonts, images, masks, transparency, patterns, shadings and form
  XObjects to the extent supported by the selected renderer;
- color spaces, rendering intent, alpha and a defined display background;
- annotations/forms as a deliberate render/interaction policy;
- optional content groups as visible/hidden/unsupported policy, not accidental
  behavior;
- output size, tiling and cache limits.

### Text and semantics

- glyph placement and font encoding;
- `/ToUnicode` use where available and explicit degraded mapping when not;
- words/runs with page coordinates, direction, confidence and extractor
  provenance;
- search and copy behavior that does not silently substitute OCR as primary
  text;
- tagged structure, logical order and alternate text where supported;
- OCR only as a derived, confidence-gated fallback.

### Navigation/interchange

- outlines/bookmarks, nested levels and destination types;
- named and explicit destinations, coordinates and target zoom;
- page labels distinct from zero-based physical indices;
- internal/external links, attachments and metadata/XMP policy;
- history/back-forward and safe handling of external launch targets.

## Explicit non-goals for the first profile

Ogma should not execute PDF JavaScript, launch actions, multimedia, embedded
3D content or arbitrary external applications. It should not claim to author
fully conforming PDF 2.0/PDF/A/PDF/UA output until a separate writer profile,
writer engine and validation suite exist. It should not advertise complete
support for every extension merely because PDFium opens the file.

## Capability result contract

Every opened document should produce a bounded diagnostic record:

```text
DocumentProfileResult
  sourceContentHash
  pdfVersion / linearized / encrypted
  parserVersion / rendererVersion / errataProfile
  openStatus: supported | degraded | refused | failed
  featureResults[]: feature, status, evidence, userImpact
  pageCount / pageGeometryVersion
  textStatus / navigationStatus / accessibilityStatus
  resourceUsage / duration
  safeRecoveryAction
```

This record belongs beside, not inside, the source PDF. It is the bridge
between PDF facts, reader UX and downstream extraction/search provenance.
