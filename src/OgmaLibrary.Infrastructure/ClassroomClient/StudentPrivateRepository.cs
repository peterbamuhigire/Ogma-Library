using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Application.Security;
using OgmaLibrary.Infrastructure.ClassroomClient.Data;
using OgmaLibrary.Infrastructure.Security;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>Creates the Phase 17 per-profile private database location.</summary>
internal sealed class StudentPrivateRepository : IStudentPrivateRepository
{
    private const string PrivateDatabaseKeyPrefix = "ogma.classroom.private-db-key.";
    private const string AnnotationBodyPurpose = "student-annotation-body";
    private const string ConflictLocalBodyPurpose = "student-conflict-local-body";
    private const string ConflictRemoteBodyPurpose = "student-conflict-remote-body";
    private const string AiQueryPurpose = "student-ai-query";
    private const string AiResponsePurpose = "student-ai-response";
    private readonly string _profileRoot;
    private readonly IClassroomCredentialStore _credentialStore;
    private readonly IAtRestEncryptionService _encryption;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _keyInitializationGates = new();

    public StudentPrivateRepository(string dataDirectory)
        : this(
            dataDirectory,
            new InMemoryClassroomCredentialStore(),
            new AesGcmAtRestEncryptionService())
    {
    }

    public StudentPrivateRepository(
        string dataDirectory,
        IClassroomCredentialStore credentialStore,
        IAtRestEncryptionService encryption)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        _encryption = encryption ?? throw new ArgumentNullException(nameof(encryption));
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
        await EnsureConflictSchemaAsync(context, cancellationToken).ConfigureAwait(false);
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
        byte[] key = await GetEncryptionKeyAsync(profileId, cancellationToken).ConfigureAwait(false);
        return rows
            .OrderBy(row => row.PageNumber)
            .ThenBy(row => row.CreatedUtc)
            .Select(row => Map(row, key))
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
        byte[] key = await GetEncryptionKeyAsync(profileId, cancellationToken).ConfigureAwait(false);
        return rows
            .OrderBy(row => row.BookId, StringComparer.Ordinal)
            .ThenBy(row => row.PageNumber)
            .ThenBy(row => row.CreatedUtc)
            .ThenBy(row => row.Id, StringComparer.Ordinal)
            .Select(row => Map(row, key))
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
        byte[] key = await GetEncryptionKeyAsync(profileId, cancellationToken).ConfigureAwait(false);
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
                Body = _encryption.Protect(annotation.Body, key, AnnotationBodyPurpose),
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
            row.Body = _encryption.Protect(annotation.Body, key, AnnotationBodyPurpose);
            row.CreatedUtc = annotation.CreatedUtc;
            row.UpdatedUtc = annotation.UpdatedUtc;
            row.IsDeleted = annotation.IsDeleted;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<StudentAnnotationConflict>> ListAnnotationConflictsAsync(
        Guid profileId,
        string hostId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);
        using StudentDbContext context = await OpenAsync(profileId, cancellationToken).ConfigureAwait(false);
        List<StudentAnnotationConflictRow> rows = await context.AnnotationConflicts
            .AsNoTracking()
            .Where(row => row.HostId == hostId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        byte[] key = await GetEncryptionKeyAsync(profileId, cancellationToken).ConfigureAwait(false);
        return rows
            .OrderBy(row => row.BookId, StringComparer.Ordinal)
            .ThenBy(row => row.PageNumber)
            .ThenBy(row => row.DetectedUtc)
            .ThenBy(row => row.AnnotationId, StringComparer.Ordinal)
            .Select(row => Map(row, key))
            .ToArray();
    }

