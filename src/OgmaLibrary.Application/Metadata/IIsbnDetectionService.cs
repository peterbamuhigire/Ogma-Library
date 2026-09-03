using OgmaLibrary.Domain;

namespace OgmaLibrary.Application.Metadata;

/// <summary>
/// The source from which an ISBN candidate was found (FR-META-001).
/// Lower numeric value = higher priority in the detection pipeline.
/// </summary>
public enum IsbnSource
{
    /// <summary>Extracted from the PDF's DocumentInformation dictionary (Subject/Keywords/Title).</summary>
    DocInfo = 0,

    /// <summary>Extracted from the PDF's XMP metadata packet.</summary>
    Xmp = 1,

    /// <summary>Found in the text of the PDF's first two pages.</summary>
    FirstPage = 2,

    /// <summary>Found in the PDF file's base name.</summary>
    Filename = 3,
}

/// <summary>
/// A ranked ISBN candidate with the source from which it was detected.
/// </summary>
/// <param name="Isbn">The validated, normalized ISBN value object.</param>
/// <param name="Source">The detection source.</param>
public sealed record IsbnCandidate(Isbn Isbn, IsbnSource Source);

/// <summary>
/// The result of running ISBN detection across all four sources for a PDF file
/// (FR-META-001). Candidates are sorted by source priority (DocInfo first).
/// </summary>
/// <param name="BestIsbn">
/// The highest-priority validated ISBN, or <see langword="null"/> when none was found.
/// </param>
/// <param name="AllCandidates">
/// All unique validated ISBNs found, ordered by source priority then lexicographic order.
/// </param>
/// <param name="SourceRanked">
/// The detection sources that contributed at least one valid candidate, in priority order.
/// </param>
public sealed record IsbnDetectionResult(
    Isbn? BestIsbn,
    IReadOnlyList<IsbnCandidate> AllCandidates,
    IReadOnlyList<IsbnSource> SourceRanked);

/// <summary>Persists ranked ISBN observations without overwriting canonical metadata.</summary>
public interface IIsbnEvidenceStore
{
    /// <summary>Replaces the evidence for one versioned extraction artifact.</summary>
    Task ReplaceAsync(
        string bookId,
        long extractionArtifactId,
        IReadOnlyList<IsbnCandidate> candidates,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Detects, normalizes, validates, and ranks ISBN-10 and ISBN-13 candidates from four
/// sources within a PDF file: DocumentInformation, XMP, first-page text, and filename
/// (FR-META-001). Invalid check digits are silently discarded.
/// </summary>
public interface IIsbnDetectionService
{
    /// <summary>
    /// Scans all four ISBN sources in the PDF at <paramref name="absoluteFilePath"/>
    /// and returns a ranked result.
    /// </summary>
    /// <param name="absoluteFilePath">Absolute path to the PDF file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// An <see cref="IsbnDetectionResult"/> containing the best ISBN and all validated
    /// candidates ranked by source priority.
    /// </returns>
    Task<IsbnDetectionResult> DetectAsync(
        string absoluteFilePath,
        CancellationToken cancellationToken = default);
}
