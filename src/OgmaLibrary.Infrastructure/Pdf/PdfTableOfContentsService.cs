using OgmaLibrary.Application.Reader;
using OgmaLibrary.Application.Search;

namespace OgmaLibrary.Infrastructure.Pdf;

/// <summary>Bounded, sanitized TOC output over the configured PDF boundary.</summary>
public sealed class PdfTableOfContentsService : ITocExtractionService
{
    private const int MaxEntries = 2048;
    private const int MaxTitleLength = 512;
    private readonly IPdfRendererFactory _rendererFactory;

    /// <summary>Initializes the TOC service at the configured PDF boundary.</summary>
    public PdfTableOfContentsService(IPdfRendererFactory? rendererFactory = null)
    {
        _rendererFactory = rendererFactory ?? new PdfiumAdapterFactory();
    }

    /// <inheritdoc />
    public Task<TocExtractionResult> ExtractAsync(
        string absoluteFilePath,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => ExtractCore(absoluteFilePath, cancellationToken), cancellationToken);

    private TocExtractionResult ExtractCore(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            using IPdfRenderer renderer = _rendererFactory.Open(filePath);
            if (renderer.PageCount <= 0)
            {
                return new TocExtractionResult([], TocExtractionQuality.Failed, "No readable pages");
            }

            IReadOnlyList<PdfOutlineEntry> outline = renderer.ReadOutline();
            if (outline.Count == 0)
            {
                return new TocExtractionResult([], TocExtractionQuality.Empty);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return new TocExtractionResult(
                outline
                    .Take(MaxEntries)
                    .Select(entry => new TocEntryRecord(
                        NormalizeTitle(entry.Title),
                        entry.PageIndex,
                        Math.Clamp(entry.Level, 0, 32)))
                    .Where(entry => entry.Title.Length > 0 &&
                                    entry.PageIndex >= 0 &&
                                    entry.PageIndex < renderer.PageCount)
                    .ToArray(),
                outline.Count > MaxEntries
                    ? TocExtractionQuality.Partial
                    : TocExtractionQuality.Complete);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new TocExtractionResult([], TocExtractionQuality.Failed, ex.GetType().Name);
        }
    }

    private static string NormalizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        string normalized = title.Normalize(System.Text.NormalizationForm.FormC).Trim();
        return normalized.Length <= MaxTitleLength
            ? normalized
            : normalized[..MaxTitleLength];
    }
}
