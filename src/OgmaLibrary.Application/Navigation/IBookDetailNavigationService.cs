namespace OgmaLibrary.Application.Navigation;

/// <summary>
/// Navigates the shell to the book-detail panel or the reader (FR-CAT-001).
/// All catalogue views (grid, list, directory, 3D) route through this single
/// interface so no view holds a direct reference to the shell or another view.
/// </summary>
public interface IBookDetailNavigationService
{
    /// <summary>Opens the book-detail panel for the specified book.</summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task OpenDetailAsync(string bookId, CancellationToken cancellationToken = default);
}
