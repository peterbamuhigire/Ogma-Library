# Source and claim register

Access/review date: 2026-09-04. URLs are primary or authoritative sources
selected for this audit. The local books are listed separately as durable
concept sources.

## Current authoritative sources

| ID | Source and scope | Date/version | Use | Status/limitation |
|---|---|---|---|---|
| S-ISO-32000 | [ISO 32000-2:2020](https://www.iso.org/standard/75839.html) | Edition 2, 2020-12 | Core PDF 2.0 scope and processor/document context | Normative source; full text is licensed |
| S-PDF-ERRATA | [PDF Association ISO 32000-2 resources](https://pdfa.org/resource/iso-32000-2/) and [errata](https://pdf-issues.pdfa.org/32000-2-2020/) | Current page reviewed 2026-09-04; Errata Collection 3 reported | Corrections/currentness and test references | Industry-hosted errata index; confirm release applicability |
| S-PDF-START | [How to get started with PDF 2.0](https://pdfa.org/how-to-get-started-with-pdf-2-0/) | Reviewed 2026-09-04 | Subset/capability-profile reasoning | Implementation guidance, not a conformance certificate |
| S-PDFUA | [PDF/UA-2 overview](https://pdfa.org/iso-14289-2-pdfua-2/) | ISO 14289-2:2024 | Tagged PDF 2.0 accessibility distinction | Does not certify Avalonia UI accessibility |
| S-PDFA | [PDF/A-4 overview](https://pdfa.org/resource/iso-19005-4-pdf-a-4/) | ISO 19005-4 | Preservation/input-profile distinction | Not a general reader UI standard |
| S-PDF-ARCHIVE | [PDF specification archive](https://pdfa.org/resource/pdf-specification-archive/) | Reviewed 2026-09-04 | Locate related standards and errata | Index source |
| S-PDFPig | [PdfPig documentation](https://github.com/UglyToad/PdfPig/blob/master/docs/index.md) and [PdfDocument notes](https://github.com/UglyToad/PdfPig/wiki/PdfDocument) | Current docs reviewed 2026-09-04 | Library behavior and limits | Project documentation; not PDF conformance authority |
| S-PDFPig-REL | [PdfPig releases](https://github.com/UglyToad/PdfPig/releases) | v0.1.15 reported 2026-06-25 | Dependency currentness check | Must test before any upgrade; Ogma pins 0.1.9 |
| S-PDFtoImage | [PDFtoImage render options](https://github.com/sungaila/PDFtoImage/blob/master/src/PDFtoImage/RenderOptions.cs) and [README](https://github.com/sungaila/PDFtoImage/blob/master/README.md?plain=1) | Reviewed 2026-09-04 | Wrapper/render option behavior | Wrapper docs; PDFium feature evidence still corpus-dependent |
| S-Avalonia | [Avalonia ScrollViewer](https://v11.docs.avaloniaui.net/docs/reference/controls/scrollviewer/) | Current docs reviewed 2026-09-04 | Scroll boundary/layout constraints | UI framework guidance, not PDF behavior |

## Local concept sources

The four PDFs in `docs/pdf-standards/pdf-reader-source-extractions-2026-09-04.md`
were read and synthesised in the previous research increment. They are used
for durable engineering concepts such as xref/trailer resolution, page-tree
inheritance, geometry, text mapping, navigation, thumbnails, linearization,
and safe active-content handling. They are not used as sole evidence for
current dependency versions, errata status or release claims.

## Claim-control rules

For every new assertion, record:

```text
claim_id, source_id/code_path/evidence_id, source tier, scope, version/date,
access date, verification date, freshness class, support status, limitation,
confidence, owner and next review date
```

The repository’s execution evidence should be linked for code claims. A source
URL alone cannot prove Ogma behavior; a passing unit test alone cannot prove a
standards claim.
