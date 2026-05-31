namespace OgmaLibrary.Application.Reader;

/// <summary>
/// Resolves a book's primary PDF file path from its stable identity.
/// The Reader bounded context uses this contract to locate the PDF file without
/// owning book identity (Catalogue is the single source of truth, HLD §3.2).
/// </summary>
public interface IBookFileLocator
{
    /// <summary>
    /// Returns the absolute path to the primary PDF file for the specified book.
    /// </summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>
    /// The absolute file path, or <see langword="null"/> if the book has no available
    /// file (e.g. the file was removed after ingestion).
    /// </returns>
    Task<string?> LocateAsync(string bookId, CancellationToken ct);
}
