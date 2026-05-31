namespace OgmaLibrary.Application.Navigation;

/// <summary>
/// Navigates the shell to the PDF reader (FR-CAT-001; reader is Phase 08).
/// The interface is defined here in Phase 06 so book-detail panels can wire
/// the "Read" button now; the reader implementation arrives in Phase 08.
/// </summary>
public interface IReaderNavigationService
{
    /// <summary>
    /// Opens the PDF reader for the specified book.
    /// In Phase 06 this routes to a "reader coming in Phase 08" placeholder.
    /// </summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="pageHint">Zero-based page to open at, or <see langword="null"/> for last position.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task OpenReaderAsync(string bookId, int? pageHint = null, CancellationToken cancellationToken = default);
}
