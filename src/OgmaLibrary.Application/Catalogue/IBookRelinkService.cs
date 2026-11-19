namespace OgmaLibrary.Application.Catalogue;

/// <summary>Outcome of relinking a catalogued book to a moved or renamed PDF.</summary>
public enum BookRelinkResult
{
    /// <summary>The file matched the catalogue hash and the book's file row now points at it.</summary>
    Relinked = 0,

    /// <summary>The chosen file's SHA-256 differs from the catalogued content; nothing changed.</summary>
    HashMismatch = 1,

    /// <summary>The chosen file is not inside an enabled library folder; nothing changed.</summary>
    OutsideLibraryRoots = 2,

    /// <summary>The chosen file or the book does not exist; nothing changed.</summary>
    NotFound = 3,

    /// <summary>The file could not be read or the catalogue could not be updated; nothing changed.</summary>
    Failed = 4,
}

/// <summary>
/// Relinks one catalogued book to a PDF the user located after it moved (Sept-23 Phase 11,
/// task 11.7). Only a file inside an enabled library root whose SHA-256 matches the catalogue
/// is accepted, so a relink can never change which content a book identity refers to.
/// </summary>
public interface IBookRelinkService
{
    /// <summary>Validates <paramref name="candidatePath"/> and, when it matches, relinks the book.</summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="candidatePath">The absolute path the user chose. Untrusted.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The relink outcome; only <see cref="BookRelinkResult.Relinked"/> changes the catalogue.</returns>
    Task<BookRelinkResult> RelinkAsync(string bookId, string candidatePath, CancellationToken cancellationToken = default);
}
