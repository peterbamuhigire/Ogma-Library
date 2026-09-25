using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Registers one user-selected PDF and returns the book id for immediate reader
/// navigation. This is intentionally narrow: it does not enumerate sibling files.
/// </summary>
public sealed class DirectPdfOpenService : IDirectPdfOpenService
{
    private readonly ILibrarySettingsService _settings;
    private readonly IBookIdentityService _identity;
    private readonly IBookRegistrationService _registration;
    private readonly CatalogueMigrator? _migrator;
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;
    private readonly Pdf.PdfFileValidityClassifier _validity = new();

    /// <summary>Initializes a new instance of <see cref="DirectPdfOpenService"/>.</summary>
    /// <param name="settings">The library settings service.</param>
    /// <param name="identity">The book identity service.</param>
    /// <param name="registration">The book registration service.</param>
    /// <param name="migrator">Optional schema migrator used to repair startup-damaged catalogues.</param>
    /// <param name="contextFactory">Optional context factory used for exact selected-path checks.</param>
    /// <param name="context">Optional context used by tests for exact selected-path checks.</param>
    public DirectPdfOpenService(
        ILibrarySettingsService settings,
        IBookIdentityService identity,
        IBookRegistrationService registration,
        CatalogueMigrator? migrator = null,
        IDbContextFactory<CatalogueDbContext>? contextFactory = null,
        CatalogueDbContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(registration);

        _settings = settings;
        _identity = identity;
        _registration = registration;
        _migrator = migrator;
        _contextFactory = contextFactory;
        _context = context;
    }

    /// <inheritdoc />
    public async Task<string> OpenAsync(
        string absoluteFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absoluteFilePath);

        string fullPath = Path.GetFullPath(absoluteFilePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The selected PDF file does not exist.", fullPath);
        }

