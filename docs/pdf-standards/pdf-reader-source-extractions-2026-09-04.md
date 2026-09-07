# PDF reader source extractions

Date: 2026-09-04

Purpose: engineering reference for Ogma Library's PDF reader

Source set: four user-provided PDF books listed in [source inventory](#source-inventory)

## How this was read

The four local PDFs were opened with a PDF parser, their document metadata and
complete table-of-contents trees were indexed, and the text of every page was
scanned for reader-critical topics. Key sections were then read at page level,
including file structure, page trees, content streams, rendering, text and
fonts, navigation, thumbnails, annotations, metadata, accessibility,
linearization, and viewer behavior. Representative pages from each book were
also rasterized and visually checked so that the extraction was not based only
on a text layer.

The notes below are an engineering synthesis. They paraphrase the books and
do not reproduce their text. Page numbers refer to the PDF page numbers shown
by the parser/book pagination where those differ; section numbers are the
stable cross-reference for ISO 32000-2.

## Source inventory

| Source | Local file | Parsed pages | Principal use in this brief |
| --- | --- | ---: | --- |
| ISO 32000-2:2020, *Document management — Portable document format — Part 2: PDF 2.0*, with errata | `C:\Users\Peter\Downloads\ISO 32000-22020 — Document management - Portable document format - Part 2 PDF 2.0 (sponsored) (ISO) (z-library.sk, 1lib.sk, z-lib.sk).pdf` | 1,003 | Normative behavior, conformance, file structure, rendering, navigation, thumbnails, tagging, linearized access |
| Ryan Hodson, *PDF Succinctly* | `C:\Users\Peter\Downloads\PDF Succinctly (Ryan Hodson) (z-library.sk, 1lib.sk, z-lib.sk).pdf` | 62 | Compact object-model and content-stream mental model; practical navigation and C# orientation |
| Leonard Rosenthol, *Developing with PDF — Dive into the Portable Document Format* | `C:\Users\Peter\Downloads\Developing with PDF - Dive into the Portable Document Format (Leonard Rosenthol) (z-library.sk, 1lib.sk, z-lib.sk).pdf` | 217 | Developer-facing treatment of syntax, imaging, fonts, annotations, forms, embedded files, optional content, structure and metadata |
| John Whitington, *PDF Explained — The ISO Standard for Document Exchange* | `C:\Users\Peter\Downloads\PDF Explained The ISO Standard for Document Exchange (John Whitington) (z-library.sk, 1lib.sk, z-lib.sk).pdf` | 138 | Reader behavior, linearization, random access, text extraction, navigation, encryption, viewer responsibilities and tooling |

## Executive design conclusions

These are the conclusions most directly relevant to the current reader work.

1. **A PDF is a page description and object graph, not a sequence of image
   files.** A page depends on a page dictionary, inherited page attributes,
   resources, content streams, fonts, images, color spaces, graphics state,
   annotations and sometimes optional-content state. The rendering pipeline
   therefore needs a document/session lifetime and a cache keyed by page plus
   render parameters; repeatedly reopening a document for every page is the
   wrong performance shape.

2. **Random access is a format feature, not a promise that every open is
   cheap.** The cross-reference table/stream and trailer locate objects, but a
   page can still pull in large shared resources, compressed object streams,
   fonts, images and transparency groups. A warm document session, deduplicated
   renders, bounded prefetch and cancellation are the practical reader
   requirements.

3. **Linearization is an optimization with a specific access strategy.** A
   linearized file places first-page data and hint information where a reader
   can begin displaying promptly, and gives byte ranges for arbitrary pages.
   Local files should still be opened through the normal trailer/xref path and
   should not be assumed to be linearized. The reader can later add a
   linearization detector/telemetry field, but must not make correctness depend
   on it.

4. **Fit width is a destination/display policy, not a PDF page size.** The
   `/FitH` destination means the page is displayed at a scale that fills the
   window horizontally, optionally with a vertical coordinate at the top.
   Ogma should calculate fit width from the *actual reading viewport* after the
   sidebar and padding are accounted for. It should not use a hard-coded 720 px
   width as the visible width.

5. **Smooth scrolling requires a real scrollable viewport.** Wheel input should
   normally change a `ScrollViewer` offset continuously. It should not be
   converted into a one-notch page-turn event while the page is taller than the
   viewport. At a scroll boundary, a deliberate fallback may navigate to the
   adjacent page; the default path must preserve native scrolling, trackpad
   deltas, scrollbar dragging and keyboard scrolling.

6. **Zoom must be separated into display geometry and raster quality.** The
   displayed surface size, overlay coordinate transform, render pixel width,
   and cache key are related but not identical. A zoom change must invalidate or
   request the correct raster size, preserve the reader's focal point where
   possible, keep annotation coordinates aligned, and cap output dimensions to
   avoid unbounded memory use.

7. **Page dimensions and rotation are document data.** `MediaBox`, `CropBox`,
   page rotation and inherited page attributes determine the page geometry.
   An assumed 3:4 page is acceptable as a temporary fallback, but the reader
   should eventually expose the actual page width/height in the session read
   model and use it for fit width, fit page, selection mapping and overlays.

8. **Text appearance is not text semantics.** Glyphs are painted through font
   dictionaries, encodings, positioning matrices and text-showing operators.
   Reliable copy/search needs `/ToUnicode` or another valid character identity
   route. A text layer must tolerate missing/ambiguous mappings and must not be
   treated as the source of truth for visual placement.

9. **Navigation is richer than first/previous/next/last.** The catalog may
   expose outlines, named destinations, explicit destinations, page labels,
   links and annotations. A useful reader should preserve and eventually expose
   these, while keeping page-index navigation as the reliable fallback.

10. **Thumbnails are optional PDF objects and a separate product concern.** A
    page can have an embedded thumbnail, but a reader is allowed to generate
    previews from the page itself. Thumbnail generation should be independent
    of full-resolution page rendering, use a small bounded width, persist by
    document identity/page/render version, and surface failure text rather than
    an empty card or an unlabeled dialog.

11. **Unknown features must fail soft.** Compatibility sections, optional
    content, unknown extensions, unsupported annotations and newer constructs
    should be isolated so that unsupported extras do not prevent the page body
    from being displayed. The UI should communicate degraded behavior when it
    matters.

12. **Security is part of opening.** Encryption applies principally to strings
    and streams and is described by the trailer's encryption entry/security
    handler. Password prompts, permissions, JavaScript/actions, embedded files,
    external launches and signatures need explicit policy boundaries. Rendering
    an encrypted document is not authorization to execute its active content.

## ISO 32000-2:2020 extraction

### Scope, conformance and processor responsibilities

The introduction and conformance clauses distinguish a conforming PDF document
from a conforming PDF processor. A processor that renders pages has obligations
around interpreting the imaging model, while an interactive processor has
additional behavior around navigation and user interaction. This distinction is
useful for Ogma: “the file parsed” and “the page rendered correctly” are separate
quality gates.

Practical implications:

- Track open, parse, page-geometry, render, overlay, text-layer and interaction
  failures separately.
- Keep the rendering worker boundary narrow: untrusted PDF parsing/rasterizing
  belongs outside the main UI process.
- Preserve the page when an optional or unsupported feature cannot be rendered;
  record the feature-level diagnostic for later support work.
- Add a corpus of valid, malformed, encrypted, incrementally updated,
  object-stream, transparency-heavy, font-heavy and annotation-heavy PDFs.

### File structure and random access — sections 7.3–7.6, pp. 35–110

A basic PDF has a header, body, cross-reference information and trailer. The
body contains indirect objects; streams hold potentially compressed data. The
trailer points to the catalog/root and the last cross-reference section, and a
reader is expected to begin from the end of the file for ordinary non-linearized
documents. PDF 1.5+ may use object streams and cross-reference streams, so an
implementation cannot assume that every object has a classic textual xref entry.

Incremental updates append new objects and a new xref/trailer chain without
rewriting the original bytes. The `Prev` chain matters: reading the file as a
simple forward stream can miss later replacements. The same object identifier
can have updated copies, so the effective object is resolved through the latest
applicable cross-reference section.

Engineering consequences:

- The document opener must resolve the complete xref/trailer chain before
  declaring page count and catalog metadata reliable.
- Do not use file size or a forward scan as the page index.
- Keep source file identity (path plus content hash/mtime policy) separate from
  render-cache identity. An incremental update changes the effective document
  even if much of the original byte content remains.
- A future editable/annotation writer must append or rewrite transactionally;
  do not overwrite the user's source file in place without an explicit save
  operation.
- Byte-range and stream decompression errors should be reported with object/page
  context to make damaged-document support actionable.

Encryption is described as a trailer-linked security handler with algorithms,
permissions and crypt filters. Numbers and much of the structural scaffolding
remain visible even when substantive strings/streams are encrypted. The UI
should not infer “safe to expose” from a visible catalog, and the worker should
never execute active content simply because it can decrypt a stream.

### Document structure, page tree and resources — sections 7.7–7.8, pp. 111–128

The catalog leads to a page tree. Internal `Pages` nodes contain `Kids` and a
`Count`; leaf `Page` dictionaries define one page. Some page attributes are
inherited from ancestors, so the effective page dictionary is the result of
walking the tree and applying inheritance/defaults. The page tree is a storage
structure, not a semantic chapter/section hierarchy.

Page geometry must be derived from page boxes, especially the effective crop
box for display, plus rotation. Resources are named locally to a content stream
and can include fonts, images, color spaces, patterns, shadings, external
graphics states and form XObjects. Content streams refer to named resources;
the resource name is not a globally meaningful object identifier.

Ogma implications:

- Store effective width, height and rotation in the page read model.
- Make the render request include page index, requested pixel width/height or
  scale, rotation policy, color/background policy and render-engine version.
- Keep overlay coordinates in a normalized/page-space coordinate system. Apply
  display scale and scroll offset only at presentation time.
- Do not recreate shared resource state per wheel tick or page turn.
- Prefetch neighbors after the current page is visible, bounded by a memory
  budget and cancellable when the reader changes book/page rapidly.

### Imaging model, images and transformations — sections 8–11, pp. 161–451

PDF graphics are a sequence of operators acting under a graphics state. Paths,
clipping, transformations, color spaces, images, form XObjects and transparency
compose through the painter's model. Images have their own dimensions, decode
mapping, color space, masks and interpolation behavior; their source resolution
is independent of the output device. Form XObjects are reusable content with
their own resources and transformation matrix.

The same page can therefore be cheap or expensive depending on image size,
filters, nested forms, transparency groups, masks and color conversion. Zooming
does not change PDF geometry; it changes the output device scale and may require
a higher-quality raster.

Implementation guidance:

- Render on a worker thread/process and publish immutable bitmaps to the UI.
- Use low-resolution preview first when the full render is slow, then replace it
  with the full result only if the request is still current.
- Bound width/height and estimate bitmap memory before allocating.
- Preserve aspect ratio and avoid accidental image interpolation artifacts at
  thumbnail sizes; use a dedicated thumbnail width rather than a page-sized
  render scaled down in the UI.
- Include rotation and color/rendering options in cache keys.
- Treat alpha/transparency and page background consistently between full pages,
  thumbnails and export.
- Keep the viewport scroll offset independent from the bitmap; changing offset
  must never trigger a rerender.

### Text, fonts and extraction — sections 9–10, pp. 308–374

PDF text is a glyph-painting model. Text state selects a font and size, text
matrices position and transform glyphs, and showing operators emit character
codes with spacing/kerning adjustments. The visible glyph is not necessarily a
Unicode character. Font dictionaries, encodings, embedded programs, CMaps and
`ToUnicode` mappings determine what can be searched or copied.

Product consequences:

- Keep visual rendering and extracted text as separate layers.
- Store text boxes in page coordinates so zoom and scroll do not change search
  results or annotation geometry.
- Use the text layer for search/selection when mapping is available, but fall
  back gracefully for scanned/image-only pages.
- Expect rotated text, vertical writing, ligatures, custom encodings and
  missing `ToUnicode`; test copy/search with all of them.
- Selection rectangles should be transformed through the same page rotation and
  scale used by the rendered page.

### Viewer preferences, destinations, outlines, thumbnails and page labels — sections 12.2–12.4, pp. 452–479

The standard distinguishes a document's navigation data from its page tree.
Destinations can specify a page and a viewing mode. Relevant modes include a
whole-page fit and horizontal fit (`FitH`), with an optional top coordinate;
there are also vertical and explicit-coordinate variants. Outlines form a
hierarchical table of contents. Page labels can differ from physical page
indices (for example, roman-numbered front matter). Thumbnail images are
optional page-associated image objects.

Required product behavior:

- Keep physical page index for rendering/cache, but expose a page-label field
  when present. The jump box should eventually accept/display both safely.
- Map outline/link destinations to a validated page index and clamp invalid
  coordinates/zoom values.
- Implement “fit width” from the current reading viewport. A fit-width action
  should align the page horizontally and start at the destination's vertical
  position (usually top) without hijacking ordinary wheel scrolling.
- Implement “fit page” as the largest scale that fits both effective page
  dimensions in the viewport, respecting rotation and padding.
- Treat an embedded `/Thumb` as an optional optimization. Generated thumbnails
  are valid product output even when no embedded thumbnail exists.
- Preserve navigation state independently from thumbnail generation; a failed
  thumbnail must not block opening.

### Annotations, forms and signatures — sections 12.5–12.8, pp. 480–728

Annotations live on individual pages and are separate from the page's main
content stream. Link annotations are rectangular hit areas that navigate to
destinations/actions; markup annotations have appearance and/or popup data;
widgets connect interactive form fields to page annotations. Forms and digital
signatures add state, actions, permissions and validation requirements.

For Ogma:

- Keep annotation overlays in a separate visual layer above the page bitmap.
- Map annotation rectangles through page rotation, crop-box origin and display
  scale; do not assume the PDF origin is the UI top-left origin.
- Link hit-testing should be clipped to the page and should not prevent text
  selection unless the user intentionally activates the link.
- Render an annotation's appearance stream when available; use a clear fallback
  for unsupported types.
- Make form fields a future capability boundary. Do not present a non-editable
  widget as if it were an editable form.
- Treat signature validation as security-sensitive and distinct from simply
  showing a signature appearance.

### Metadata, logical structure and accessibility — sections 14.3, 14.7–14.9, pp. 729–817

PDF 2.0 supports document/page metadata streams and logical structure. Tagged
PDF connects marked content to a structure tree and standard structure types;
artifacts, alternate text, language and reading order can support assistive
technology and repurposing. Metadata sources may need reconciliation.

Reader roadmap:

- Show title/author/subject/keywords from the catalog/XMP/info sources with
  explicit source precedence and no blank unlabeled dialog.
- Expose a document language where available and keep UI strings localized.
- Use structure information for a future reading-order/text export mode; do not
  infer reading order solely from y-coordinate sorting on complex layouts.
- Preserve figure alternate text and table/list semantics in future extraction
  artifacts.
- Ensure every toolbar control, page navigation control, zoom option,
  thumbnail and annotation action has a non-empty accessible name.

### Portability and linearized access — Annexes C, F and G, pp. 865–919

The portability advice reinforces that readers encounter files produced by many
generators and should be tolerant of harmless variation. Linearized PDFs put a
linearization parameter dictionary, first-page data and hint streams in a
layout that supports early display and byte-range retrieval. Hint tables can
describe page offsets, shared objects, thumbnails and other object groups.

The access strategy is important: after receiving enough initial information, a
reader can request an arbitrary page's required byte ranges, then draw
incrementally as fonts/images arrive. The recommended progressive sequence is
to activate annotations, draw available contents, substitute missing resources
when possible, draw annotations, then redraw portions after late images/fonts
arrive.

This suggests a future network-reader architecture, but not a reason to add
network complexity to the current local reader. The local equivalent is:

1. open the document/session once;
2. display a first/low-resolution page as soon as possible;
3. prioritize the current page over prefetch;
4. warm the previous/next page render entries;
5. upgrade the visible page when a higher-quality result completes;
6. discard stale results when the book/page/zoom request changes.

The standard also notes that an incremental update appended after linearization
can invalidate hints. Therefore, any future linearization detector must verify
the file length/update state and fall back to ordinary access when necessary.

## PDF Succinctly extraction

### The minimum mental model — pp. 10–22

Hodson's compact model presents the header, body, cross-reference table and
trailer as the four entry points to a PDF. The body contains a page tree, page
objects, resources, content and catalog. The xref enables random object access;
the trailer points at the catalog and xref location; readers commonly discover
the trailer from the end of the file.

The examples make two points that matter for diagnostics:

- PDF object identifiers are object number plus generation number. Generation
  numbers become important when incremental updates reuse object numbers.
- Content streams are postfix operator sequences. The stream's `/Length` is
  metadata needed to delimit the bytes and may itself be filled in by a writer.

For Ogma, log the document/page/object phase rather than reporting a generic
“PDF failed” message. When a page fails, the useful question is whether its
page object, resources, content stream or a referenced stream failed.

### Text and graphics — pp. 23–45

The text examples emphasize explicit font selection, positioning and showing;
there is no high-level paragraph layout in the file format. `Td`, `T*`, `Tm`,
`Tj` and `TJ` affect placement and spacing. The graphics examples similarly
build paths from moves, lines and curves, then paint them. This reinforces that
selection/search and layout require interpretation above the raw operator level.

The practical lesson for the reader is that zooming a rendered page is safe
because the page is already a device-independent description. However, text
selection overlays cannot be computed from the bitmap alone; they need text
positions and font/encoding interpretation.

### Navigation and annotations — pp. 46–52

The document outline is a catalog-linked tree; children and siblings form an
interactive table of contents. Destinations can use whole-page fit, horizontal
fit with a top coordinate, vertical fit, or explicit position/zoom. Hyperlinks
are rectangular link annotations overlaid on a page, not text objects that are
intrinsically “linked.” Text annotations similarly occupy page rectangles and
may expose popup state/icons.

This validates the product split between a page navigation toolbar, a future
outline panel, and page overlay hit testing. It also warns against assuming a
link's visible text bounds are available or that all annotation appearance is
consistent across viewers.

### C# library boundary — pp. 53–61

The final chapter shows the productivity value of a high-level .NET PDF API for
authoring: page dimensions, colors, fonts, embedding, paragraph leading,
spacing, indentation and alignment are handled above raw syntax. It is less
relevant to current read-only rendering than to future export/annotation save,
but the architectural lesson is useful: keep domain intent (page, highlight,
bookmark, citation) independent from the low-level PDF object-writing layer.

## Developing with PDF extraction

### Syntax, file structure and document structure — pp. 17–49

Rosenthol walks from primitive objects and streams through the four file
sections, incremental update, linearization, catalog, page tree, pages and name
dictionary. The reader-facing takeaway is dependency order: resolve the file
index and catalog, then page tree/geometry, then resources/content. A page
cannot be treated as an isolated byte slice unless the required indirect object
dependencies are available.

The book's linearization discussion complements ISO Annex G: linearization
supports first-page and arbitrary-page retrieval but is a layout optimization,
not a different page model. The existing persistent worker/session approach is
aligned with this dependency graph and should remain the default local path.

### Imaging model, images, transformations and transparency — pp. 51–77

Content streams mutate graphics state; transformations place content in user
space/device space; resources and external graphics state affect appearance.
Raster images, JPEGs, masks, form XObjects and transparency groups each add
different cost and fidelity concerns.

Useful performance rules:

- A render request should be immutable and fully keyed.
- A low-res preview is useful for perceived latency, but it must not replace the
  eventual full-quality result.
- Scale the page at render time when text/images are too soft under UI-only
  scaling; avoid rerendering on every scroll offset change.
- Use a page-size-aware cache and evict by memory, not merely item count.
- Add stress fixtures for large JPEG/JPX-like images, masks, forms and alpha.

### Fonts, text and extraction — pp. 79–92

Fonts comprise dictionaries, metrics, encodings and font programs. Embedded
fonts make visual fidelity independent of the client machine. The discussion of
text state, rendering modes and positioning explains why text can look correct
while extraction is wrong if encoding/character identity is missing.

The `/ToUnicode` discussion is especially important for Ogma's future search,
selection and citation features: use Unicode mappings where present, retain
page-space bounding boxes, and mark low-confidence text rather than silently
returning misleading citations.

### Destinations, actions and annotations — pp. 93–120

Destinations identify page, position and magnification. Named destinations
decouple links/bookmarks from direct page references. Actions include internal
GoTo and external URI/launch forms. Outlines are a hierarchical bookmark view.
Annotations have dictionaries and appearance streams; markup, text and
non-markup types vary in behavior.

Do not open external launch actions automatically. URI activation should pass
through an explicit user action and safe-link policy. Annotations should be
loaded as page-local overlay data, not baked into the page bitmap unless the
render engine intentionally includes their appearance.

### AcroForms, embedded files, multimedia, 3D and optional content — pp. 121–181

AcroForms use field dictionaries, field flags, widgets and actions. Embedded
files are reachable through file specifications/name trees and attachment
annotations. Multimedia and 3D are attached through annotation/rendition
structures. Optional content groups have visibility state and configuration;
content can be marked optional in streams, XObjects and annotations.

Roadmap and safety implications:

- Treat forms as a separate interaction subsystem with validation and save
  rules; do not fake editability over static text.
- Show embedded-file metadata/size and require an explicit safe export action.
- Treat multimedia/3D as unsupported-but-preserved until an isolated renderer
  exists.
- When optional-content state is available, render the selected configuration
  consistently in the page bitmap, thumbnails and text layer.

### Tagged PDF and metadata — pp. 183–195

The structure tree associates logical elements with marked content. Metadata
streams complement the document information dictionary and can carry richer
machine-readable information. This is the foundation for accessible reading
order, alternate text and structured export; it should be stored as durable
extraction artifacts rather than regenerated from the bitmap.

## PDF Explained extraction

### Why PDF feels fast — pp. 15–25, 39–52

Whitington explicitly connects PDF's advantages to random access, linearization,
stream creation/incremental update, embedded fonts and searchable text. The
viewer chapter later identifies display, bookmark/hyperlink interaction, search
and copy as the core reader functions.

This gives Ogma a crisp definition of “fast enough”:

- opening should show the reader shell immediately;
- first visible page should appear as soon as a usable preview is available;
- page navigation should reuse a warm session/cache;
- search/copy and navigation are part of reader quality, not post-processing;
- a slow feature (for example full-size rasterization) must not block the shell
  from communicating progress.

### File layout and the read/write lifecycle — pp. 27–52

The simple-PDF walkthrough and file-structure chapter cover header, body, xref,
trailer, streams/filters, incremental update, object/xref streams and
linearized PDFs. The chapter's “how a PDF file is read” section supports the
back-to-front trailer/xref approach and the separation of parsing from page
rendering.

For future PDF editing, incremental update is attractive because small changes
can be appended. It also creates a chain of historical objects and requires
careful xref resolution. Reader-only annotation state can remain in Ogma's own
database until a deliberate PDF export/write feature is implemented.

### Page structure, graphics, images and text — pp. 53–101

The book describes page trees, content streams, operators, transforms, color,
clipping, transparency, form XObjects and image XObjects. Its text chapter
connects glyphs, encodings, embedded fonts, text state, showing/positioning and
Unicode extraction.

Two reader requirements follow:

- Use effective page boxes/rotation and content transforms for geometry. A
  display viewport measured in device-independent UI units is not the same as
  PDF points or raster pixels.
- Use the text extraction layer to support search/copy, but keep visual raster
  fidelity independent of extraction success.

### Metadata and navigation — pp. 103–112

Bookmarks/outlines and destinations are separate from page content. XML/XMP
metadata provides a portable machine-readable layer. Annotations and
hyperlinks are page-associated rectangles. Attachments are part of the document
but do not belong in the ordinary page-render path.

The app should load navigation metadata lazily but early enough that the outline
panel can become useful while the first page is rendering. A malformed outline
entry should be skipped or diagnosed without suppressing the page.

### Encryption and viewer/tooling behavior — pp. 113–135

Encryption covers streams and strings with a security dictionary, permissions
and password-dependent keys. The structural scaffolding may remain inspectable,
but the content is not necessarily available. Viewer features vary in support,
especially around newer forms/annotations and complex graphics, so capability
reporting matters.

The tooling discussion distinguishes rasterizing a page for screen/print from
converting a document to another format. Ogma's worker should remain a renderer,
not silently convert or flatten source PDFs as a side effect of opening.

## Feature specifications derived from the sources

### Magnification controls

Current product behavior should provide:

- Zoom out and zoom in buttons with accessible names and tooltips.
- A visible zoom percentage for fixed zoom.
- “Fit width” and “Fit page” actions.
- A bounded fixed-zoom range (for example 25%–300%, adjustable later).
- Keyboard equivalents (`Ctrl` + `-`, `Ctrl` + `+`, and `Ctrl` + `0` for fit
  width, subject to the app's established shortcut policy).
- Preservation of the focal point/scroll proportion when changing zoom where
  the UI toolkit permits it.
- A cache/render request upgrade at zoom changes, with stale result cancellation.

The UI state model should distinguish:

| State | Meaning | Surface calculation |
| --- | --- | --- |
| Fit width | Scale effective page width to available viewport width | `(viewportWidth - horizontalPadding) / pageWidth` |
| Fit page | Scale both dimensions to fit viewport | `min(availableWidth/pageWidth, availableHeight/pageHeight)` |
| Fixed | User-selected scale independent of viewport | `zoomPercent / 100` |

The actual page dimensions and rotation should be inputs. The current 720×960
fallback should be retained only when page metadata is unavailable, with a
follow-up task to remove the assumption from normal rendering.

### Smooth scrolling

The reading area should be one `ScrollViewer` for the page viewport, not a
wheel-to-page-turn handler at the root. Its expected behavior is:

1. mouse wheel/trackpad input changes the vertical offset smoothly;
2. scrollbar thumb and keyboard scrolling work;
3. zoomed pages can scroll horizontally and vertically;
4. fit-width pages can scroll down through their full height;
5. when the offset is already at the bottom/top, the next wheel gesture may
   navigate to the next/previous page as a boundary convenience;
6. right-side panels keep their own scrolling and do not turn document pages.

The page-turn boundary fallback must be guarded so a wheel event that produces
an actual offset change is never also treated as a page turn. It must also be
debounced/cancelled during an in-flight navigation request.

Do not animate every wheel event in application code. Native `ScrollViewer`
handling gives better trackpad behavior and avoids fighting the platform's
input coalescing. If a future smooth-scroll animation is added, it should be
interruptible and driven by a bounded offset target, not a chain of delayed
page renders.

### Thumbnails

Thumbnail generation should follow this pipeline:

1. Resolve document identity and page count without blocking the catalogue UI.
2. Prefer a valid embedded page thumbnail when available.
3. Otherwise render the page at a small fixed maximum width using the same
   isolated worker/session boundary.
4. Store a versioned thumbnail artifact keyed by document hash, page index,
   render-engine version and thumbnail width.
5. Show a labelled loading/failure state in the catalogue; never show an empty
   button/dialog with only colored controls.
6. Retry only known transient/failure states and do not enqueue an unbounded
   repair loop.

The thumbnail path must not compete with the current reader page at the same
priority. Cover/spine/thumbnail work should be low priority and cancellable.

### Navigation and page identity

The page index used by the renderer should remain zero-based and stable. Add
these fields to the page/document metadata model when supported:

- physical page index;
- PDF page label;
- effective crop/media box;
- rotation;
- outline/destination references;
- optional thumbnail availability;
- page content hash/render version.

The existing first/previous/next/last controls are the essential fallback. The
next layer is an outline panel, then named destinations and page labels, then
link activation and history/back-forward navigation.

## Traceability to the current Ogma architecture

| Source-derived need | Current/target boundary | Acceptance evidence |
| --- | --- | --- |
| Warm document/session | `OgmaLibrary.Infrastructure.Pdf` worker session and `ReaderSessionService` | Repeated neighbor navigation does not create a new worker per page |
| Deduplicated preview/full render | `PageRenderCache` | Concurrent foreground/prefetch requests share one full render; visible preview upgrades safely |
| Isolated rendering | `PdfWorkerClient`/`PdfWorkerCommand` | UI remains responsive; worker failure is translated to page-level status |
| Viewport-based fit width | `ReaderView` page `ScrollViewer` + `ReaderViewModel` geometry | Resizing the reading area changes page width to the usable viewport; sidebar width is excluded |
| Smooth wheel scroll | page-only `ScrollViewer` handler | Middle-of-page wheel changes offset without changing page index; boundary gesture can turn page |
| Zoom controls | `ReaderViewModel` `ZoomMode`/`ZoomPercent` and toolbar | Buttons/mode labels have text and automation names; session progress persists mode/percent |
| Correct overlay mapping | `AnnotationOverlays`, `TextSelectionService` | Fixed zoom/rotation/fit-width selection and annotation tests agree with page coordinates |
| Optional thumbnails | catalogue thumbnail services/jobs | Missing embedded thumbnails still produce generated previews or a labelled failure state |
| Accessible interaction | Avalonia automation properties and localized labels | UI tests assert non-empty names on nav/zoom/thumbnail controls |
| PDF feature tolerance | worker diagnostics + page status | Unsupported optional feature does not blank the page; diagnostics identify feature/page |

## Test plan

### Unit and view-model tests

- Fixed zoom clamps to lower/upper limits and uses predictable increments.
- Fit-width width equals usable viewport width less padding for portrait and
  landscape pages.
- Fit-page scale is the minimum of width and height scales.
- Rotation swaps effective width/height where required.
- Zoom changes call `UpdateZoom` once with the effective mode/percent.
- Scroll offset is persisted without triggering a page render.
- Stale render completion cannot replace a newer page/zoom result.
- Annotation/selection normalized coordinates are invariant under zoom.

### UI tests

- The page surface is inside exactly one page `ScrollViewer`; the sidebar's
  `ScrollViewer` remains independent.
- Zoom controls visibly contain text or a percentage and have automation names.
- Resizing the reader changes fit-width geometry.
- Wheel input in the middle of a tall/zoomed page changes scroll offset while
  `CurrentPageIndex` remains unchanged.
- Wheel input at the bottom/top invokes adjacent-page navigation only once.
- Mouse wheel over the sidebar does not navigate the PDF page.
- Page navigation buttons and jump input continue to work while the page is
  loading.
- Double-clicking a catalogue card/row opens the reader.
- Thumbnail loading, success and failure states all contain visible text.
- No hidden share/file-stream dialog is focusable or blocks the catalogue.

### Rendering corpus

Add or retain fixtures covering:

- portrait, landscape, rotated and non-letter page boxes;
- scanned image-only pages and text with valid/missing `ToUnicode`;
- embedded/subset fonts, vertical text and ligatures;
- large JPEG/JPX-like images, masks, transparency and form XObjects;
- classic xref, xref streams, object streams and incremental updates;
- linearized and non-linearized files;
- encrypted files with wrong/empty/correct password paths;
- outlines, named destinations, page labels, links and markup annotations;
- tagged PDF, logical structure and alternate text;
- optional-content groups;
- malformed/damaged files that should fail with a useful message.

## Deliberate non-goals for this iteration

- Implementing a complete PDF 2.0 parser in Ogma; the isolated renderer remains
  responsible for format breadth.
- Editing/writing source PDFs or flattening annotations into them.
- Executing PDF JavaScript, launch actions, multimedia or 3D content.
- Treating a document's page tree as its semantic table of contents.
- Claiming that every PDF opens instantly: complex pages can still require
  expensive rasterization. The goal is immediate shell feedback, warm-session
  reuse, preview-first display and responsive scrolling/navigation.

## Open engineering risks

1. The current reader surface uses a fallback 720×960 geometry. It should be
   replaced by effective PDF page dimensions for accurate fit width, fit page,
   rotation and selection.
2. The underlying renderer's support for annotations, forms, optional content,
   tagged structure and PDF 2.0 extensions needs a capability matrix and corpus
   evidence rather than assumptions from the file parser.
3. Persistent workers reduce repeated startup/document-load cost, but the first
   render of a difficult page can still be slow. Preview-first and measured
   cache behavior need real user-document benchmarks.
4. Progress persistence currently stores a single scroll offset/page state. A
   future continuous-scroll mode may need per-page/continuous document position
   semantics, especially if multiple pages are eventually mounted in one
   scrollable document.
5. XMP/info/catalog metadata precedence and page-label localization need an
   explicit contract before they are exposed in user-facing panels.

## Next implementation slice

For the current request, the smallest source-aligned change is:

1. add a page-only `ScrollViewer` around `PageSurface`;
2. remove root-level wheel interception and only use a boundary fallback;
3. expose zoom-out, zoom percentage, zoom-in, fit-width and fit-page controls;
4. calculate fit width/page from the measured reading viewport, with the current
   page geometry fallback;
5. persist zoom mode/percent and scroll offset through the existing session;
6. add view-model/UI tests for geometry, controls and smooth wheel behavior;
7. build, run focused reader tests, launch the app and manually verify with a
   real multi-page PDF.