    public async Task SaveAnnotationConflictAsync(
        Guid profileId,
        StudentAnnotationConflict conflict,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conflict);
        ArgumentException.ThrowIfNullOrWhiteSpace(conflict.HostId);
        ArgumentException.ThrowIfNullOrWhiteSpace(conflict.LocalAnnotation.Id);
        if (!string.Equals(conflict.LocalAnnotation.Id, conflict.RemoteAnnotation.Id, StringComparison.Ordinal) ||
            !string.Equals(conflict.HostId, conflict.LocalAnnotation.HostId, StringComparison.Ordinal) ||
            !string.Equals(conflict.HostId, conflict.RemoteAnnotation.HostId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Conflict rows must refer to the same Host and annotation.", nameof(conflict));
        }

        using StudentDbContext context = await OpenAsync(profileId, cancellationToken).ConfigureAwait(false);
        byte[] key = await GetEncryptionKeyAsync(profileId, cancellationToken).ConfigureAwait(false);
        StudentAnnotationConflictRow? row = await context.AnnotationConflicts
            .SingleOrDefaultAsync(
                candidate => candidate.HostId == conflict.HostId &&
                             candidate.AnnotationId == conflict.LocalAnnotation.Id,
                cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            context.AnnotationConflicts.Add(CreateRow(conflict, key));
        }
        else
        {
            Apply(row, conflict, key);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAnnotationConflictAsync(
        Guid profileId,
        string hostId,
        string annotationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);
        ArgumentException.ThrowIfNullOrWhiteSpace(annotationId);
        using StudentDbContext context = await OpenAsync(profileId, cancellationToken).ConfigureAwait(false);
        StudentAnnotationConflictRow? row = await context.AnnotationConflicts
            .SingleOrDefaultAsync(
                candidate => candidate.HostId == hostId && candidate.AnnotationId == annotationId,
                cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return;
        }

        context.AnnotationConflicts.Remove(row);
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
        byte[] key = await GetEncryptionKeyAsync(profileId, cancellationToken).ConfigureAwait(false);
        return rows
            .OrderBy(row => row.CreatedUtc)
            .Select(row => Map(row, key))
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
        byte[] key = await GetEncryptionKeyAsync(profileId, cancellationToken).ConfigureAwait(false);
        StudentAiHistoryRow? row = await context.AiHistory
            .SingleOrDefaultAsync(candidate => candidate.Id == entry.Id, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            context.AiHistory.Add(new StudentAiHistoryRow
            {
                Id = entry.Id,
                HostId = entry.HostId,
                Query = _encryption.Protect(entry.Query, key, AiQueryPurpose)!,
                ResponseSummary = _encryption.Protect(entry.ResponseSummary, key, AiResponsePurpose),
                Tier = entry.Tier,
                CreatedUtc = entry.CreatedUtc,
                IsDeleted = entry.IsDeleted,
            });
        }
        else
        {
            row.HostId = entry.HostId;
            row.Query = _encryption.Protect(entry.Query, key, AiQueryPurpose)!;
            row.ResponseSummary = _encryption.Protect(entry.ResponseSummary, key, AiResponsePurpose);
            row.Tier = entry.Tier;
            row.CreatedUtc = entry.CreatedUtc;
            row.IsDeleted = entry.IsDeleted;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> DeleteAiHistoryAsync(
        Guid profileId,
        string hostId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);
        using StudentDbContext context = await OpenAsync(profileId, cancellationToken).ConfigureAwait(false);
        return await context.AiHistory
            .Where(row => row.HostId == hostId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
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

    public async Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string profileDirectory = Path.GetDirectoryName(GetPrivateDatabasePath(profileId))!;
        if (Directory.Exists(profileDirectory))
        {
            Directory.Delete(profileDirectory, recursive: true);
        }

        await _credentialStore.DeleteSecretAsync(CreatePrivateDatabaseKeyName(profileId), cancellationToken)
            .ConfigureAwait(false);
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

    private async Task<byte[]> GetEncryptionKeyAsync(
        Guid profileId,
        CancellationToken cancellationToken)
    {
        string secretName = CreatePrivateDatabaseKeyName(profileId);
        string? encodedSecret = await _credentialStore
            .GetSecretAsync(secretName, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(encodedSecret))
        {
            SemaphoreSlim gate = _keyInitializationGates.GetOrAdd(profileId, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                encodedSecret = await _credentialStore
                    .GetSecretAsync(secretName, cancellationToken)
                    .ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(encodedSecret))
                {
                    byte[] generatedSecret = RandomNumberGenerator.GetBytes(32);
                    try
                    {
                        encodedSecret = Convert.ToBase64String(generatedSecret);
                        await _credentialStore.SaveSecretAsync(
                                secretName,
                                encodedSecret,
                                cancellationToken)
                            .ConfigureAwait(false);
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(generatedSecret);
                    }
                }
            }
            finally
            {
                gate.Release();
            }
        }

        byte[] deviceSecret;
        try
        {
            deviceSecret = Convert.FromBase64String(encodedSecret!);
        }
        catch (FormatException error)
        {
            throw new CryptographicException("The classroom private database key is malformed.", error);
        }

        try
        {
            return _encryption.DeriveKey(deviceSecret, profileId.ToString("N"));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(deviceSecret);
        }
    }

    private static string CreatePrivateDatabaseKeyName(Guid profileId) =>
        $"{PrivateDatabaseKeyPrefix}{profileId:N}";

    private static Task<int> EnsureConflictSchemaAsync(StudentDbContext context, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS StudentAnnotationConflicts (
                HostId TEXT NOT NULL,
                AnnotationId TEXT NOT NULL,
                BookId TEXT NOT NULL,
                PageNumber INTEGER NOT NULL,
                Type TEXT NOT NULL,
                LocalColor TEXT NULL,
                LocalBody TEXT NULL,
                LocalCreatedUtc TEXT NOT NULL,
                LocalUpdatedUtc TEXT NOT NULL,
                LocalIsDeleted INTEGER NOT NULL,
                RemoteColor TEXT NULL,
                RemoteBookId TEXT NOT NULL,
                RemotePageNumber INTEGER NOT NULL,
                RemoteType TEXT NOT NULL,
                RemoteBody TEXT NULL,
                RemoteCreatedUtc TEXT NOT NULL,
                RemoteUpdatedUtc TEXT NOT NULL,
                RemoteIsDeleted INTEGER NOT NULL,
                DetectedUtc TEXT NOT NULL,
                CONSTRAINT PK_StudentAnnotationConflicts PRIMARY KEY (HostId, AnnotationId)
            );
            CREATE INDEX IF NOT EXISTS IX_StudentAnnotationConflicts_HostId_BookId_PageNumber
                ON StudentAnnotationConflicts (HostId, BookId, PageNumber);
            """,
            cancellationToken);

    private static void ValidateScope(string hostId, string bookId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
    }

    private static StudentReadingProgress Map(StudentReadingProgressRow row) =>
        new(row.HostId, row.BookId, row.LastPage, row.LastOffsetY, row.UpdatedUtc);

    private StudentAnnotation Map(StudentAnnotationRow row, byte[] key) =>
        new(
            row.Id,
            row.HostId,
            row.BookId,
            row.PageNumber,
            row.Type,
            row.Color,
            _encryption.Unprotect(row.Body, key, AnnotationBodyPurpose),
            row.CreatedUtc,
            row.UpdatedUtc,
            row.IsDeleted);

    private StudentAnnotationConflict Map(StudentAnnotationConflictRow row, byte[] key) =>
        new(
            row.HostId,
            new StudentAnnotation(
                row.AnnotationId,
                row.HostId,
                row.BookId,
                row.PageNumber,
                row.Type,
                row.LocalColor,
                _encryption.Unprotect(row.LocalBody, key, ConflictLocalBodyPurpose),
                row.LocalCreatedUtc,
                row.LocalUpdatedUtc,
                row.LocalIsDeleted),
            new StudentAnnotation(
                row.AnnotationId,
                row.HostId,
                row.RemoteBookId,
                row.RemotePageNumber,
                row.RemoteType,
                row.RemoteColor,
                _encryption.Unprotect(row.RemoteBody, key, ConflictRemoteBodyPurpose),
                row.RemoteCreatedUtc,
                row.RemoteUpdatedUtc,
                row.RemoteIsDeleted),
            row.DetectedUtc);

    private StudentAnnotationConflictRow CreateRow(StudentAnnotationConflict conflict, byte[] key)
    {
        var row = new StudentAnnotationConflictRow();
        Apply(row, conflict, key);
        return row;
    }

    private void Apply(StudentAnnotationConflictRow row, StudentAnnotationConflict conflict, byte[] key)
    {
        StudentAnnotation local = conflict.LocalAnnotation;
        StudentAnnotation remote = conflict.RemoteAnnotation;
        row.HostId = conflict.HostId;
        row.AnnotationId = local.Id;
        row.BookId = local.BookId;
        row.PageNumber = local.PageNumber;
        row.Type = local.Type;
        row.LocalColor = local.Color;
        row.LocalBody = _encryption.Protect(local.Body, key, ConflictLocalBodyPurpose);
        row.LocalCreatedUtc = local.CreatedUtc;
        row.LocalUpdatedUtc = local.UpdatedUtc;
        row.LocalIsDeleted = local.IsDeleted;
        row.RemoteColor = remote.Color;
        row.RemoteBookId = remote.BookId;
        row.RemotePageNumber = remote.PageNumber;
        row.RemoteType = remote.Type;
        row.RemoteBody = _encryption.Protect(remote.Body, key, ConflictRemoteBodyPurpose);
        row.RemoteCreatedUtc = remote.CreatedUtc;
        row.RemoteUpdatedUtc = remote.UpdatedUtc;
        row.RemoteIsDeleted = remote.IsDeleted;
        row.DetectedUtc = conflict.DetectedUtc;
    }

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

    private StudentAiHistoryEntry Map(StudentAiHistoryRow row, byte[] key) =>
        new(
            row.Id,
            row.HostId,
            _encryption.Unprotect(row.Query, key, AiQueryPurpose)!,
            _encryption.Unprotect(row.ResponseSummary, key, AiResponsePurpose),
            row.Tier,
            row.CreatedUtc,
            row.IsDeleted);

    private static StudentSyncState Map(StudentSyncStateRow row) =>
        new(row.HostId, row.LastSyncedUtc, row.LastSyncBlobHash, row.ConflictCount);
}
