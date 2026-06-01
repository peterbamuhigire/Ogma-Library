using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Infrastructure.Catalogue;

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
        _libraryRoot = Path.GetFullPath(libraryRoot);
    }

    /// <inheritdoc />
    public async Task<string?> ResolveAsync(string bookId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        using CatalogueDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        string? relativePath = await context.BookFiles
            .AsNoTracking()
            .Where(file => file.BookId == bookId && file.FileStatus == 0)
            .OrderBy(file => file.RelativePath)
            .Select(file => file.RelativePath)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        string platformPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(platformPath))
        {
            return null;
        }

        string fullPath = Path.GetFullPath(Path.Combine(_libraryRoot, platformPath));
        if (!IsWithinRoot(fullPath, _libraryRoot) || !File.Exists(fullPath))
        {
            return null;
        }

        return fullPath;
    }

    private static bool IsWithinRoot(string fullPath, string root)
    {
        string normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        string normalizedPath = Path.GetFullPath(fullPath);
        return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }
}
