using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Pathing;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>One configured library root with a usable absolute locator.</summary>
/// <param name="RootId">The stable root identity.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="CanonicalPath">The canonical absolute folder path.</param>
/// <param name="IsActive">Whether the root is enabled and not removed.</param>
public sealed record LibraryRootLocation(
    string RootId,
    string DisplayName,
    string CanonicalPath,
    bool IsActive);

/// <summary>
/// Per-root path resolution for <c>BookFiles</c> (Sept-23 Phase 05, K22). Every
/// stored file resolves through its own root; a null root id means a loose file
/// stored by absolute path, or a legacy row that still resolves through the
/// single-root settings path until the backfill runs.
/// </summary>
public static class LibraryRootPaths
{
    /// <summary>Loads every root that has a locator, keyed by root id.</summary>
    public static async Task<IReadOnlyDictionary<string, LibraryRootLocation>> LoadAsync(
        CatalogueDbContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rows = await context.LibraryRoots
            .AsNoTracking()
            .Where(row => row.CanonicalLocator != null)
            .Select(row => new
            {
                row.LibraryRootId,
                row.DisplayName,
                row.CanonicalLocator,
                row.IsEnabled,
                row.RemovedUtc,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(
            row => row.LibraryRootId,
            row => new LibraryRootLocation(
                row.LibraryRootId,
                row.DisplayName,
                row.CanonicalLocator!,
                row.IsEnabled && row.RemovedUtc == null),
            StringComparer.Ordinal);
    }

    /// <summary>Returns the enabled, non-removed roots in display order.</summary>
    public static async Task<IReadOnlyList<LibraryRootLocation>> GetActiveRootsAsync(
        CatalogueDbContext context,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, LibraryRootLocation> roots = await LoadAsync(context, cancellationToken)
            .ConfigureAwait(false);
        return roots.Values
            .Where(root => root.IsActive)
            .OrderBy(root => root.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(root => root.RootId, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Returns the ids of enabled, non-removed roots.</summary>
    public static async Task<HashSet<string>> GetActiveRootIdsAsync(
        CatalogueDbContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        List<string> ids = await context.LibraryRoots
            .AsNoTracking()
            .Where(row => row.IsEnabled && row.RemovedUtc == null && row.CanonicalLocator != null)
            .Select(row => row.LibraryRootId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return ids.ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Resolves a stored file to an absolute path, bounded to its root. Returns null
    /// when the root is unknown, the path escapes the root, or the input is unsafe.
    /// </summary>
    /// <param name="roots">The configured roots.</param>
    /// <param name="libraryRootId">The file's root id, or null.</param>
    /// <param name="storedPath">The stored root-relative (or loose absolute) path.</param>
    /// <param name="legacyRoot">The legacy single-root settings path used for un-backfilled rows.</param>
    public static string? Resolve(
        IReadOnlyDictionary<string, LibraryRootLocation> roots,
        string? libraryRootId,
        string storedPath,
        string? legacyRoot)
    {
        ArgumentNullException.ThrowIfNull(roots);
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return null;
        }

        string nativePath = storedPath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        try
        {
            if (Path.IsPathFullyQualified(nativePath))
            {
                // A loose (directly opened) file is stored by its exact absolute path.
                return Path.GetFullPath(nativePath);
            }

            string? root = null;
            if (!string.IsNullOrWhiteSpace(libraryRootId))
            {
                root = roots.TryGetValue(libraryRootId, out LibraryRootLocation? location)
                    ? location.CanonicalPath
                    : null;
            }
            else
            {
                root = legacyRoot;
            }

            if (string.IsNullOrWhiteSpace(root) || Path.IsPathRooted(nativePath))
            {
                return null;
            }

            string canonicalRoot = PathGuard.CanonicalizeRoot(root);
            return PathGuard.EnsureWithinRoot(Path.Combine(canonicalRoot, nativePath), canonicalRoot);
        }
        catch (PathTraversalException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Resolves one stored file row.</summary>
    public static string? Resolve(
        IReadOnlyDictionary<string, LibraryRootLocation> roots,
        BookFileRow file,
        string? legacyRoot)
    {
        ArgumentNullException.ThrowIfNull(file);
        return Resolve(roots, file.LibraryRootId, file.RelativePath, legacyRoot);
    }

    /// <summary>
    /// Finds the active root that contains <paramref name="absolutePath"/>, preferring
    /// the deepest (most specific) root when roots nest.
    /// </summary>
    public static LibraryRootLocation? FindContainingRoot(
        IEnumerable<LibraryRootLocation> roots,
        string absolutePath)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);
        string full = Path.GetFullPath(absolutePath);
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return roots
            .Where(root => root.IsActive)
            .Where(root => full.StartsWith(
                Path.TrimEndingDirectorySeparator(root.CanonicalPath) + Path.DirectorySeparatorChar,
                comparison))
            .OrderByDescending(root => root.CanonicalPath.Length)
            .FirstOrDefault();
    }

    /// <summary>Converts an absolute path under <paramref name="rootPath"/> to a forward-slash relative path.</summary>
    public static string ToRelativePath(string rootPath, string absolutePath) =>
        Path.GetRelativePath(rootPath, absolutePath)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');

    /// <summary>
    /// Creates (or returns) the root row for a legacy single-root settings path and
    /// assigns it to every relative <c>BookFiles</c> row that has no root yet, in one
    /// transaction (Sept-23 Phase 05, T05.2 backfill). Loose absolute rows keep a null root.
    /// </summary>
    /// <returns>The legacy root id, or null when no legacy path is configured.</returns>
    public static async Task<string?> BackfillLegacyRootAsync(
        CatalogueDbContext context,
        string? legacyRoot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(legacyRoot))
        {
            return null;
        }

        string canonical;
        try
        {
            canonical = PathGuard.CanonicalizeRoot(legacyRoot);
        }
        catch (ArgumentException)
        {
            return null;
        }

        LibraryRootRow? row = await context.LibraryRoots
            .FirstOrDefaultAsync(candidate => candidate.CanonicalLocator == canonical, cancellationToken)
            .ConfigureAwait(false);
        bool pending = await context.BookFiles
            .AnyAsync(file => file.LibraryRootId == null, cancellationToken)
            .ConfigureAwait(false);
        if (row is not null && !pending)
        {
            // Already migrated: the legacy folder is a root and every row has one.
            return row.LibraryRootId;
        }

        var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            row = await BackfillInTransactionAsync(context, row, canonical, pending, cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return row.LibraryRootId;
    }

    private static async Task<LibraryRootRow> BackfillInTransactionAsync(
        CatalogueDbContext context,
        LibraryRootRow? row,
        string canonical,
        bool pending,
        CancellationToken cancellationToken)
    {
        if (row is null)
        {
            string name = new DirectoryInfo(canonical).Name;
            row = new LibraryRootRow
            {
                LibraryRootId = CanonicalIdGenerator.NewId(),
                DisplayName = string.IsNullOrWhiteSpace(name) ? canonical : name,
                CanonicalLocator = canonical,
                VolumeIdentity = Path.GetPathRoot(canonical)?.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
                RootStatus = Directory.Exists(canonical) ? 0 : 1,
                PermissionStatus = 0,
                IsEnabled = true,
                CreatedUtc = DateTimeOffset.UtcNow,
                LastHealthCheckUtc = DateTimeOffset.UtcNow,
            };
            context.LibraryRoots.Add(row);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (pending)
        {
            List<BookFileRow> legacyRows = await context.BookFiles
                .Where(file => file.LibraryRootId == null)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            int assigned = 0;
            foreach (BookFileRow file in legacyRows)
            {
                string native = file.RelativePath.Replace('/', Path.DirectorySeparatorChar);
                if (Path.IsPathFullyQualified(native))
                {
                    // Directly opened files stay loose unless they live under the root.
                    string full = Path.GetFullPath(native);
                    if (!IsUnder(full, canonical))
                    {
                        continue;
                    }

                    file.RelativePath = ToRelativePath(canonical, full);
                }

                file.LibraryRootId = row.LibraryRootId;
                assigned++;
            }

            if (assigned > 0)
            {
                context.AuditEvents.Add(new AuditEventRow
                {
                    EventType = "LibraryRootBackfilled",
                    EntityId = row.LibraryRootId,
                    EntityType = "LibraryRoot",
                    AfterJson = $"{{\"files\":{assigned}}}",
                    Timestamp = DateTimeOffset.UtcNow,
                    IsLocalOnly = true,
                });
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return row;
    }

    private static bool IsUnder(string fullPath, string canonicalRoot)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return fullPath.StartsWith(
            Path.TrimEndingDirectorySeparator(canonicalRoot) + Path.DirectorySeparatorChar,
            comparison);
    }
}
