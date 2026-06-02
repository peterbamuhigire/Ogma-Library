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
            var firstRepository = new StudentPrivateRepository(dataDirectory);
            var progress = new StudentReadingProgress("host-1", "book-1", 42, 12.5, Now);

            await firstRepository.SaveReadingProgressAsync(profileId, progress);

            var secondRepository = new StudentPrivateRepository(dataDirectory);
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
