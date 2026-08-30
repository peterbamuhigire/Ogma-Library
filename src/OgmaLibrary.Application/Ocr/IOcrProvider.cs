using OgmaLibrary.Application.Extensions;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Application.Search;

namespace OgmaLibrary.Application.Ocr;

/// <summary>OCR provider boundary for Phase 15 scanned-PDF text recognition.</summary>
[ExtensionPoint]
internal interface IOcrProvider
{
    /// <summary>Recognizes text from a rendered page image.</summary>
    /// <param name="pageImage">Rendered page image stream, usually PNG.</param>
    /// <param name="languageHint">Tesseract language key such as <c>eng</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recognized text and confidence.</returns>
    Task<OcrPageResult> RecognizeAsync(
        Stream pageImage,
        string languageHint,
        CancellationToken cancellationToken = default);
}

/// <summary>OCR result for one rendered PDF page.</summary>
/// <param name="Text">Recognized plain text.</param>
/// <param name="Confidence">Provider confidence in [0.0, 1.0].</param>
public sealed record OcrPageResult(string Text, double Confidence);

/// <summary>Allowed local OCR language-pack policy.</summary>
public static class OcrLanguagePolicy
{
    private static readonly HashSet<string> SupportedLanguages =
        ["deu", "eng", "fra", "ita", "spa"];

    /// <summary>Maximum serialized language selector length.</summary>
    public const int MaximumLanguageSelectorLength = 32;

    /// <summary>Returns a canonical language selector or <see langword="null"/>.</summary>
    public static string? Normalize(string? languageHint)
    {
        if (string.IsNullOrWhiteSpace(languageHint) ||
            languageHint.Length > MaximumLanguageSelectorLength)
        {
            return null;
        }

        string[] languages = languageHint
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(language => language.ToLowerInvariant())
            .ToArray();
        return languages.Length > 0 &&
               languages.All(language => SupportedLanguages.Contains(language))
            ? string.Join('+', languages.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            : null;
    }
}

/// <summary>Deterministic local policy for deciding whether a page needs OCR.</summary>
public static class OcrPageQualityPolicy
{
    /// <summary>Minimum confidence required before OCR replaces missing primary text.</summary>
    public const double MinimumSelectionConfidence = 0.75;

    /// <summary>
    /// Returns whether OCR should render and process a page based on its native
    /// text-layer classification.
    /// </summary>
    public static bool ShouldProcess(ExtractionQuality quality, int wordCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(wordCount);
        return quality is ExtractionQuality.Scanned or ExtractionQuality.Empty ||
               quality == ExtractionQuality.Partial && wordCount < 12;
    }

    /// <summary>
    /// Chooses OCR only when primary text is unavailable or OCR is materially
    /// more trustworthy than a partial primary extraction.
    /// </summary>
    public static bool ShouldSelectOcr(
        SearchExtractionQuality primaryQuality,
        int primaryWordCount,
        string? ocrText,
        double ocrConfidence)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(primaryWordCount);
        if (string.IsNullOrWhiteSpace(ocrText) || ocrConfidence < MinimumSelectionConfidence)
        {
            return false;
        }

        return primaryQuality is SearchExtractionQuality.Empty or SearchExtractionQuality.Scanned or SearchExtractionQuality.Failed ||
               primaryWordCount == 0 ||
               primaryQuality == SearchExtractionQuality.Partial && primaryWordCount < 12;
    }
}
