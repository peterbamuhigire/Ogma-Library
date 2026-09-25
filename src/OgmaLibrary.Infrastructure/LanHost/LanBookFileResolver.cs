using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Ingestion;
using OgmaLibrary.Infrastructure.Pathing;

namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>Catalogue-backed resolver for FileStream-mode PDF paths.</summary>
internal sealed class LanBookFileResolver : ILanBookFileResolver
{
    private readonly IDbContextFactory<CatalogueDbContext> _contextFactory;
    private readonly string _libraryRoot;

    public LanBookFileResolver(IDbContextFactory<CatalogueDbContext> contextFactory, string libraryRoot)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);
        _libraryRoot = PathGuard.CanonicalizeRoot(libraryRoot);
    }

    /// <inheritdoc />
    public async Task<string?> ResolveAsync(string bookId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        using CatalogueDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        List<Catalogue.Entities.BookFileRow> files = await context.BookFiles
            .AsNoTracking()
            .Where(file => file.BookId == bookId && file.FileStatus == 0)
            .OrderBy(file => file.RelativePath)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (files.Count == 0)
        {
            return null;
        }

        IReadOnlyDictionary<string, LibraryRootLocation> roots = await LibraryRootPaths
            .LoadAsync(context, cancellationToken)
            .ConfigureAwait(false);
        foreach (Catalogue.Entities.BookFileRow file in files)
        {
            // LAN streaming serves only files inside a configured library root:
            // loose (absolute) files are never exposed to classroom clients.
            if (Path.IsPathRooted(file.RelativePath.Replace('/', Path.DirectorySeparatorChar)))
            {
                continue;
            }

            string? fullPath = LibraryRootPaths.Resolve(roots, file, _libraryRoot);
            if (fullPath is not null && File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }
}
