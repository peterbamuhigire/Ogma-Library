using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure.Catalogue;

namespace OgmaLibrary.Infrastructure.Pdf;

/// <summary>
/// Resolves a book's primary PDF file path by querying the catalogue and combining
/// the relative path with the library root (Reader bounded-context contract).
/// </summary>
public sealed class BookFileLocator : IBookFileLocator
{
    private readonly CatalogueDbContext _db;
    private readonly ILibrarySettingsService _settings;

    /// <summary>
    /// Initializes a new instance of <see cref="BookFileLocator"/>.
    /// </summary>
    /// <param name="db">The catalogue database context.</param>
    /// <param name="settings">The library settings service (provides library root).</param>
    public BookFileLocator(CatalogueDbContext db, ILibrarySettingsService settings)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(settings);
        _db = db;
        _settings = settings;
    }

    /// <inheritdoc />
    public async Task<string?> LocateAsync(string bookId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        string? libraryRoot = await _settings.GetLibraryRootAsync(ct).ConfigureAwait(false);
        if (libraryRoot is null)
        {
            return null;
        }

        string? relativePath = await _db.BookFiles
            .AsNoTracking()
            .Where(f => f.BookId == bookId)
            .OrderBy(f => f.RelativePath)
            .Select(f => f.RelativePath)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (relativePath is null)
        {
            return null;
        }

        string fullPath = Path.Combine(libraryRoot, relativePath);
        return File.Exists(fullPath) ? fullPath : null;
    }
}
