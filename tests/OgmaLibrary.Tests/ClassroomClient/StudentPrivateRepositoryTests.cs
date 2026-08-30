using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Infrastructure.ClassroomClient;

namespace OgmaLibrary.Tests.ClassroomClient;

/// <summary>Phase 17 per-student private database tests.</summary>
public sealed class StudentPrivateRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task StudentPrivateRepository_PersistsReadingProgress_InProfileDatabase()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            Guid profileId = Guid.NewGuid();
            var credentials = new TestCredentialStore();
            var firstRepository = new StudentPrivateRepository(
                dataDirectory,
                credentials,
                new OgmaLibrary.Infrastructure.Security.AesGcmAtRestEncryptionService());
            var progress = new StudentReadingProgress("host-1", "book-1", 42, 12.5, Now);

            await firstRepository.SaveReadingProgressAsync(profileId, progress);

            var secondRepository = new StudentPrivateRepository(
                dataDirectory,
                credentials,
                new OgmaLibrary.Infrastructure.Security.AesGcmAtRestEncryptionService());
            StudentReadingProgress? persisted = await secondRepository.GetReadingProgressAsync(
                profileId,
                "host-1",
                "book-1");

            Assert.Equal(progress, persisted);
            Assert.True(File.Exists(firstRepository.GetPrivateDatabasePath(profileId)));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task StudentPrivateRepository_IsolatesAnnotationsByProfile_AndKeepsTombstones()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            var repository = new StudentPrivateRepository(dataDirectory);
            Guid firstProfile = Guid.NewGuid();
            Guid secondProfile = Guid.NewGuid();
            var annotation = new StudentAnnotation(
                "annotation-1",
                "host-1",
                "book-1",
                7,
                "Highlight",
                "#ffd166",
                "Important idea",
                Now,
                Now);

            await repository.SaveAnnotationAsync(firstProfile, annotation);
            await repository.SoftDeleteAnnotationAsync(firstProfile, annotation.Id, Now.AddMinutes(1));

            IReadOnlyList<StudentAnnotation> firstVisible = await repository.ListAnnotationsAsync(
                firstProfile,
                "host-1",
                "book-1");
            IReadOnlyList<StudentAnnotation> firstWithTombstones = await repository.ListAnnotationsAsync(
                firstProfile,
                "host-1",
                "book-1",
                includeDeleted: true);
            IReadOnlyList<StudentAnnotation> second = await repository.ListAnnotationsAsync(
                secondProfile,
                "host-1",
                "book-1",
                includeDeleted: true);

            Assert.Empty(firstVisible);
            StudentAnnotation tombstone = Assert.Single(firstWithTombstones);
            Assert.True(tombstone.IsDeleted);
            Assert.Equal(Now.AddMinutes(1), tombstone.UpdatedUtc);
            Assert.Empty(second);
            Assert.NotEqual(
                repository.GetPrivateDatabasePath(firstProfile),
                repository.GetPrivateDatabasePath(secondProfile));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task StudentPrivateRepository_PersistsBookmarksAiHistory_AndSyncState()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            var repository = new StudentPrivateRepository(dataDirectory);
            Guid profileId = Guid.NewGuid();
            var bookmark = new StudentBookmark(
                "bookmark-1",
                "host-1",
                "book-1",
                9,
                "Exam quote",
                Now,
                Now);
            var history = new StudentAiHistoryEntry(
                "ai-1",
                "host-1",
                "What does this chapter argue?",
                "The chapter argues for local-first reading.",
                "student",
                Now);
            var syncState = new StudentSyncState("host-1", Now, "abc123", 2);

            await repository.SaveBookmarkAsync(profileId, bookmark);
            await repository.SaveAiHistoryAsync(profileId, history);
            await repository.SaveSyncStateAsync(profileId, syncState);

            IReadOnlyList<StudentBookmark> bookmarks = await repository.ListBookmarksAsync(
                profileId,
                "host-1",
                "book-1");
            IReadOnlyList<StudentAiHistoryEntry> historyEntries = await repository.ListAiHistoryAsync(
                profileId,
                "host-1");
            StudentSyncState? persistedSync = await repository.GetSyncStateAsync(profileId, "host-1");

            Assert.Equal(bookmark, Assert.Single(bookmarks));
            Assert.Equal(history, Assert.Single(historyEntries));
            Assert.Equal(syncState, persistedSync);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task StudentPrivateRepository_DeleteAiHistory_ClearsOwnHostHistoryOnly()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            var repository = new StudentPrivateRepository(dataDirectory);
            Guid profileId = Guid.NewGuid();
            Guid otherProfileId = Guid.NewGuid();
            await repository.SaveAiHistoryAsync(
                profileId,
                new StudentAiHistoryEntry("ai-1", "host-1", "Ask one", "Answer one", "metadata", Now));
            await repository.SaveAiHistoryAsync(
                profileId,
                new StudentAiHistoryEntry("ai-2", "host-2", "Ask two", "Answer two", "metadata", Now));
            await repository.SaveAiHistoryAsync(
                otherProfileId,
                new StudentAiHistoryEntry("ai-3", "host-1", "Ask three", "Answer three", "metadata", Now));

            int deleted = await repository.DeleteAiHistoryAsync(profileId, "host-1");

            Assert.Equal(1, deleted);
            Assert.Empty(await repository.ListAiHistoryAsync(profileId, "host-1", includeDeleted: true));
            Assert.Equal("ai-2", Assert.Single(await repository.ListAiHistoryAsync(profileId, "host-2")).Id);
            Assert.Equal("ai-3", Assert.Single(await repository.ListAiHistoryAsync(otherProfileId, "host-1")).Id);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task StudentPrivateRepository_PersistsAnnotationConflicts()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            var credentials = new TestCredentialStore();
            var firstRepository = new StudentPrivateRepository(
                dataDirectory,
                credentials,
                new OgmaLibrary.Infrastructure.Security.AesGcmAtRestEncryptionService());
            Guid profileId = Guid.NewGuid();
            var local = new StudentAnnotation(
                "annotation-1",
                "host-1",
                "book-1",
                9,
                "Note",
                null,
                "Local note",
                Now,
                Now);
            var conflict = new StudentAnnotationConflict(
                "host-1",
                local,
                local with { Body = "Server note" },
                Now.AddMinutes(1));

            await firstRepository.SaveAnnotationConflictAsync(profileId, conflict);

            var secondRepository = new StudentPrivateRepository(
                dataDirectory,
                credentials,
                new OgmaLibrary.Infrastructure.Security.AesGcmAtRestEncryptionService());
            StudentAnnotationConflict persisted = Assert.Single(
                await secondRepository.ListAnnotationConflictsAsync(profileId, "host-1"));
            await secondRepository.DeleteAnnotationConflictAsync(profileId, "host-1", "annotation-1");
            IReadOnlyList<StudentAnnotationConflict> afterDelete =
                await secondRepository.ListAnnotationConflictsAsync(profileId, "host-1");

            Assert.Equal(conflict, persisted);
            Assert.Empty(afterDelete);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task StudentPrivateRepository_EncryptsSensitiveFieldsInRawDatabase()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            var credentials = new TestCredentialStore();
            var repository = new StudentPrivateRepository(
                dataDirectory,
                credentials,
                new OgmaLibrary.Infrastructure.Security.AesGcmAtRestEncryptionService());
            Guid profileId = Guid.NewGuid();
            var annotation = new StudentAnnotation(
                "annotation-raw",
                "host-raw",
                "book-raw",
                3,
                "Note",
                null,
                "Unique private classroom annotation text",
                Now,
                Now);
            var history = new StudentAiHistoryEntry(
                "history-raw",
                "host-raw",
                "Unique private student question",
                "Unique private student response",
                "metadata",
                Now);

            await repository.SaveAnnotationAsync(profileId, annotation);
            await repository.SaveAiHistoryAsync(profileId, history);

            byte[] raw = await File.ReadAllBytesAsync(repository.GetPrivateDatabasePath(profileId));
            string rawText = System.Text.Encoding.UTF8.GetString(raw);

            Assert.DoesNotContain(annotation.Body!, rawText, StringComparison.Ordinal);
            Assert.DoesNotContain(history.Query, rawText, StringComparison.Ordinal);
            Assert.DoesNotContain(history.ResponseSummary!, rawText, StringComparison.Ordinal);
            Assert.Equal(annotation, Assert.Single(await repository.ListAnnotationsAsync(profileId, "host-raw", "book-raw")));
            Assert.Equal(history, Assert.Single(await repository.ListAiHistoryAsync(profileId, "host-raw")));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    private sealed class TestCredentialStore : IClassroomCredentialStore
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public Task SaveSecretAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.TryGetValue(key, out string? value) ? value : null);

        public Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task StudentPrivateRepository_ListsHostWideSnapshotRows_WithTombstones()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            var repository = new StudentPrivateRepository(dataDirectory);
            Guid profileId = Guid.NewGuid();
            Guid otherProfileId = Guid.NewGuid();
            await repository.SaveReadingProgressAsync(
                profileId,
                new StudentReadingProgress("host-1", "book-2", 12, 4.5, Now));
            await repository.SaveReadingProgressAsync(
                profileId,
                new StudentReadingProgress("host-2", "book-9", 1, 0, Now));
            await repository.SaveAnnotationAsync(
                profileId,
                new StudentAnnotation("annotation-1", "host-1", "book-1", 3, "Highlight", "#ffd166", "Keep", Now, Now));
            await repository.SaveAnnotationAsync(
                profileId,
                new StudentAnnotation("annotation-2", "host-1", "book-2", 4, "Note", null, "Deleted", Now, Now, IsDeleted: true));
            await repository.SaveAnnotationAsync(
                otherProfileId,
                new StudentAnnotation("annotation-3", "host-1", "book-3", 5, "Note", null, "Other profile", Now, Now));
            await repository.SaveBookmarkAsync(
                profileId,
                new StudentBookmark("bookmark-1", "host-1", "book-1", 2, "Visible", Now, Now));
            await repository.SaveBookmarkAsync(
                profileId,
                new StudentBookmark("bookmark-2", "host-1", "book-2", 7, "Deleted", Now, Now, IsDeleted: true));

            IReadOnlyList<StudentReadingProgress> progress = await repository.ListReadingProgressAsync(
                profileId,
                "host-1");
            IReadOnlyList<StudentAnnotation> visibleAnnotations = await repository.ListAnnotationsForHostAsync(
                profileId,
                "host-1");
            IReadOnlyList<StudentAnnotation> allAnnotations = await repository.ListAnnotationsForHostAsync(
                profileId,
                "host-1",
                includeDeleted: true);
            IReadOnlyList<StudentBookmark> allBookmarks = await repository.ListBookmarksForHostAsync(
                profileId,
                "host-1",
                includeDeleted: true);

            Assert.Equal("book-2", Assert.Single(progress).BookId);
            Assert.Equal("annotation-1", Assert.Single(visibleAnnotations).Id);
            Assert.Equal(["annotation-1", "annotation-2"], allAnnotations.Select(annotation => annotation.Id).ToArray());
            Assert.Contains(allAnnotations, annotation => annotation is { Id: "annotation-2", IsDeleted: true });
            Assert.Equal(["bookmark-1", "bookmark-2"], allBookmarks.Select(bookmark => bookmark.Id).ToArray());
            Assert.DoesNotContain(allAnnotations, annotation => annotation.Id == "annotation-3");
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    private static string CreateTempDirectory()
    {
        string dataDirectory = Path.Combine(Path.GetTempPath(), $"ogma-student-private-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataDirectory);
        return dataDirectory;
    }

    private static void CleanupTempDirectory(string dataDirectory)
    {
        if (Directory.Exists(dataDirectory))
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }
}
