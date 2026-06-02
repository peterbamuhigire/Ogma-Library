using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Infrastructure.ClassroomClient.Data;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>Creates the Phase 17 per-profile private database location.</summary>
internal sealed class StudentPrivateRepository : IStudentPrivateRepository
{
    private readonly string _profileRoot;

    public StudentPrivateRepository(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        _profileRoot = Path.Combine(dataDirectory, "classroom", "profiles");
    }

    public string GetPrivateDatabasePath(Guid profileId)
    {
        if (profileId == Guid.Empty)
        {
            throw new ArgumentException("Profile id is required.", nameof(profileId));
        }

        return Path.Combine(_profileRoot, profileId.ToString("N"), "private.db");
    }

    public async Task EnsureCreatedAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = GetPrivateDatabasePath(profileId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using StudentDbContext context = CreateContext(path);
        await context.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<StudentReadingProgress?> GetReadingProgressAsync(
        Guid profileId,
        string hostId,
        string bookId,
        CancellationToken cancellationToken = default)
    {
        ValidateScope(hostId, bookId);
        using StudentDbContext context = await OpenAsync(profileId, cancellationToken).ConfigureAwait(false);
        StudentReadingProgressRow? row = await context.ReadingProgress
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.HostId == hostId && candidate.BookId == bookId,
                cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : Map(row);
    }

    public async Task<IReadOnlyList<StudentReadingProgress>> ListReadingProgressAsync(
        Guid profileId,
        string hostId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);
        using StudentDbContext context = await OpenAsync(profileId, cancellationToken).ConfigureAwait(false);
        List<StudentReadingProgressRow> rows = await context.ReadingProgress
            .AsNoTracking()
            .Where(row => row.HostId == hostId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows
            .OrderBy(row => row.BookId, StringComparer.Ordinal)
            .Select(Map)
            .ToArray();
    }

    public async Task SaveReadingProgressAsync(
        Guid profileId,
        StudentReadingProgress progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ValidateScope(progress.HostId, progress.BookId);
        using StudentDbContext context = await OpenAsync(profileId, cancellationToken).ConfigureAwait(false);
        StudentReadingProgressRow? row = await context.ReadingProgress
            .SingleOrDefaultAsync(
                candidate => candidate.HostId == progress.HostId && candidate.BookId == progress.BookId,
                cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            context.ReadingProgress.Add(new StudentReadingProgressRow
            {
                HostId = progress.HostId,
                BookId = progress.BookId,
                LastPage = progress.LastPage,
                LastOffsetY = progress.LastOffsetY,
                UpdatedUtc = progress.UpdatedUtc,
            });
        }
        else
        {
            row.LastPage = progress.LastPage;
            row.LastOffsetY = progress.LastOffsetY;
            row.UpdatedUtc = progress.UpdatedUtc;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<StudentAnnotation>> ListAnnotationsAsync(
        Guid profileId,
        string hostId,
        string bookId,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        ValidateScope(hostId, bookId);
        using StudentDbContext context = await OpenAsync(profileId, cancellationToken).ConfigureAwait(false);
        IQueryable<StudentAnnotationRow> query = context.Annotations
            .AsNoTracking()
            .Where(row => row.HostId == hostId && row.BookId == bookId);
        if (!includeDeleted)
        {
            query = query.Where(row => !row.IsDeleted);
        }

        List<StudentAnnotationRow> rows = await query
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows
            .OrderBy(row => row.PageNumber)
            .ThenBy(row => row.CreatedUtc)
            .Select(Map)
            .ToArray();
    }

    public async Task<IReadOnlyList<StudentAnnotation>> ListAnnotationsForHostAsync(
        Guid profileId,
        string hostId,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);
        using StudentDbContext context = await OpenAsync(profileId, cancellationToken).ConfigureAwait(false);
        IQueryable<StudentAnnotationRow> query = context.Annotations
            .AsNoTracking()
            .Where(row => row.HostId == hostId);
        if (!includeDeleted)
        {
            query = query.Where(row => !row.IsDeleted);
        }

        List<StudentAnnotationRow> rows = await query
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows
            .OrderBy(row => row.BookId, StringComparer.Ordinal)
            .ThenBy(row => row.PageNumber)
            .ThenBy(row => row.CreatedUtc)
            .ThenBy(row => row.Id, StringComparer.Ordinal)
            .Select(Map)
            .ToArray();
    }

    public async Task SaveAnnotationAsync(
        Guid profileId,
        StudentAnnotation annotation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        ValidateScope(annotation.HostId, annotation.BookId);
        ArgumentException.ThrowIfNullOrWhiteSpace(annotation.Id);
        using StudentDbContext context = await OpenAsync(profileId, cancellationToken).ConfigureAwait(false);
        StudentAnnotationRow? row = await context.Annotations
            .SingleOrDefaultAsync(candidate => candidate.Id == annotation.Id, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            context.Annotations.Add(new StudentAnnotationRow
            {
                Id = annotation.Id,
                HostId = annotation.HostId,
                BookId = annotation.BookId,
                PageNumber = annotation.PageNumber,
                Type = annotation.Type,
                Color = annotation.Color,
                Body = annotation.Body,
                CreatedUtc = annotation.CreatedUtc,
                UpdatedUtc = annotation.UpdatedUtc,
                IsDeleted = annotation.IsDeleted,
            });
        }
        else
        {
            row.HostId = annotation.HostId;
            row.BookId = annotation.BookId;
            row.PageNumber = annotation.PageNumber;
            row.Type = annotation.Type;
            row.Color = annotation.Color;
            row.Body = annotation.Body;
            row.CreatedUtc = annotation.CreatedUtc;
            row.UpdatedUtc = annotation.UpdatedUtc;
            row.IsDeleted = annotation.IsDeleted;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SoftDeleteAnnotationAsync(
        Guid profileId,
        string annotationId,
        DateTimeOffset updatedUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(annotationId);
        using StudentDbContext context = await OpenAsync(profileId, cancellationToken).ConfigureAwait(false);
        StudentAnnotationRow? row = await context.Annotations
            .SingleOrDefaultAsync(candidate => candidate.Id == annotationId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return;
        }

        row.IsDeleted = true;
        row.UpdatedUtc = updatedUtc;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<StudentBookmark>> ListBookmarksAsync(
        Guid profileId,
        string hostId,
        string bookId,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        ValidateScope(hostId, bookId);
        using StudentDbContext context = await OpenAsync(profileId, cancellationToken).ConfigureAwait(false);
        IQueryable<StudentBookmarkRow> query = context.Bookmarks
            .AsNoTracking()
            .Where(row => row.HostId == hostId && row.BookId == bookId);
        if (!includeDeleted)
        {
            query = query.Where(row => !row.IsDeleted);
        }

        List<StudentBookmarkRow> rows = await query
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows
            .OrderBy(row => row.PageNumber)
            .ThenBy(row => row.CreatedUtc)
            .Select(Map)
            .ToArray();
    }

    public async Task<IReadOnlyList<StudentBookmark>> ListBookmarksForHostAsync(
        Guid profileId,
        string hostId,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);
        using StudentDbContext context = await OpenAsync(profileId, cancellationToken).ConfigureAwait(false);
        IQueryable<StudentBookmarkRow> query = context.Bookmarks
            .AsNoTracking()
            .Where(row => row.HostId == hostId);
        if (!includeDeleted)
        {
            query = query.Where(row => !row.IsDeleted);
        }

        List<StudentBookmarkRow> rows = await query
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows
            .OrderBy(row => row.BookId, StringComparer.Ordinal)
            .ThenBy(row => row.PageNumber)
            .ThenBy(row => row.CreatedUtc)
            .ThenBy(row => row.Id, StringComparer.Ordinal)
            .Select(Map)
            .ToArray();
    }

    public async Task SaveBookmarkAsync(
        Guid profileId,
        StudentBookmark bookmark,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bookmark);
        ValidateScope(bookmark.HostId, bookmark.BookId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bookmark.Id);
        using StudentDbContext context = await OpenAsync(profileId, cancellationToken).ConfigureAwait(false);
        StudentBookmarkRow? row = await context.Bookmarks
            .SingleOrDefaultAsync(candidate => candidate.Id == bookmark.Id, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            context.Bookmarks.Add(new StudentBookmarkRow
            {
                Id = bookmark.Id,
                HostId = bookmark.HostId,
                BookId = bookmark.BookId,
                PageNumber = bookmark.PageNumber,
                Label = bookmark.Label,
                CreatedUtc = bookmark.CreatedUtc,
                UpdatedUtc = bookmark.UpdatedUtc,
                IsDeleted = bookmark.IsDeleted,
            });
        }
        else
        {
            row.HostId = bookmark.HostId;
            row.BookId = bookmark.BookId;
            row.PageNumber = bookmark.PageNumber;
            row.Label = bookmark.Label;
            row.CreatedUtc = bookmark.CreatedUtc;
            row.UpdatedUtc = bookmark.UpdatedUtc;
            row.IsDeleted = bookmark.IsDeleted;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SoftDeleteBookmarkAsync(
        Guid profileId,
        string bookmarkId,
        DateTimeOffset updatedUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookmarkId);
        using StudentDbContext context = await OpenAsync(profileId, cancellationToken).ConfigureAwait(false);
        StudentBookmarkRow? row = await context.Bookmarks
            .SingleOrDefaultAsync(candidate => candidate.Id == bookmarkId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return;
        }

        row.IsDeleted = true;
        row.UpdatedUtc = updatedUtc;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<StudentAiHistoryEntry>> ListAiHistoryAsync(
        Guid profileId,
        string hostId,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);
        using StudentDbContext context = await OpenAsync(profileId, cancellationToken).ConfigureAwait(false);
        IQueryable<StudentAiHistoryRow> query = context.AiHistory
            .AsNoTracking()
            .Where(row => row.HostId == hostId);
        if (!includeDeleted)
        {
            query = query.Where(row => !row.IsDeleted);
        }

        List<StudentAiHistoryRow> rows = await query
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows
            .OrderBy(row => row.CreatedUtc)
            .Select(Map)
            .ToArray();
    }

    public async Task SaveAiHistoryAsync(
        Guid profileId,
        StudentAiHistoryEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.HostId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Query);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Tier);
        using StudentDbContext context = await OpenAsync(profileId, cancellationToken).ConfigureAwait(false);
        StudentAiHistoryRow? row = await context.AiHistory
            .SingleOrDefaultAsync(candidate => candidate.Id == entry.Id, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            context.AiHistory.Add(new StudentAiHistoryRow
            {
                Id = entry.Id,
                HostId = entry.HostId,
                Query = entry.Query,
                ResponseSummary = entry.ResponseSummary,
                Tier = entry.Tier,
                CreatedUtc = entry.CreatedUtc,
                IsDeleted = entry.IsDeleted,
            });
        }
        else
        {
            row.HostId = entry.HostId;
            row.Query = entry.Query;
            row.ResponseSummary = entry.ResponseSummary;
            row.Tier = entry.Tier;
            row.CreatedUtc = entry.CreatedUtc;
            row.IsDeleted = entry.IsDeleted;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<StudentSyncState?> GetSyncStateAsync(
        Guid profileId,
        string hostId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);
        using StudentDbContext context = await OpenAsync(profileId, cancellationToken).ConfigureAwait(false);
        StudentSyncStateRow? row = await context.SyncState
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.HostId == hostId, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : Map(row);
    }

    public async Task SaveSyncStateAsync(
        Guid profileId,
        StudentSyncState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(state.HostId);
        using StudentDbContext context = await OpenAsync(profileId, cancellationToken).ConfigureAwait(false);
        StudentSyncStateRow? row = await context.SyncState
            .SingleOrDefaultAsync(candidate => candidate.HostId == state.HostId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            context.SyncState.Add(new StudentSyncStateRow
            {
                HostId = state.HostId,
                LastSyncedUtc = state.LastSyncedUtc,
                LastSyncBlobHash = state.LastSyncBlobHash,
                ConflictCount = state.ConflictCount,
            });
        }
        else
        {
            row.LastSyncedUtc = state.LastSyncedUtc;
            row.LastSyncBlobHash = state.LastSyncBlobHash;
            row.ConflictCount = state.ConflictCount;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string profileDirectory = Path.GetDirectoryName(GetPrivateDatabasePath(profileId))!;
        if (Directory.Exists(profileDirectory))
        {
            Directory.Delete(profileDirectory, recursive: true);
        }

        return Task.CompletedTask;
    }

    private async Task<StudentDbContext> OpenAsync(Guid profileId, CancellationToken cancellationToken)
    {
        await EnsureCreatedAsync(profileId, cancellationToken).ConfigureAwait(false);
        return CreateContext(GetPrivateDatabasePath(profileId));
    }

    private static StudentDbContext CreateContext(string path)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Pooling = false,
        }.ToString();
        var options = new DbContextOptionsBuilder<StudentDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new StudentDbContext(options);
    }

    private static void ValidateScope(string hostId, string bookId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
    }

    private static StudentReadingProgress Map(StudentReadingProgressRow row) =>
        new(row.HostId, row.BookId, row.LastPage, row.LastOffsetY, row.UpdatedUtc);

    private static StudentAnnotation Map(StudentAnnotationRow row) =>
        new(
            row.Id,
            row.HostId,
            row.BookId,
            row.PageNumber,
            row.Type,
            row.Color,
            row.Body,
            row.CreatedUtc,
            row.UpdatedUtc,
            row.IsDeleted);

    private static StudentBookmark Map(StudentBookmarkRow row) =>
        new(
            row.Id,
            row.HostId,
            row.BookId,
            row.PageNumber,
            row.Label,
            row.CreatedUtc,
            row.UpdatedUtc,
            row.IsDeleted);

    private static StudentAiHistoryEntry Map(StudentAiHistoryRow row) =>
        new(
            row.Id,
            row.HostId,
            row.Query,
            row.ResponseSummary,
            row.Tier,
            row.CreatedUtc,
            row.IsDeleted);

    private static StudentSyncState Map(StudentSyncStateRow row) =>
        new(row.HostId, row.LastSyncedUtc, row.LastSyncBlobHash, row.ConflictCount);
}
