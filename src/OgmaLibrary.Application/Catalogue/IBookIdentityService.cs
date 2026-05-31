namespace OgmaLibrary.Application.Catalogue;

/// <summary>
/// The result of attempting to resolve the identity of a file against the
/// catalogue (HLD §4 — five-tier identity chain, FR-LIB-003).
/// </summary>
public abstract record BookMatchResult
{
    private BookMatchResult() { }

    /// <summary>
    /// The file exactly matches a known book by relative path (tier 1),
    /// SHA-256 hash (tier 2), or mtime fast-path (tier 3).
    /// </summary>
    /// <param name="BookId">The stable identity of the matched book.</param>
    public sealed record ExactMatch(string BookId) : BookMatchResult;

    /// <summary>
    /// The file matches a known book with partial confidence, typically via the PDF
    /// fingerprint (tier 4) or ISBN/DOI (tier 5).
    /// </summary>
    /// <param name="BookId">The stable identity of the best-matching book.</param>
    /// <param name="Confidence">Match confidence in [0.0, 1.0].</param>
    public sealed record FuzzyMatch(string BookId, float Confidence) : BookMatchResult;

    /// <summary>No existing book in the catalogue matches the file; it should be imported.</summary>
    public sealed record NewBook : BookMatchResult;

    /// <summary>The file type is not supported or identity cannot be determined.</summary>
    /// <param name="Reason">A human-readable explanation.</param>
    public sealed record Unresolvable(string Reason) : BookMatchResult;
}

/// <summary>
/// Resolves the stable identity of a file against the catalogue using the five-tier
/// identity chain (HLD §4): relative path → SHA-256 → size+mtime → PDF fingerprint → ISBN/DOI.
/// </summary>
/// <remarks>
/// The service is deterministic: given the same file and catalogue state, it always
/// returns the same result. A book that has been renamed or moved is re-matched via
/// its SHA-256 content hash without user intervention (FR-LIB-003).
/// </remarks>
public interface IBookIdentityService
{
    /// <summary>
    /// Resolves the catalogue identity of a file by walking the five-tier chain.
    /// Returns the first successful match tier.
    /// </summary>
    /// <param name="absoluteFilePath">The absolute path to the file to resolve.</param>
    /// <param name="libraryRootPath">The absolute path to the library root folder.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="BookMatchResult"/> discriminated union representing the match tier
    /// and confidence.
    /// </returns>
    Task<BookMatchResult> ResolveAsync(
        string absoluteFilePath,
        string libraryRootPath,
        CancellationToken cancellationToken = default);
}
