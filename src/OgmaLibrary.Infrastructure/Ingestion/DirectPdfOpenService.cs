using System.Security.Cryptography;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Catalogue;

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

    /// <summary>Initializes a new instance of <see cref="DirectPdfOpenService"/>.</summary>
    /// <param name="settings">The library settings service.</param>
    /// <param name="identity">The book identity service.</param>
    /// <param name="registration">The book registration service.</param>
    /// <param name="migrator">Optional schema migrator used to repair startup-damaged catalogues.</param>
    public DirectPdfOpenService(
        ILibrarySettingsService settings,
        IBookIdentityService identity,
        IBookRegistrationService registration,
        CatalogueMigrator? migrator = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(registration);

        _settings = settings;
        _identity = identity;
        _registration = registration;
        _migrator = migrator;
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

        if (_migrator is not null)
        {
            await _migrator.ApplyAsync(cancellationToken).ConfigureAwait(false);
        }

        string containingFolder = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("The selected PDF has no containing folder.");

        string? existingRoot = await _settings.GetLibraryRootAsync(cancellationToken)
            .ConfigureAwait(false);

        string identityRoot;
        string relativePath;
        if (string.IsNullOrWhiteSpace(existingRoot))
        {
            identityRoot = containingFolder;
            relativePath = Path.GetFileName(fullPath);
            await _settings.SetLibraryRootAsync(identityRoot, cancellationToken)
                .ConfigureAwait(false);
        }
        else if (IsUnderRoot(fullPath, existingRoot))
        {
            identityRoot = Path.GetFullPath(existingRoot);
            relativePath = NormalizeStoredPath(Path.GetRelativePath(identityRoot, fullPath));
        }
        else
        {
            identityRoot = Path.GetFullPath(existingRoot);
            relativePath = NormalizeStoredPath(fullPath);
        }

        var info = new FileInfo(fullPath);
        var discovered = new DiscoveredFile(
            AbsolutePath: fullPath,
            RelativePath: relativePath,
            SizeBytes: info.Length,
            MtimeTicks: info.LastWriteTimeUtc.Ticks);

        string contentHash = await ComputeSha256Async(fullPath, cancellationToken)
            .ConfigureAwait(false);

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

            BookMatchResult.FuzzyMatch fuzzy => await UpdateExistingAsync(
                fuzzy.BookId, discovered, contentHash, cancellationToken).ConfigureAwait(false),

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
}
