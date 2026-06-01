namespace OgmaLibrary.Application.Navigation;

/// <summary>
/// Navigates the shell to the PDF reader (FR-CAT-001; reader is Phase 08).
/// Book-detail panels use this boundary so catalogue views can open the reader
/// without referencing reader view models directly.
/// </summary>
public interface IReaderNavigationService
{
    /// <summary>Opens the PDF reader for the specified book.</summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="pageHint">Zero-based page to open at, or <see langword="null"/> for last position.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task OpenReaderAsync(string bookId, int? pageHint = null, CancellationToken cancellationToken = default);
}
