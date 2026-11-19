using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Ingestion;

namespace OgmaLibrary.Infrastructure.Catalogue.Repositories;

/// <summary>
/// Maps catalogue books to canonical file occurrences and reads file facts for Compare
/// (Sept-23 Phase 10, task 10.9). Read-only apart from the canonical identity backfill, which the
/// startup migrator would otherwise run only at the next launch.
/// </summary>
public sealed class BookOccurrenceReader : IBookOccurrenceReader
{
    private const int HashPrefixLength = 12;

    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>Test constructor using an existing context.</summary>
    /// <param name="context">The catalogue context.</param>
    internal BookOccurrenceReader(CatalogueDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>Initializes the runtime reader.</summary>
    /// <param name="contextFactory">Factory for operation-scoped contexts.</param>
    public BookOccurrenceReader(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ResolveOccurrenceIdsAsync(
        IReadOnlyList<string> bookIds,
        CancellationToken cancellationToken = default)
    {
        string[] ids = Normalize(bookIds);
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        bool missing = await context.Books.AsNoTracking()
            .AnyAsync(
                book => ids.Contains(book.BookId) &&
                        !context.LegacyIdentityAliases.Any(alias => alias.LegacyBookId == book.BookId),
                cancellationToken)
            .ConfigureAwait(false);
        if (missing)
        {
            // Books scanned since launch have no canonical identity yet; the backfill is
            // incremental and skips books that already have one.
            await new CanonicalIdentityMigrationService(context)
                .ApplyAsync(progress: null, cancellationToken)
                .ConfigureAwait(false);
        }

        var rows = await (
                from alias in context.LegacyIdentityAliases.AsNoTracking()
                join link in context.CatalogueItemOccurrences.AsNoTracking()
                    on alias.CatalogueItemId equals link.CatalogueItemId
                where ids.Contains(alias.LegacyBookId)
                select new { alias.LegacyBookId, link.FileOccurrenceId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (string id in ids)
        {
            result[id] = rows
                .Where(row => string.Equals(row.LegacyBookId, id, StringComparison.Ordinal))
                .Select(row => row.FileOccurrenceId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BookFileFacts>> GetFileFactsAsync(
        IReadOnlyList<string> bookIds,
        CancellationToken cancellationToken = default)
    {
        string[] ids = Normalize(bookIds);
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        // Widen to every book sharing an active identity group with the requested ones.
        List<string> groupIds = await (
                from alias in context.LegacyIdentityAliases.AsNoTracking()
                join link in context.CatalogueItemOccurrences.AsNoTracking()
                    on alias.CatalogueItemId equals link.CatalogueItemId
                join member in context.IdentityGroupMembers.AsNoTracking()
                    on link.FileOccurrenceId equals member.FileOccurrenceId
                where ids.Contains(alias.LegacyBookId) && member.IsActive
                select member.IdentityGroupId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        List<string> siblings = groupIds.Count == 0
            ? []
            : await (
                    from member in context.IdentityGroupMembers.AsNoTracking()
                    join link in context.CatalogueItemOccurrences.AsNoTracking()
                        on member.FileOccurrenceId equals link.FileOccurrenceId
                    join alias in context.LegacyIdentityAliases.AsNoTracking()
                        on link.CatalogueItemId equals alias.CatalogueItemId
                    where groupIds.Contains(member.IdentityGroupId) && member.IsActive
                    select alias.LegacyBookId)
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        string[] allIds = [.. ids.Union(siblings, StringComparer.Ordinal)];

        var files = await context.BookFiles.AsNoTracking()
            .Where(file => allIds.Contains(file.BookId))
            .Select(file => new
            {
                file.BookId,
                file.Book!.Title,
                file.RelativePath,
                file.Book.SizeBytes,
                file.Book.Sha256Hash,
                file.FileValidity,
                file.FileStatus,
                file.Book.AddedUtc,
                file.LastSeenUtc,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return files
            .OrderBy(file => file.BookId, StringComparer.Ordinal)
            .ThenBy(file => file.RelativePath, StringComparer.Ordinal)
            .Select(file => new BookFileFacts(
                file.BookId,
                // Like the card: with no stored title, each file is named by its own file name.
                string.IsNullOrWhiteSpace(file.Title)
                    ? Path.GetFileNameWithoutExtension(file.RelativePath.Replace('\\', '/').Split('/').Last())
                    : file.Title,
                file.RelativePath,
                file.SizeBytes,
                file.Sha256Hash is { Length: > 0 } hash ? hash[..Math.Min(HashPrefixLength, hash.Length)] : null,
                Enum.IsDefined(typeof(FileValidity), file.FileValidity) ? (FileValidity)file.FileValidity : FileValidity.Damaged,
                file.FileStatus == 0,
                file.AddedUtc,
                file.LastSeenUtc))
            .ToArray();
    }

    private static string[] Normalize(IReadOnlyList<string> bookIds)
    {
        ArgumentNullException.ThrowIfNull(bookIds);
        return bookIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
