using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue;

/// <summary>Safe preflight counts that contain no paths or bibliographic text.</summary>
public sealed record IdentityMigrationPreflightReport(
    int LegacyBookCount,
    int LegacyFileCount,
    int ExistingAliasCount,
    int InvalidHashCount,
    int DuplicateLocatorCount,
    int ConflictingIsbnCount,
    int ConflictingDoiCount);

/// <summary>Redacted progress update for migration UI and diagnostics.</summary>
public sealed record CatalogueMigrationProgress(
    string Stage,
    int CompletedItems,
    int TotalItems,
    int ConflictCount);

/// <summary>Counts produced by an idempotent canonical identity backfill.</summary>
public sealed record CanonicalIdentityMigrationResult(
    int MigratedBooks,
    int CreatedOccurrences,
    int CreatedAssets,
    int ConflictCount,
    TimeSpan Duration);

internal sealed class CanonicalIdentityMigrationService
{
    internal const string CompatibilityRootId = "00000000000000000000000001";
    internal const int MigrationVersion = 1;
    private const int BatchSize = 500;

    private readonly CatalogueDbContext _context;

    public CanonicalIdentityMigrationService(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<bool> IsBackfillRequiredAsync(CancellationToken cancellationToken)
    {
        int books = await _context.Books.CountAsync(cancellationToken).ConfigureAwait(false);
        if (books == 0)
        {
            return false;
        }

        int aliases = await _context.LegacyIdentityAliases.CountAsync(cancellationToken).ConfigureAwait(false);
        if (books != aliases)
        {
            return true;
        }

        int catalogueItems = await _context.CatalogueItems.CountAsync(cancellationToken).ConfigureAwait(false);
        int works = await _context.CanonicalWorks.CountAsync(cancellationToken).ConfigureAwait(false);
        int editions = await _context.CanonicalEditions.CountAsync(cancellationToken).ConfigureAwait(false);
        return aliases != catalogueItems || aliases != works || aliases != editions;
    }

    public async Task<IdentityMigrationPreflightReport> PreflightAsync(
        CancellationToken cancellationToken)
    {
        int legacyBookCount = await _context.Books.CountAsync(cancellationToken).ConfigureAwait(false);
        int legacyFileCount = await _context.BookFiles.CountAsync(cancellationToken).ConfigureAwait(false);
        int existingAliasCount = await _context.LegacyIdentityAliases.CountAsync(cancellationToken).ConfigureAwait(false);
        List<string?> hashes = await _context.Books.AsNoTracking()
            .Where(book => book.Sha256Hash != null)
            .Select(book => book.Sha256Hash)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        int invalidHashCount = hashes.Count(hash => !TryNormalizeSha256(hash, out _));

        List<string> relativePaths = await _context.BookFiles.AsNoTracking()
            .Select(file => file.RelativePath)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        int duplicateLocatorCount = relativePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .GroupBy(NormalizeRelativePath, StringComparer.Ordinal)
            .Sum(group => Math.Max(0, group.Count() - 1));

        int conflictingIsbnCount = await CountDuplicateValuesAsync(
            _context.Books.Where(book => book.IsbnNormalized != null).Select(book => book.IsbnNormalized!),
            cancellationToken).ConfigureAwait(false);
        int conflictingDoiCount = await CountDuplicateValuesAsync(
            _context.Books.Where(book => book.Doi != null).Select(book => book.Doi!),
            cancellationToken).ConfigureAwait(false);

        return new IdentityMigrationPreflightReport(
            legacyBookCount,
            legacyFileCount,
            existingAliasCount,
            invalidHashCount,
            duplicateLocatorCount,
            conflictingIsbnCount,
            conflictingDoiCount);
    }

    public async Task<CanonicalIdentityMigrationResult> ApplyAsync(
        IProgress<CatalogueMigrationProgress>? progress,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        await EnsureCompatibilityRootAsync(cancellationToken).ConfigureAwait(false);

        int total = await _context.Books.CountAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> assetsByHash = await _context.ContentAssets.AsNoTracking()
            .Where(asset => asset.FingerprintVersion == 1)
            .ToDictionaryAsync(
                asset => asset.Sha256Hash,
                asset => asset.ContentAssetId,
                StringComparer.Ordinal,
                cancellationToken)
            .ConfigureAwait(false);
        Dictionary<string, string> occurrencesByLocator = await _context.FileOccurrences.AsNoTracking()
            .Where(occurrence => occurrence.LibraryRootId == CompatibilityRootId)
            .ToDictionaryAsync(
                occurrence => occurrence.NormalizedRelativePath,
                occurrence => occurrence.FileOccurrenceId,
                StringComparer.Ordinal,
                cancellationToken)
            .ConfigureAwait(false);

        HashSet<string> conflictingIsbns = await ReadConflictingValuesAsync(
            _context.Books.Where(book => book.IsbnNormalized != null).Select(book => book.IsbnNormalized!),
            cancellationToken).ConfigureAwait(false);
        HashSet<string> conflictingDois = await ReadConflictingValuesAsync(
            _context.Books.Where(book => book.Doi != null).Select(book => book.Doi!),
            cancellationToken).ConfigureAwait(false);

        int migrated = 0;
        int createdOccurrences = 0;
        int createdAssets = 0;
        int conflicts = conflictingIsbns.Count + conflictingDois.Count;
        string? lastBookId = null;

        while (true)
        {
            List<BookRow> books = await _context.Books
                .Include(book => book.BookFiles)
                .Where(book => lastBookId == null || book.BookId.CompareTo(lastBookId) > 0)
                .OrderBy(book => book.BookId)
                .Take(BatchSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (books.Count == 0)
            {
                break;
            }

            string[] bookIds = books.Select(book => book.BookId).ToArray();
            HashSet<string> alreadyMigrated = await _context.LegacyIdentityAliases.AsNoTracking()
                .Where(alias => bookIds.Contains(alias.LegacyBookId))
                .Select(alias => alias.LegacyBookId)
                .ToHashSetAsync(StringComparer.Ordinal, cancellationToken)
                .ConfigureAwait(false);

            using var transaction = await _context.Database
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (BookRow book in books)
            {
                lastBookId = book.BookId;
                if (alreadyMigrated.Contains(book.BookId))
                {
                    continue;
                }

                DateTimeOffset now = DateTimeOffset.UtcNow;
                string workId = CanonicalIdGenerator.NewId();
                string editionId = CanonicalIdGenerator.NewId();
                string catalogueItemId = CanonicalIdGenerator.NewId();
                _context.CanonicalWorks.Add(new CanonicalWorkRow
                {
                    WorkId = workId,
                    ResolutionState = (int)BibliographicResolutionState.Provisional,
                    CanonicalTitle = book.Title,
                    CreatedUtc = now,
                });
                _context.CanonicalEditions.Add(new CanonicalEditionRow
                {
                    EditionId = editionId,
                    WorkId = workId,
                    ResolutionState = (int)BibliographicResolutionState.Provisional,
                    PublicationYear = book.Year,
                    CreatedUtc = now,
                });

                string? assetId = TryGetOrCreateAsset(book, assetsByHash, now, ref createdAssets);
                List<LegacyFileObservation> observations = CreateObservations(book);
                string? preferredOccurrenceId = null;
                var linkedOccurrenceIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (LegacyFileObservation observation in observations)
                {
                    string normalizedLocator = NormalizeRelativePath(observation.RelativePath);
                    if (normalizedLocator.Length is 0 or > 4096)
                    {
                        conflicts++;
                        continue;
                    }

                    if (!occurrencesByLocator.TryGetValue(normalizedLocator, out string? occurrenceId))
                    {
                        occurrenceId = CanonicalIdGenerator.NewId();
                        occurrencesByLocator.Add(normalizedLocator, occurrenceId);
                        _context.FileOccurrences.Add(new FileOccurrenceRow
                        {
                            FileOccurrenceId = occurrenceId,
                            LibraryRootId = CompatibilityRootId,
                            ContentAssetId = assetId,
                            RelativePath = observation.RelativePath,
                            NormalizedRelativePath = normalizedLocator,
                            AvailabilityStatus = observation.AvailabilityStatus,
                            SizeBytes = book.SizeBytes,
                            ModifiedUtcTicks = book.MtimeTicks,
                            PdfFingerprint = book.PdfFingerprint,
                            LastSeenUtc = observation.LastSeenUtc,
                        });
                        createdOccurrences++;
                    }
                    else
                    {
                        FileOccurrenceRow? existingOccurrence = await _context.FileOccurrences
                            .FindAsync([occurrenceId], cancellationToken)
                            .ConfigureAwait(false);
                        if (existingOccurrence is not null)
                        {
                            if (existingOccurrence.ContentAssetId is null)
                            {
                                existingOccurrence.ContentAssetId = assetId;
                            }
                            else if (assetId is not null && existingOccurrence.ContentAssetId != assetId)
                            {
                                conflicts++;
                            }

                            if (observation.AvailabilityStatus == (int)AvailabilityStatus.Available)
                            {
                                existingOccurrence.AvailabilityStatus = observation.AvailabilityStatus;
                            }
                        }
                    }

                    if (linkedOccurrenceIds.Add(occurrenceId))
                    {
                        _context.CatalogueItemOccurrences.Add(new CatalogueItemOccurrenceRow
                        {
                            CatalogueItemId = catalogueItemId,
                            FileOccurrenceId = occurrenceId,
                        });
                    }

                    if (preferredOccurrenceId is null ||
                        observation.AvailabilityStatus == (int)AvailabilityStatus.Available)
                    {
                        preferredOccurrenceId = occurrenceId;
                    }
                }

                _context.CatalogueItems.Add(new CatalogueItemRow
                {
                    CatalogueItemId = catalogueItemId,
                    WorkId = workId,
                    EditionId = editionId,
                    PreferredOccurrenceId = preferredOccurrenceId,
                    CreatedUtc = now,
                });
                if (assetId is not null)
                {
                    _context.EditionContentAssets.Add(new EditionContentAssetRow
                    {
                        EditionId = editionId,
                        ContentAssetId = assetId,
                    });
                }

                AddLegacyBibliographicIdentifiers(book, editionId, conflictingIsbns, conflictingDois);
                _context.LegacyIdentityAliases.Add(new LegacyIdentityAliasRow
                {
                    LegacyBookId = book.BookId,
                    CatalogueItemId = catalogueItemId,
                    WorkId = workId,
                    EditionId = editionId,
                    MigrationVersion = MigrationVersion,
                    CreatedUtc = now,
                });

                book.EmbeddingStatus = 0;
                migrated++;
            }

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            progress?.Report(new CatalogueMigrationProgress("identity.backfill", migrated, total, conflicts));
        }

        stopwatch.Stop();
        return new CanonicalIdentityMigrationResult(
            migrated,
            createdOccurrences,
            createdAssets,
            conflicts,
            stopwatch.Elapsed);
    }

    private async Task EnsureCompatibilityRootAsync(CancellationToken cancellationToken)
    {
        if (await _context.LibraryRoots.AnyAsync(
                root => root.LibraryRootId == CompatibilityRootId,
                cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        _context.LibraryRoots.Add(new LibraryRootRow
        {
            LibraryRootId = CompatibilityRootId,
            DisplayName = "Migrated library location",
            RootStatus = 0,
            IsCompatibilityRoot = true,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private string? TryGetOrCreateAsset(
        BookRow book,
        Dictionary<string, string> assetsByHash,
        DateTimeOffset now,
        ref int createdAssets)
    {
        if (!TryNormalizeSha256(book.Sha256Hash, out string hash))
        {
            return null;
        }

        if (assetsByHash.TryGetValue(hash, out string? existingId))
        {
            return existingId;
        }

        string assetId = CanonicalIdGenerator.NewId();
        assetsByHash.Add(hash, assetId);
        _context.ContentAssets.Add(new ContentAssetRow
        {
            ContentAssetId = assetId,
            Sha256Hash = hash,
            FingerprintVersion = 1,
            SizeBytes = book.SizeBytes > 0 ? book.SizeBytes : null,
            VerificationStatus = 0,
            CreatedUtc = now,
        });
        createdAssets++;
        return assetId;
    }

    private static List<LegacyFileObservation> CreateObservations(BookRow book)
    {
        List<LegacyFileObservation> observations = book.BookFiles
            .Select(file => new LegacyFileObservation(
                file.RelativePath,
                file.FileStatus == 0
                    ? (int)AvailabilityStatus.Available
                    : (int)AvailabilityStatus.Unavailable,
                file.LastSeenUtc == default ? null : file.LastSeenUtc))
            .ToList();
        if (observations.Count == 0 && !string.IsNullOrWhiteSpace(book.RelativePath))
        {
            observations.Add(new LegacyFileObservation(
                book.RelativePath,
                book.Status == 0
                    ? (int)AvailabilityStatus.Available
                    : (int)AvailabilityStatus.Unavailable,
                null));
        }

        return observations;
    }

    private void AddLegacyBibliographicIdentifiers(
        BookRow book,
        string editionId,
        HashSet<string> conflictingIsbns,
        HashSet<string> conflictingDois)
    {
        string? normalizedIsbn = book.IsbnNormalized?.Trim().ToLowerInvariant();
        if (normalizedIsbn is not null &&
            !conflictingIsbns.Contains(normalizedIsbn) &&
            Isbn.TryParse(normalizedIsbn, out Isbn parsedIsbn))
        {
            _context.BibliographicIdentifiers.Add(new BibliographicIdentifierRow
            {
                OwnerScope = (int)BibliographicIdentityScope.Edition,
                EditionId = editionId,
                Source = "legacy.books",
                IdentifierKind = parsedIsbn.Normalized.Length == 10
                    ? (int)BibliographicIdentifierKind.Isbn10
                    : (int)BibliographicIdentifierKind.Isbn13,
                NormalizedValue = parsedIsbn.Normalized,
            });
        }

        string? normalizedDoi = NormalizeDoi(book.Doi);
        if (normalizedDoi is not null && !conflictingDois.Contains(normalizedDoi))
        {
            _context.BibliographicIdentifiers.Add(new BibliographicIdentifierRow
            {
                OwnerScope = (int)BibliographicIdentityScope.Edition,
                EditionId = editionId,
                Source = "legacy.books",
                IdentifierKind = (int)BibliographicIdentifierKind.Doi,
                NormalizedValue = normalizedDoi,
            });
        }
    }

    internal static string NormalizeRelativePath(string path) =>
        path.Trim().Replace('\\', '/').Normalize(NormalizationForm.FormC);

    private static string? NormalizeDoi(string? doi)
    {
        if (string.IsNullOrWhiteSpace(doi))
        {
            return null;
        }

        string normalized = doi.Trim().ToLowerInvariant();
        return normalized.Length <= 512 ? normalized : null;
    }

    private static bool TryNormalizeSha256(string? value, out string hash)
    {
        hash = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64 || !IsHex(value))
        {
            return false;
        }

        hash = value.ToLowerInvariant();
        return true;
    }

    private static bool IsHex(string value) => value.All(character =>
        character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');

    private static async Task<int> CountDuplicateValuesAsync(
        IQueryable<string> query,
        CancellationToken cancellationToken)
    {
        List<string> values = await query.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        return values
            .Select(value => value.Trim().ToLowerInvariant())
            .Where(value => value.Length > 0)
            .GroupBy(value => value, StringComparer.Ordinal)
            .Count(group => group.Count() > 1);
    }

    private static async Task<HashSet<string>> ReadConflictingValuesAsync(
        IQueryable<string> query,
        CancellationToken cancellationToken)
    {
        List<string> values = await query.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        return values
            .Select(value => value.Trim().ToLowerInvariant())
            .Where(value => value.Length > 0)
            .GroupBy(value => value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    private sealed record LegacyFileObservation(
        string RelativePath,
        int AvailabilityStatus,
        DateTimeOffset? LastSeenUtc);
}
