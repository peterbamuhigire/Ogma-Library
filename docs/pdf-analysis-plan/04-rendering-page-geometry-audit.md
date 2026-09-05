# Rendering and page geometry audit

## What the standard model requires

A PDF page is not intrinsically a 3:4 bitmap. Its effective page dictionary is
formed from the page tree and inherited attributes. MediaBox/CropBox, rotation,
resources, content streams and annotations determine what a processor displays
and how document coordinates map to device pixels.

## Current implementation

`PdfiumAdapter` renders with PDFtoImage and obtains page dimensions through
PdfPig. The render request carries width, height, scale and preview status. The
current options use aspect-ratio preservation, anti-aliasing and no annotations,
no form fill and no tiling. Reader geometry still has a 720×960 fallback and
overlay code uses normalized coordinates based on fixed surface dimensions.

## Gaps and user impact

| Gap | Failure mode |
|---|---|
| No explicit effective CropBox/MediaBox/origin model | Fit width and clipping can be wrong on cropped/non-letter pages |
| Rotation is queried separately, not part of the render contract | Bitmap, overlay and page dimensions can disagree |
| No render intent/color/optional-content state in cache key | Same page can be incorrectly reused across display modes |
| Annotation/form rendering disabled | A user sees a page unlike another PDF reader |
| No tiled rendering | Large pages create latency and memory spikes |
| Whole-file bytes and duplicate PdfPig lazies | First open and page turns can pay avoidable cost |
| Fixed fallback geometry in UI | Selection, annotations and zoom drift on unusual pages |
| No explicit partial render state | A blank/placeholder can look like a broken PDF |

## Required target

Define one immutable `EffectivePageGeometry` per physical page:

```text
pageIndex, pageLabel, mediaBox, cropBox, trimBox?, bleedBox?, artBox?
rotation, userUnit, displayWidthPoints, displayHeightPoints,
coordinateTransformVersion, contentHash, parserVersion
```

Define one `RenderRequest` that includes page box, rotation policy, pixel size,
color/background policy, annotations/forms policy, optional-content state,
preview/full quality, tile rectangle and cancellation/deadline. Its cache key
must include every visual input.

The UI should compute fit width and fit page from the measured reading viewport
after sidebar/padding, then request a raster appropriate to the display size.
Changing zoom should preserve the focal point, invalidate only the affected
render key, and keep text/annotation overlays on the same transform.

## Acceptance tests

- portrait, landscape, rotated 90/180/270, crop-offset and unusual page sizes;
- fit width after resizing and sidebar changes;
- zoom in/out while preserving focal point and scroll position;
- annotation/text selection alignment at fixed and fitted zoom;
- transparency, masks, large images, fonts, patterns and form XObjects;
- annotations/forms visible or intentionally hidden according to explicit policy;
- large page tiled/preview/full upgrade without UI thread blocking;
- no stale render may replace a newer page/zoom request.

## Reader experience decision

The correct smooth behavior is a real scrollable viewport. Mouse-wheel and
trackpad deltas must update the offset continuously; only a wheel gesture at a
confirmed page boundary may turn to the adjacent page. A future continuous
document mode should mount a virtualised sequence of page surfaces, not chain
delayed page-turn animations. The existing page-only implementation is a
valuable intermediate state, not the final PDF-reader experience.
