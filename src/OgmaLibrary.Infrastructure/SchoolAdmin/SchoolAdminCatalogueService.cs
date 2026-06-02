using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OgmaLibrary.Application.SchoolAdmin;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.SchoolAdmin;

/// <summary>SQLite-backed school library publishing and shared-shelf service.</summary>
internal sealed class SchoolAdminCatalogueService : ILibraryPublishingService, ISharedShelfService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDbContextFactory<CatalogueDbContext> _contextFactory;

    public SchoolAdminCatalogueService(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<PublishedLibrary> PublishAsync(
        PublishLibraryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string libraryId = RequireTrimmed(request.LibraryId, nameof(request.LibraryId));
        string displayName = RequireTrimmed(request.DisplayName, nameof(request.DisplayName));
        string sourcePath = RequireTrimmed(request.SourcePath, nameof(request.SourcePath));
        DateTimeOffset now = DateTimeOffset.UtcNow;

        CatalogueDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            LibraryPublishSettingsRow? row = await context.LibraryPublishSettings
                .FirstOrDefaultAsync(x => x.LibraryRootId == libraryId, cancellationToken)
                .ConfigureAwait(false);

            if (row is null)
            {
                row = new LibraryPublishSettingsRow { LibraryRootId = libraryId };
                context.LibraryPublishSettings.Add(row);
            }

            row.DisplayName = displayName;
            row.SourcePath = sourcePath;
            row.AiTier = (int)request.AiTier;
            row.IsPublished = true;
            row.UpdatedUtc = now;

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Map(row);
        }
    }

    public async Task UnpublishAsync(string libraryId, CancellationToken cancellationToken = default)
    {
        string normalizedLibraryId = RequireTrimmed(libraryId, nameof(libraryId));
        CatalogueDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            LibraryPublishSettingsRow? row = await context.LibraryPublishSettings
                .FirstOrDefaultAsync(x => x.LibraryRootId == normalizedLibraryId, cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
            {
                return;
            }

            row.IsPublished = false;
            row.UpdatedUtc = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<PublishedLibrary>> ListAsync(CancellationToken cancellationToken = default)
    {
        CatalogueDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            List<LibraryPublishSettingsRow> rows = await context.LibraryPublishSettings
                .AsNoTracking()
                .OrderBy(row => row.DisplayName)
                .ThenBy(row => row.LibraryRootId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            return rows.Select(Map).ToList();
        }
    }

    public async Task<SharedShelf> SaveAsync(
        SaveSharedShelfRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string shelfId = RequireTrimmed(request.ShelfId, nameof(request.ShelfId));
        string name = RequireTrimmed(request.Name, nameof(request.Name));
        string[] bookIds = request.BookIds
            .Select(bookId => RequireTrimmed(bookId, nameof(request.BookIds)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(bookId => bookId, StringComparer.Ordinal)
            .ToArray();
        string[] groupIds = request.GroupIds
            .Select(groupId => RequireTrimmed(groupId, nameof(request.GroupIds)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(groupId => groupId, StringComparer.Ordinal)
            .ToArray();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        CatalogueDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            IDbContextTransaction transaction = await context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {

                SharedShelfRow? row = await context.SharedShelves
                    .Include(shelf => shelf.Books)
                    .FirstOrDefaultAsync(x => x.ShelfId == shelfId, cancellationToken)
                    .ConfigureAwait(false);
                if (row is null)
                {
                    row = new SharedShelfRow
                    {
                        ShelfId = shelfId,
                        CreatedUtc = now,
                    };
                    context.SharedShelves.Add(row);
                }

                row.Name = name;
                row.Visibility = (int)request.Visibility;
                row.GroupIdsJson = JsonSerializer.Serialize(groupIds, JsonOptions);
                row.UpdatedUtc = now;
                row.IsDeleted = false;

                HashSet<string> requestedBooks = bookIds.ToHashSet(StringComparer.Ordinal);
                foreach (SharedShelfBookRow existing in row.Books.Where(book => !requestedBooks.Contains(book.BookId)).ToList())
                {
                    context.SharedShelfBooks.Remove(existing);
                }

                HashSet<string> existingBookIds = row.Books.Select(book => book.BookId).ToHashSet(StringComparer.Ordinal);
                foreach (string bookId in bookIds.Where(bookId => !existingBookIds.Contains(bookId)))
                {
                    row.Books.Add(new SharedShelfBookRow
                    {
                        ShelfId = shelfId,
                        BookId = bookId,
                        AddedUtc = now,
                    });
                }

                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return Map(row);
            }
        }
    }

    public async Task DeleteAsync(string shelfId, CancellationToken cancellationToken = default)
    {
        string normalizedShelfId = RequireTrimmed(shelfId, nameof(shelfId));
        CatalogueDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            SharedShelfRow? row = await context.SharedShelves
                .Include(shelf => shelf.Books)
                .FirstOrDefaultAsync(x => x.ShelfId == normalizedShelfId, cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
            {
                return;
            }

            row.IsDeleted = true;
            row.UpdatedUtc = DateTimeOffset.UtcNow;
            context.SharedShelfBooks.RemoveRange(row.Books);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    async Task<IReadOnlyList<SharedShelf>> ISharedShelfService.ListAsync(CancellationToken cancellationToken)
    {
        CatalogueDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            List<SharedShelfRow> rows = await context.SharedShelves
                .AsNoTracking()
                .Include(shelf => shelf.Books)
                .Where(shelf => !shelf.IsDeleted)
                .OrderBy(shelf => shelf.Name)
                .ThenBy(shelf => shelf.ShelfId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            return rows.Select(Map).ToList();
        }
    }

    private static PublishedLibrary Map(LibraryPublishSettingsRow row) =>
        new(
            row.LibraryRootId,
            row.DisplayName,
            row.SourcePath,
            (AiPrivacyTier)row.AiTier,
            row.IsPublished,
            row.UpdatedUtc);

    private static SharedShelf Map(SharedShelfRow row) =>
        new(
            row.ShelfId,
            row.Name,
            (SharedShelfVisibility)row.Visibility,
            row.Books.Select(book => book.BookId).OrderBy(bookId => bookId, StringComparer.Ordinal).ToList(),
            DeserializeGroupIds(row.GroupIdsJson),
            row.UpdatedUtc);

    private static IReadOnlyList<string> DeserializeGroupIds(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<string>>(value, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string RequireTrimmed(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return value.Trim();
    }
}
