# Phase 16 Evidence - Real-Worker Disk Generation Budget

Date: 2026-09-06

## Gate addressed

The prior 50,000-book benchmark measured manifest lookup only. It did not
generate image bytes and therefore could not establish the disk cost of the
bounded visual-asset variants.

## Method

`Phase16VisualAssetDiskBudgetTests` runs the production isolated PDF worker
against all three versioned PDFium corpus files:

- `gc-large.pdf`
- `gc-simple-text.pdf`
- `gc-two-column.pdf`

For each PDF it generates and registers the complete supported local set:

- 200 x 300 default cover;
- 400 x 600 detail cover;
- 7 x 100 default spine;
- 14 x 200 retina spine.

The worker's existing output boundary decodes every image and verifies its
exact dimensions before registration. The benchmark then resolves all four
manifest entries, reads their actual encoded file sizes, and enforces a
512-KiB per-book aggregate ceiling. The 50,000-book projection uses the
largest measured corpus result, not the average.

## Result

```text
corpus=3
maxBytesPerBook=78274
projected50kGiB=3.645
budgetBytesPerBook=524288

gc-large.pdf       75699 B   19255 ms
gc-simple-text.pdf 78274 B    5040 ms
gc-two-column.pdf  74582 B    6214 ms
```

Command:

```text
dotnet test tests\OgmaLibrary.Tests\OgmaLibrary.Tests.csproj \
  --configuration Release --no-restore \
  --filter "FullyQualifiedName~Phase16VisualAssetDiskBudgetTests" \
  --logger "console;verbosity=normal" -m:1
```

Result: 1 passed, 0 failed, 0 skipped in 33.5 seconds.

## Gate disposition

The real-worker bounded disk-generation subgate is closed locally. The maximum
sample projects to 3.645 GiB for 50,000 books, below the explicit 24.414-GiB
ceiling implied by 512 KiB per book. This is encoded-size evidence for the
versioned corpus; it is not a guarantee for every possible PDF.

GPU/texture residency, physical UI journeys, reference-hardware throughput,
and cross-platform CI execution of this new benchmark remain open. Those are
not inferred from disk output.
