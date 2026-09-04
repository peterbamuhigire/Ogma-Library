using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Pathing;

namespace OgmaLibrary.Infrastructure.Assets;

/// <summary>
/// Persists and resolves visual asset manifests shared by catalogue and 3D
/// consumers. Files remain in the sidecar; this service owns only safe portable
/// references and their lifecycle metadata.
/// </summary>
public sealed class VisualAssetService : IVisualAssetService
{
    private const string GeneratedSource = "generated";
    private const string CustomSource = "custom";
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;
    private readonly string? _libraryRoot;

    /// <summary>Creates a service backed by a factory for application use.</summary>
    [ActivatorUtilitiesConstructor]
    public VisualAssetService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        string? libraryRoot = null)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
        _libraryRoot = NormalizeLibraryRoot(libraryRoot);
    }

    /// <summary>Creates a service backed by an explicit context for tests and migrations.</summary>
    public VisualAssetService(CatalogueDbContext context, string? libraryRoot = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _libraryRoot = NormalizeLibraryRoot(libraryRoot);
    }

    /// <inheritdoc />
    public async Task<VisualAssetDescriptor?> GetPreferredAsync(
        string bookId,
        VisualAssetKind kind,
        CancellationToken cancellationToken = default)
    {
        ValidateBookId(bookId);
        ValidateKind(kind);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        List<VisualAssetManifestRow> rows = await lease.Context.VisualAssetManifests
            .AsNoTracking()
            .Where(asset => asset.BookId == bookId &&
                            asset.Kind == (int)kind &&
                            asset.Status == (int)VisualAssetStatus.Ready)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .OrderByDescending(asset => asset.IsCustom)
            .ThenByDescending(asset => SourcePriority(asset.Source))
            .ThenByDescending(asset => asset.UpdatedUtc)
            .ThenBy(asset => asset.Variant, StringComparer.Ordinal)
            .Select(ToDescriptor)
            .FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<VisualAssetDescriptor?> GetVariantAsync(
        string bookId,
        VisualAssetKind kind,
        string variant,
        CancellationToken cancellationToken = default)
    {
        ValidateBookId(bookId);
        ValidateKind(kind);
        string normalizedVariant = ValidateVariant(variant);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        VisualAssetManifestRow? row = await lease.Context.VisualAssetManifests
            .AsNoTracking()
            .SingleOrDefaultAsync(asset => asset.BookId == bookId &&
                                           asset.Kind == (int)kind &&
                                           asset.Variant == normalizedVariant &&
                                           asset.Status == (int)VisualAssetStatus.Ready,
                cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : ToDescriptor(row);
    }

    /// <inheritdoc />
    public async Task<VisualAssetDescriptor> RegisterGeneratedAsync(
        string bookId,
        string? sourceContentHash,
        VisualAssetKind kind,
        string variant,
        string relativePath,
        int widthPx,
        int heightPx,
        string format,
        int generationVersion,
        CancellationToken cancellationToken = default)
    {
        ValidateBookId(bookId);
        ValidateKind(kind);
        string normalizedVariant = ValidateVariant(variant);
        string normalizedPath = ValidateRelativePath(relativePath);
        ValidateDimensions(widthPx, heightPx);
        string normalizedFormat = ValidateFormat(format);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(generationVersion);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        VisualAssetManifestRow? row = await context.VisualAssetManifests
            .SingleOrDefaultAsync(asset => asset.BookId == bookId &&
                                           asset.Kind == (int)kind &&
                                           asset.Variant == normalizedVariant,
                cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (row is null)
        {
            row = new VisualAssetManifestRow
            {
                BookId = bookId,
                Kind = (int)kind,
                Variant = normalizedVariant,
                CreatedUtc = now,
            };
            context.VisualAssetManifests.Add(row);
        }

        // A custom cover has a separate protected variant, but retain this guard
        // for databases created by early previews that used the default key.
        if (row.IsCustom && kind == VisualAssetKind.Cover)
        {
            return ToDescriptor(row);
        }

        row.RelativePath = normalizedPath;
        row.Source = GeneratedSource;
        row.SourceContentHash = NormalizeHash(sourceContentHash);
        row.WidthPx = widthPx;
        row.HeightPx = heightPx;
        row.Format = normalizedFormat;
        row.GenerationVersion = generationVersion;
        row.Status = (int)VisualAssetStatus.Ready;
        row.IsCustom = false;
        row.UpdatedUtc = now;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDescriptor(row);
    }

    /// <inheritdoc />
    public async Task<VisualAssetDescriptor> RegisterCustomCoverAsync(
        string bookId,
        string relativePath,
        int widthPx,
        int heightPx,
        string format,
        CancellationToken cancellationToken = default)
    {
        ValidateBookId(bookId);
        string normalizedPath = ValidateRelativePath(relativePath);
        ValidateDimensions(widthPx, heightPx);
        string normalizedFormat = ValidateFormat(format);

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        const string variant = "custom";
        VisualAssetManifestRow? row = await context.VisualAssetManifests
            .SingleOrDefaultAsync(asset => asset.BookId == bookId &&
                                           asset.Kind == (int)VisualAssetKind.Cover &&
                                           asset.Variant == variant,
                cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (row is null)
        {
            row = new VisualAssetManifestRow
            {
                BookId = bookId,
                Kind = (int)VisualAssetKind.Cover,
                Variant = variant,
                CreatedUtc = now,
            };
            context.VisualAssetManifests.Add(row);
        }

        row.RelativePath = normalizedPath;
        row.Source = CustomSource;
        row.SourceContentHash = null;
        row.WidthPx = widthPx;
        row.HeightPx = heightPx;
        row.Format = normalizedFormat;
        row.GenerationVersion = 1;
        row.Status = (int)VisualAssetStatus.Ready;
        row.IsCustom = true;
        row.UpdatedUtc = now;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDescriptor(row);
    }

    /// <inheritdoc />
    public async Task<int> InvalidateGeneratedAsync(
        string bookId,
        string? currentSourceContentHash,
        CancellationToken cancellationToken = default)
    {
        ValidateBookId(bookId);
        string? normalizedHash = NormalizeHash(currentSourceContentHash);
        if (normalizedHash is null)
        {
            return 0;
        }

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        List<VisualAssetManifestRow> rows = await lease.Context.VisualAssetManifests
            .Where(asset => asset.BookId == bookId &&
                            !asset.IsCustom &&
                            asset.Status == (int)VisualAssetStatus.Ready &&
                            asset.SourceContentHash != null &&
                            asset.SourceContentHash != normalizedHash)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (VisualAssetManifestRow row in rows)
        {
            row.Status = (int)VisualAssetStatus.Stale;
            row.UpdatedUtc = DateTimeOffset.UtcNow;
        }

        if (rows.Count > 0)
        {
            await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return rows.Count;
    }

    /// <inheritdoc />
    public async Task<VisualAssetGarbageCollectionResult> CollectStaleAsync(
        string? bookId = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(bookId))
        {
            ValidateBookId(bookId);
        }

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        List<VisualAssetManifestRow> stale = await context.VisualAssetManifests
            .Where(asset => !asset.IsCustom &&
                            asset.Status == (int)VisualAssetStatus.Stale &&
                            (bookId == null || asset.BookId == bookId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (stale.Count == 0)
        {
            return new VisualAssetGarbageCollectionResult(0, 0, 0);
        }

        HashSet<string> stalePaths = stale
            .Select(asset => asset.RelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<string> referencedPathRows = await context.VisualAssetManifests
            .Where(asset => !stalePaths.Contains(asset.RelativePath) ||
                            asset.Status != (int)VisualAssetStatus.Stale)
            .Select(asset => asset.RelativePath)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        HashSet<string> referencedPaths = referencedPathRows
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<VisualAssetManifestRow> rowsToRemove = new();

        int deletedFiles = 0;
        int retainedReferencedFiles = 0;
        foreach (string relativePath in stalePaths)
        {
            List<VisualAssetManifestRow> pathRows = stale
                .Where(asset => string.Equals(asset.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (referencedPaths.Contains(relativePath))
            {
                rowsToRemove.AddRange(pathRows);
                retainedReferencedFiles++;
                continue;
            }

            if (_libraryRoot is null)
            {
                rowsToRemove.AddRange(pathRows);
                continue;
            }

            string absolutePath = PathGuard.EnsureWithinRoot(
                Path.Combine(_libraryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)),
                _libraryRoot);
            if (!absolutePath.StartsWith(
                    Path.Combine(_libraryRoot, ".ogma") + Path.DirectorySeparatorChar,
                    OperatingSystem.IsWindows()
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal))
            {
                continue;
            }

            if (!File.Exists(absolutePath))
            {
                rowsToRemove.AddRange(pathRows);
                continue;
            }

            try
            {
                File.Delete(absolutePath);
                rowsToRemove.AddRange(pathRows);
                deletedFiles++;
            }
            catch (IOException)
            {
                // A locked asset remains represented for a later collection pass.
            }
            catch (UnauthorizedAccessException)
            {
                // Permission failures are non-fatal; retain the manifest for retry.
            }
        }

        if (rowsToRemove.Count > 0)
        {
            context.VisualAssetManifests.RemoveRange(rowsToRemove);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return new VisualAssetGarbageCollectionResult(
            rowsToRemove.Count,
            deletedFiles,
            retainedReferencedFiles);
    }

    private static VisualAssetDescriptor ToDescriptor(VisualAssetManifestRow row) =>
        new(
            row.BookId,
            (VisualAssetKind)row.Kind,
            row.Variant,
            row.RelativePath,
            row.Source,
            row.SourceContentHash,
            row.WidthPx,
            row.HeightPx,
            row.Format,
            row.GenerationVersion,
            (VisualAssetStatus)row.Status,
            row.IsCustom,
            row.UpdatedUtc);

    private static int SourcePriority(string source) => source.ToLowerInvariant() switch
    {
        CustomSource => 100,
        "embedded" => 80,
        "provider" => 70,
        GeneratedSource => 50,
        "placeholder" => 10,
        _ => 0,
    };

    private static void ValidateBookId(string bookId) =>
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

    private static void ValidateKind(VisualAssetKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown visual asset kind.");
        }
    }

    private static string ValidateVariant(string variant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(variant);
        string normalized = variant.Trim();
        if (normalized.Length > 64 || normalized.Any(character =>
                !char.IsLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new ArgumentException("Variant must be an alphanumeric name with '-' or '_'.", nameof(variant));
        }

        return normalized;
    }

    private static string ValidateRelativePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        string normalized = relativePath.Trim().Replace('\\', '/');
        if (Path.IsPathRooted(normalized) ||
            normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or "..") ||
            !normalized.StartsWith(".ogma/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Visual assets must use a safe .ogma-relative path.", nameof(relativePath));
        }

        return normalized;
    }

    private static void ValidateDimensions(int widthPx, int heightPx)
    {
        if (widthPx is <= 0 or > 4096 || heightPx is <= 0 or > 4096)
        {
            throw new ArgumentOutOfRangeException(nameof(widthPx), "Visual asset dimensions must be between 1 and 4096 pixels.");
        }
    }

    private static string ValidateFormat(string format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        string normalized = format.Trim().ToLowerInvariant();
        if (normalized is not ("jpg" or "jpeg" or "png" or "webp"))
        {
            throw new ArgumentException("Only approved image formats are supported.", nameof(format));
        }

        return normalized;
    }

    private static string? NormalizeHash(string? hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            return null;
        }

        string normalized = hash.Trim().ToLowerInvariant();
        return normalized.Length == 64 && normalized.All(Uri.IsHexDigit)
            ? normalized
            : throw new ArgumentException("Source content hash must be a SHA-256 hexadecimal value.", nameof(hash));
    }

    private static string? NormalizeLibraryRoot(string? libraryRoot) =>
        string.IsNullOrWhiteSpace(libraryRoot) ? null : Path.GetFullPath(libraryRoot);

    private async ValueTask<ContextLease> CreateLeaseAsync(CancellationToken cancellationToken)
    {
        if (_contextFactory is null)
        {
            return new ContextLease(_context!, ownsContext: false);
        }

        CatalogueDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        return new ContextLease(context, ownsContext: true);
    }

    private readonly struct ContextLease : IDisposable
    {
        public ContextLease(CatalogueDbContext context, bool ownsContext)
        {
            Context = context;
            _ownsContext = ownsContext;
        }

        private readonly bool _ownsContext;
        public CatalogueDbContext Context { get; }

        public void Dispose()
        {
            if (_ownsContext)
            {
                Context.Dispose();
            }
        }
    }
}
