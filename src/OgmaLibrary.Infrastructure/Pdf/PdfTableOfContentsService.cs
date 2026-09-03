using OgmaLibrary.Application.Search;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Outline;

namespace OgmaLibrary.Infrastructure.Pdf;

/// <summary>PdfPig outline adapter with bounded, sanitized TOC output.</summary>
public sealed class PdfTableOfContentsService : ITocExtractionService
{
    private const int MaxEntries = 2048;
    private const int MaxTitleLength = 512;

    /// <inheritdoc />
    public Task<TocExtractionResult> ExtractAsync(
        string absoluteFilePath,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => ExtractCore(absoluteFilePath, cancellationToken), cancellationToken);

    private static TocExtractionResult ExtractCore(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            using PdfDocument document = PdfDocument.Open(
                filePath,
                new ParsingOptions { UseLenientParsing = true });
            if (!document.TryGetBookmarks(out Bookmarks? bookmarks) || bookmarks is null)
            {
                return new TocExtractionResult([], TocExtractionQuality.Empty);
            }

            var entries = new List<TocEntryRecord>(Math.Min(MaxEntries, bookmarks.Roots.Count));
            bool skippedEntry = false;
            foreach (BookmarkNode node in bookmarks.GetNodes())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entries.Count >= MaxEntries)
                {
                    skippedEntry = true;
                    break;
                }

                string title = NormalizeTitle(node.Title);
                if (node is not DocumentBookmarkNode documentNode ||
                    title.Length == 0 ||
                    documentNode.PageNumber < 1 ||
                    documentNode.PageNumber > document.NumberOfPages)
                {
                    skippedEntry = true;
                    continue;
                }

                entries.Add(new TocEntryRecord(
                    title,
                    documentNode.PageNumber - 1,
                    Math.Clamp(node.Level, 0, 32)));
            }

            TocExtractionQuality quality = entries.Count == 0
                ? TocExtractionQuality.Empty
                : skippedEntry ? TocExtractionQuality.Partial : TocExtractionQuality.Complete;
            return new TocExtractionResult(entries, quality);
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
