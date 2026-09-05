# Acceptance corpus and release gates

## Corpus families

| Family | Required fixtures | Primary assertions |
|---|---|---|
| Structure | classic xref, xref stream, object stream, incremental update, linearized/non-linearized | page count/effective object resolution/fast first page |
| Geometry | portrait, landscape, rotated, crop-offset, non-letter, user-unit | dimensions, transforms, fit and overlays |
| Imaging | fonts, missing/valid ToUnicode, ligatures, vertical text, images, masks, transparency, patterns | visual fidelity and text quality |
| Interaction | outlines, named/explicit destinations, labels, links, markup, widgets, OCG | navigation and explicit safety policy |
| Security | malformed, encrypted/password variants, embedded/launch/JavaScript, decompression/size bombs | fail closed, no leak, bounded resources |
| Quality | image-only, mixed, Unicode, multi-column, low-quality scans | extraction/OCR/search provenance and confidence |
| Operations | rename/move/replace/copy/root disconnect, worker crash, source mutation | identity, recovery, idempotency and no false deletion |
| Release | clean install, packaged native assets/OCR, signed/notarized builds | startup, rendering, rollback and legal notices |

Each fixture needs lawful provenance, content hash, expected outcome, PDF
version/profile, known limitations and owner. Do not commit private user books.

## Gate levels

**Developer gate:** build, focused tests, architecture boundary scan, no new
untyped exceptions or direct file opens.

**Corpus gate:** all profile fixtures run with reproducible results, typed
diagnostics, acceptable visual/text thresholds and no silent degradation.

**Security gate:** real sandbox restrictions, path/TOCTOU, password secrecy,
resource limits, worker crash isolation and independent review.

**Experience gate:** first visual, cached/uncached page navigation, wheel/trackpad
scroll, zoom/focus, thumbnails, search/copy, keyboard and error recovery.

**Platform gate:** named Windows and macOS reference devices, packaged native
dependencies, Narrator/VoiceOver, DPI/localisation and clean-install behavior.

**Release gate:** versioned conformance profile, evidence index, known-limits
list, signed/notarized artifact, rollback and owner acceptance.

## Evidence record

Every run records commit, build configuration, OS, hardware, dataset hash,
dependency/native asset/model versions, test command, duration, peak memory,
CPU, result, failures and reviewer. Mocks and synthetic fixtures are labelled;
missing physical/live evidence is `NOT_ASSESSED`.