        if (!string.Equals(Path.GetExtension(fullPath), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The selected file is not a PDF document.");
        }

        FileValidity validity = await _validity.ClassifyAsync(fullPath, cancellationToken).ConfigureAwait(false);
        if (validity is FileValidity.Empty or FileValidity.NotAPdf)
        {
            throw new InvalidPdfFileException(validity);
        }

        if (_migrator is not null)
        {
            await _migrator.ApplyAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            return await RegisterOrUpdateAsync(fullPath, validity, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (_migrator is not null && IsMissingSqliteTable(ex))
        {
            await _migrator.ApplyAsync(cancellationToken).ConfigureAwait(false);
            return await RegisterOrUpdateAsync(fullPath, validity, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<string> RegisterOrUpdateAsync(
        string fullPath,
        FileValidity validity,
        CancellationToken cancellationToken)
    {
        string? legacyRoot = await _settings.GetLibraryRootAsync(cancellationToken)
            .ConfigureAwait(false);

        // Sept-23 Phase 05 (K28): never change library roots here. A file inside an
        // enabled root is recorded against that root; anything else is a loose book.
        string? rootId = null;
        string identityRoot = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("The selected PDF has no containing folder.");
        string relativePath = NormalizeStoredPath(fullPath);
        if (_contextFactory is not null || _context is not null)
        {
            using ContextLease rootLease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
            await LibraryRootPaths.BackfillLegacyRootAsync(rootLease.Context, legacyRoot, cancellationToken)
                .ConfigureAwait(false);
            IReadOnlyList<LibraryRootLocation> roots = await LibraryRootPaths
                .GetActiveRootsAsync(rootLease.Context, cancellationToken)
                .ConfigureAwait(false);
            LibraryRootLocation? root = LibraryRootPaths.FindContainingRoot(roots, fullPath);
            if (root is not null)
            {
                rootId = root.RootId;
                identityRoot = root.CanonicalPath;
                relativePath = LibraryRootPaths.ToRelativePath(root.CanonicalPath, fullPath);
            }
        }
        else if (!string.IsNullOrWhiteSpace(legacyRoot) && IsUnderRoot(fullPath, legacyRoot))
        {
            identityRoot = Path.GetFullPath(legacyRoot);
            relativePath = NormalizeStoredPath(Path.GetRelativePath(identityRoot, fullPath));
        }

        var info = new FileInfo(fullPath);
        var discovered = new DiscoveredFile(
            AbsolutePath: fullPath,
            RelativePath: relativePath,
            SizeBytes: info.Length,
            MtimeTicks: info.LastWriteTimeUtc.Ticks,
            LibraryRootId: rootId,
            Validity: validity == FileValidity.Locked ? FileValidity.Locked : FileValidity.Valid);

        string contentHash = await ComputeSha256Async(fullPath, cancellationToken)
            .ConfigureAwait(false);

        string? registeredBookId = await FindRegisteredBookIdByPathAsync(rootId, relativePath, cancellationToken)
            .ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(registeredBookId))
        {
            return await UpdateExistingAsync(registeredBookId, discovered, contentHash, cancellationToken)
                .ConfigureAwait(false);
        }

        if (_contextFactory is not null || _context is not null)
        {
            // The same bytes opened from another place are another occurrence of
            // the same book, not a second book (T05.11).
            string? sameContentBookId = await AddOccurrenceToExistingContentAsync(
                discovered, contentHash, cancellationToken).ConfigureAwait(false);
            if (sameContentBookId is not null)
            {
                return sameContentBookId;
            }

            return await _registration
                .RegisterAsync(discovered, contentHash, cancellationToken)
                .ConfigureAwait(false);
        }

        BookMatchResult match = await _identity
            .ResolveAsync(fullPath, identityRoot, cancellationToken)
            .ConfigureAwait(false);

        return match switch
        {
            BookMatchResult.NewBook => await _registration
                .RegisterAsync(discovered, contentHash, cancellationToken)
                .ConfigureAwait(false),

            BookMatchResult.ExactMatch exact => await UpdateExistingAsync(
                exact.BookId, discovered, contentHash, cancellationToken).ConfigureAwait(false),

            BookMatchResult.FuzzyMatch => await _registration
                .RegisterAsync(discovered, contentHash, cancellationToken)
                .ConfigureAwait(false),

            BookMatchResult.Unresolvable unresolved => throw new InvalidOperationException(
                $"Cannot open selected PDF: {unresolved.Reason}"),

            _ => throw new InvalidOperationException("Cannot open selected PDF: unknown identity result."),
        };
    }

    private async Task<string> UpdateExistingAsync(
        string bookId,
        DiscoveredFile discovered,
        string contentHash,
        CancellationToken cancellationToken)
    {
        await _registration
            .UpdateFilePathAsync(bookId, discovered, contentHash, cancellationToken)
            .ConfigureAwait(false);

        return bookId;
    }

    private async Task<string?> FindRegisteredBookIdByPathAsync(
        string? rootId,
        string relativePath,
        CancellationToken cancellationToken)
    {
        if (_contextFactory is null && _context is null)
        {
            return null;
        }

        using ContextLease lease = await CreateLeaseAsync(cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        return await context.BookFiles
            .AsNoTracking()
            .Where(f => f.RelativePath == relativePath && f.LibraryRootId == rootId)
            .OrderBy(f => f.FileStatus)
            .Select(f => f.BookId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<string?> AddOccurrenceToExistingContentAsync(
        DiscoveredFile discovered,
        string contentHash,
        CancellationToken cancellationToken)
    {
        using ContextLease lease = await CreateLeaseAsync(cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        BookRow? book = await context.Books
            .Where(b => b.Sha256Hash == contentHash)
            .OrderBy(b => b.BookId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (book is null)
        {
            return null;
        }

        context.BookFiles.Add(new BookFileRow
        {
            BookId = book.BookId,
            LibraryRootId = discovered.LibraryRootId,
            RelativePath = discovered.RelativePath,
            FileStatus = 0,
            FileValidity = (int)discovered.Validity,
            LastSeenUtc = DateTimeOffset.UtcNow,
        });
        if (book.Status == 1)
        {
            book.Status = 0;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return book.BookId;
    }

    private static bool IsMissingSqliteTable(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqliteException sqlite &&
                sqlite.SqliteErrorCode == 1 &&
                sqlite.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<string> ComputeSha256Async(
        string filePath,
        CancellationToken cancellationToken)
    {
        var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        await using (stream.ConfigureAwait(false))
        {
            byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken)
                .ConfigureAwait(false);
            return Convert.ToHexStringLower(hash);
        }
    }

    private static bool IsUnderRoot(string absoluteFilePath, string rootPath)
    {
        string normalizedFile = Path.GetFullPath(absoluteFilePath);
        string normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath))
            + Path.DirectorySeparatorChar;

        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return normalizedFile.StartsWith(normalizedRoot, comparison);
    }

    private static string NormalizeStoredPath(string path) =>
        path.Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');

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
