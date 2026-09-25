using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Diagnostics;
using OgmaLibrary.Infrastructure.Pdf;

namespace OgmaLibrary.Infrastructure.Metadata;

/// <summary>
/// PdfPig-backed implementation of <see cref="IIsbnDetectionService"/> that scans
/// four sources in priority order: DocInfo > XMP > first two pages of text >
/// filename. Invalid check digits are silently discarded (FR-META-001).
/// </summary>
public sealed class IsbnDetectionService : IIsbnDetectionService
{
    private readonly ILogger _logger;
    private readonly IPdfRendererFactory _rendererFactory;

    /// <summary>Initializes ISBN detection behind the configured PDF boundary.</summary>
    public IsbnDetectionService(
        IPdfRendererFactory? rendererFactory = null,
        ILogger<IsbnDetectionService>? logger = null)
    {
        _rendererFactory = rendererFactory ?? new PdfiumAdapterFactory();
        _logger = logger ?? (ILogger)NullLogger.Instance;
    }

    // Compiled once: matches ISBN-10 or ISBN-13 with optional hyphens/spaces.
    // Group 1 captures the raw ISBN string for normalization.
    private static readonly Regex IsbnRegex = new(
        @"\bISBN[-: ]?(?:97[89][-\s]?)?\d{1,5}[-\s]?\d{1,7}[-\s]?\d{1,7}[-\s]?[\dX]\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    // Fallback: any 10 or 13 consecutive digits (possibly separated by hyphens/spaces).
    private static readonly Regex DigitsOnlyRegex = new(
        @"(?<!\d)(?:97[89][-\s]?)?\d{1,5}[-\s]?\d{1,7}[-\s]?\d{1,6}[-\s]?[\dX](?!\d)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    /// <inheritdoc />
    public Task<IsbnDetectionResult> DetectAsync(
        string absoluteFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absoluteFilePath);

        return Task.Run(() => DetectCore(absoluteFilePath), cancellationToken);
    }

    private IsbnDetectionResult DetectCore(string filePath)
    {
        // Collect candidates per source. Use a dict of source → first candidate found
        // (a file rarely has multiple valid ISBNs from the same source).
        var bySource = new Dictionary<IsbnSource, List<Isbn>>();

        // Source 4: filename (lowest priority)
        string baseName = Path.GetFileNameWithoutExtension(filePath);
        AddFromText(baseName, IsbnSource.Filename, bySource);

        // Sources 1–3 require opening the PDF
        try
        {
            using IPdfRenderer renderer = _rendererFactory.Open(filePath);
            PdfDocumentMetadata metadata = renderer.ReadDocumentMetadata();

            // Source 1: DocInfo (highest priority)
            string docInfoText = string.Join(" ",
                new[] { metadata.Title, metadata.Subject, metadata.Keywords, metadata.Author }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
            AddFromText(docInfoText, IsbnSource.DocInfo, bySource);

            // Source 2: XMP via TryGetXmpMetadata
            if (!string.IsNullOrWhiteSpace(metadata.XmpXml))
            {
                AddFromText(metadata.XmpXml, IsbnSource.Xmp, bySource);
            }

            // Source 3: first 2 pages of text
            int pagesToScan = Math.Min(2, renderer.PageCount);
            var firstPageText = new System.Text.StringBuilder();
            for (int i = 0; i < pagesToScan; i++)
            {
                try
                {
                    TextLayer layer = renderer.ExtractTextLayer(i);
                    firstPageText.AppendJoin(' ', layer.Words.Select(word => word.Text));
                    firstPageText.Append(' ');
                }
                catch (Exception exception)
                {
                    // Intentionally ignored: one unreadable page must not stop ISBN detection.
                    InfrastructureLog.BestEffortStepFailed(_logger, exception, nameof(IsbnDetectionService), "metadata.isbn.page_text");
                }
            }

            AddFromText(firstPageText.ToString(), IsbnSource.FirstPage, bySource);
        }
        catch (Exception exception)
        {
            // Intentionally ignored: if the file cannot be opened we still return the filename candidate.
            InfrastructureLog.BestEffortStepFailed(_logger, exception, nameof(IsbnDetectionService), "metadata.isbn.open");
        }

        // Build the sorted candidate list (DocInfo first, then XMP, FirstPage, Filename).
        // De-duplicate across sources: the same ISBN from a higher-priority source wins.
        var seenNormalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allCandidates = new List<IsbnCandidate>();
        var sourcesContributing = new List<IsbnSource>();

        foreach (IsbnSource source in Enum.GetValues<IsbnSource>().OrderBy(s => (int)s))
        {
            if (!bySource.TryGetValue(source, out var isbns))
            {
                continue;
            }

            bool addedFromSource = false;
            foreach (Isbn isbn in isbns)
            {
                if (seenNormalized.Add(isbn.Normalized))
                {
                    allCandidates.Add(new IsbnCandidate(isbn, source));
                    addedFromSource = true;
                }
            }

            if (addedFromSource)
            {
                sourcesContributing.Add(source);
            }
        }

        Isbn? best = allCandidates.Count > 0 ? allCandidates[0].Isbn : null;

        return new IsbnDetectionResult(
            BestIsbn: best,
            AllCandidates: allCandidates,
            SourceRanked: sourcesContributing);
    }

    private static void AddFromText(
        string text,
        IsbnSource source,
        Dictionary<IsbnSource, List<Isbn>> bySource)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var found = ExtractIsbnCandidates(text);
        if (found.Count == 0)
        {
            return;
        }

        if (!bySource.TryGetValue(source, out var list))
        {
            list = [];
            bySource[source] = list;
        }

        list.AddRange(found);
    }

    /// <summary>
    /// Extracts and validates ISBN candidates from arbitrary text.
    /// Returns only those that pass check-digit validation.
    /// </summary>
    internal static IReadOnlyList<Isbn> ExtractIsbnCandidates(string text)
    {
        var result = new List<Isbn>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Try with ISBN prefix first (more reliable), then digit-only patterns.
        foreach (Match match in IsbnRegex.Matches(text))
        {
            TryAddIsbn(match.Value, result, seen);
        }

        // Also try bare digit runs that look like ISBNs (no prefix).
        foreach (Match match in DigitsOnlyRegex.Matches(text))
        {
            TryAddIsbn(match.Value, result, seen);
        }

        return result;
    }

    private static void TryAddIsbn(string raw, List<Isbn> result, HashSet<string> seen)
    {
        if (Isbn.TryParse(raw, out Isbn isbn) && seen.Add(isbn.Normalized))
        {
            result.Add(isbn);
        }
    }
}
