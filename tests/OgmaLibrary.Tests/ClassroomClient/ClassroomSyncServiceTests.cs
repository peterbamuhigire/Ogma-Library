using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Infrastructure.ClassroomClient;

namespace OgmaLibrary.Tests.ClassroomClient;

/// <summary>Phase 17 manual private-state sync orchestration tests.</summary>
public sealed class ClassroomSyncServiceTests
{
    private const string Fingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly ClassroomJoinRequest JoinRequest = new("192.168.1.13", 7473, Fingerprint);
    private static readonly ClassroomHostConnection Connection = new(JoinRequest, "session-token", DateTimeOffset.UtcNow);

    [Fact]
    public async Task ClassroomSyncService_SyncNow_UploadsEncryptedSnapshotAndRecordsState()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            Guid profileId = Guid.NewGuid();
            var profile = new ClassroomProfile(profileId, "Ada", ClassroomRole.Student, IsGuest: false);
            var repository = new StudentPrivateRepository(dataDirectory);
            string hostId = HostTrustService.CreateHostKey(JoinRequest);
            await repository.SaveReadingProgressAsync(
                profileId,
                new StudentReadingProgress(hostId, "book-1", 5, 120.5, DateTimeOffset.UtcNow));
            await repository.SaveAnnotationAsync(
                profileId,
                new StudentAnnotation(
                    "annotation-1",
                    hostId,
                    "book-1",
                    5,
                    "Highlight",
                    "#ffd166",
                    "Important idea",
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow));
            await repository.SaveBookmarkAsync(
                profileId,
                new StudentBookmark(
                    "bookmark-1",
                    hostId,
                    "book-1",
                    5,
                    "Exam quote",
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow));
            await repository.SaveAiHistoryAsync(
                profileId,
                new StudentAiHistoryEntry(
                    "ai-1",
                    hostId,
                    "Explain the theorem",
                    "Local-first explanation",
                    "offline",
                    DateTimeOffset.UtcNow));

            var codec = new ClassroomSyncBlobCodec();
            var host = new RecordingHostClient();
            var service = new ClassroomSyncService(
                new FixedProfileService(profile),
                new FixedConnectionService(Connection),
                repository,
                codec,
                host);

            ClassroomSyncStatus status = await service.SyncNowAsync();

            Assert.True(status.IsEnabled);
            Assert.Null(status.ErrorMessage);
            Assert.NotNull(host.Uploaded);
            string encryptedText = Encoding.UTF8.GetString(host.Uploaded.Content);
            Assert.DoesNotContain("Important idea", encryptedText, StringComparison.Ordinal);
            Assert.DoesNotContain("Explain the theorem", encryptedText, StringComparison.Ordinal);
            ClassroomSyncSnapshot snapshot = codec.Decode(host.Uploaded, Connection.SessionToken);
            Assert.Equal(profileId, snapshot.ProfileId);
            Assert.Equal(hostId, snapshot.HostId);
            Assert.Single(snapshot.ReadingProgress);
            Assert.Single(snapshot.Annotations);
            Assert.Single(snapshot.Bookmarks);
            Assert.Single(snapshot.AiHistory);

            StudentSyncState? state = await repository.GetSyncStateAsync(profileId, hostId);
            Assert.NotNull(state);
            Assert.Equal(status.LastSyncedUtc, state.LastSyncedUtc);
            Assert.Equal(
                Convert.ToHexString(SHA256.HashData(host.Uploaded.Content)).ToLowerInvariant(),
                state.LastSyncBlobHash);
            Assert.Equal(0, state.ConflictCount);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task ClassroomSyncService_GetStatus_ReturnsSavedSyncStateForActiveConnection()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            Guid profileId = Guid.NewGuid();
            var profile = new ClassroomProfile(profileId, "Ada", ClassroomRole.Student, IsGuest: false);
            var repository = new StudentPrivateRepository(dataDirectory);
            string hostId = HostTrustService.CreateHostKey(JoinRequest);
            DateTimeOffset syncedUtc = new(2026, 6, 2, 14, 30, 0, TimeSpan.Zero);
            await repository.SaveSyncStateAsync(profileId, new StudentSyncState(hostId, syncedUtc, "abc123", 2));
            var service = new ClassroomSyncService(
                new FixedProfileService(profile),
                new FixedConnectionService(Connection),
                repository,
                new ClassroomSyncBlobCodec(),
                new RecordingHostClient());

            ClassroomSyncStatus status = await service.GetStatusAsync();

            Assert.True(status.IsEnabled);
            Assert.False(status.IsRunning);
            Assert.Equal(syncedUtc, status.LastSyncedUtc);
            Assert.Equal(2, status.ConflictCount);
            Assert.Null(status.ErrorMessage);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task ClassroomSyncService_SyncNow_DownloadsRemoteSnapshotAndAppliesLastWriteWins()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            Guid profileId = Guid.NewGuid();
            var profile = new ClassroomProfile(profileId, "Ada", ClassroomRole.Student, IsGuest: false);
            var repository = new StudentPrivateRepository(dataDirectory);
            string hostId = HostTrustService.CreateHostKey(JoinRequest);
            DateTimeOffset createdUtc = new(2026, 6, 2, 8, 0, 0, TimeSpan.Zero);
            await repository.SaveAnnotationAsync(
                profileId,
                new StudentAnnotation(
                    "annotation-1",
                    hostId,
                    "book-1",
                    4,
                    "Note",
                    null,
                    "Local older body",
                    createdUtc,
                    createdUtc.AddMinutes(1)));
            var remoteAnnotation = new StudentAnnotation(
                "annotation-1",
                hostId,
                "book-1",
                4,
                "Note",
                null,
                "Server newer body",
                createdUtc,
                createdUtc.AddMinutes(5));
            var codec = new ClassroomSyncBlobCodec();
            var host = new RecordingHostClient
            {
                RemoteBlob = codec.Encode(
                    new ClassroomSyncSnapshot(
                        profileId,
                        hostId,
                        createdUtc.AddMinutes(5),
                        [],
                        [remoteAnnotation],
                        [],
                        [],
                        null),
                    Connection.SessionToken),
            };
            var service = new ClassroomSyncService(
                new FixedProfileService(profile),
                new FixedConnectionService(Connection),
                repository,
                codec,
                host);

            ClassroomSyncStatus status = await service.SyncNowAsync();

            IReadOnlyList<StudentAnnotation> annotations = await repository.ListAnnotationsForHostAsync(
                profileId,
                hostId,
                includeDeleted: true);
            StudentAnnotation merged = Assert.Single(annotations);
            Assert.Equal("Server newer body", merged.Body);
            Assert.Equal(0, status.ConflictCount);
            ClassroomSyncSnapshot uploaded = codec.Decode(host.Uploaded!, Connection.SessionToken);
            Assert.Equal("Server newer body", Assert.Single(uploaded.Annotations).Body);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task ClassroomSyncService_SyncNow_DetectsConflictAndKeepsLocalUntilUiResolution()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            Guid profileId = Guid.NewGuid();
            var profile = new ClassroomProfile(profileId, "Ada", ClassroomRole.Student, IsGuest: false);
            var repository = new StudentPrivateRepository(dataDirectory);
            string hostId = HostTrustService.CreateHostKey(JoinRequest);
            DateTimeOffset updatedUtc = new(2026, 6, 2, 8, 0, 0, TimeSpan.Zero);
            var localAnnotation = new StudentAnnotation(
                "annotation-1",
                hostId,
                "book-1",
                4,
                "Note",
                null,
                "Keep local body",
                updatedUtc.AddMinutes(-1),
                updatedUtc);
            await repository.SaveAnnotationAsync(profileId, localAnnotation);
            var remoteAnnotation = localAnnotation with { Body = "Server conflicting body" };
            var codec = new ClassroomSyncBlobCodec();
            var host = new RecordingHostClient
            {
                RemoteBlob = codec.Encode(
                    new ClassroomSyncSnapshot(
                        profileId,
                        hostId,
                        updatedUtc,
                        [],
                        [remoteAnnotation],
                        [],
                        [],
                        null),
                    Connection.SessionToken),
            };
            var service = new ClassroomSyncService(
                new FixedProfileService(profile),
                new FixedConnectionService(Connection),
                repository,
                codec,
                host);

            ClassroomSyncStatus status = await service.SyncNowAsync();

            StudentAnnotation persisted = Assert.Single(await repository.ListAnnotationsForHostAsync(
                profileId,
                hostId,
                includeDeleted: true));
            StudentSyncState? state = await repository.GetSyncStateAsync(profileId, hostId);
            ClassroomSyncSnapshot uploaded = codec.Decode(host.Uploaded!, Connection.SessionToken);
            StudentAnnotationConflict conflict = Assert.Single(
                await repository.ListAnnotationConflictsAsync(profileId, hostId));
            Assert.Equal("Keep local body", persisted.Body);
            Assert.Equal("Keep local body", conflict.LocalAnnotation.Body);
            Assert.Equal("Server conflicting body", conflict.RemoteAnnotation.Body);
            Assert.Equal(1, status.ConflictCount);
            Assert.Equal(1, state!.ConflictCount);
            Assert.Equal("Keep local body", Assert.Single(uploaded.Annotations).Body);

            ClassroomSyncStatus resolvedStatus = await service.ResolveAnnotationConflictAsync(
                "annotation-1",
                ClassroomSyncConflictResolution.KeepServer);

            StudentAnnotation resolved = Assert.Single(await repository.ListAnnotationsForHostAsync(
                profileId,
                hostId,
                includeDeleted: true));
            StudentSyncState? resolvedState = await repository.GetSyncStateAsync(profileId, hostId);
            Assert.Equal("Server conflicting body", resolved.Body);
            Assert.Empty(await repository.ListAnnotationConflictsAsync(profileId, hostId));
            Assert.Equal(0, resolvedStatus.ConflictCount);
            Assert.Equal(0, resolvedState!.ConflictCount);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task ClassroomSyncService_ResolveConflictKeepLocal_ClearsPendingConflict()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            Guid profileId = Guid.NewGuid();
            var profile = new ClassroomProfile(profileId, "Ada", ClassroomRole.Student, IsGuest: false);
            var repository = new StudentPrivateRepository(dataDirectory);
            string hostId = HostTrustService.CreateHostKey(JoinRequest);
            DateTimeOffset updatedUtc = new(2026, 6, 2, 8, 0, 0, TimeSpan.Zero);
            var localAnnotation = new StudentAnnotation(
                "annotation-1",
                hostId,
                "book-1",
                4,
                "Note",
                null,
                "Keep local body",
                updatedUtc.AddMinutes(-1),
                updatedUtc);
            await repository.SaveAnnotationAsync(profileId, localAnnotation);
            await repository.SaveAnnotationConflictAsync(
                profileId,
                new StudentAnnotationConflict(
                    hostId,
                    localAnnotation,
                    localAnnotation with { Body = "Server conflicting body" },
                    updatedUtc));
            await repository.SaveSyncStateAsync(profileId, new StudentSyncState(hostId, updatedUtc, "abc123", 1));
            var service = new ClassroomSyncService(
                new FixedProfileService(profile),
                new FixedConnectionService(Connection),
                repository,
                new ClassroomSyncBlobCodec(),
                new RecordingHostClient());

            ClassroomSyncStatus status = await service.ResolveAnnotationConflictAsync(
                "annotation-1",
                ClassroomSyncConflictResolution.KeepLocal);

            StudentAnnotation persisted = Assert.Single(await repository.ListAnnotationsForHostAsync(
                profileId,
                hostId,
                includeDeleted: true));
            StudentSyncState? state = await repository.GetSyncStateAsync(profileId, hostId);
            Assert.Equal("Keep local body", persisted.Body);
            Assert.Empty(await repository.ListAnnotationConflictsAsync(profileId, hostId));
            Assert.Equal(0, status.ConflictCount);
            Assert.Equal(0, state!.ConflictCount);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task ClassroomSyncService_SyncNow_RequiresPersistentProfileAndActiveConnection()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            var repository = new StudentPrivateRepository(dataDirectory);
            var guest = new ClassroomProfile(Guid.NewGuid(), "Guest", ClassroomRole.Guest, IsGuest: true);
            var host = new RecordingHostClient();
            var guestService = new ClassroomSyncService(
                new FixedProfileService(guest),
                new FixedConnectionService(Connection),
                repository,
                new ClassroomSyncBlobCodec(),
                host);
            var disconnectedService = new ClassroomSyncService(
                new FixedProfileService(new ClassroomProfile(Guid.NewGuid(), "Ada", ClassroomRole.Student, IsGuest: false)),
                new FixedConnectionService(null),
                repository,
                new ClassroomSyncBlobCodec(),
                host);

            ClassroomSyncStatus guestStatus = await guestService.SyncNowAsync();
            ClassroomSyncStatus disconnectedStatus = await disconnectedService.SyncNowAsync();

            Assert.False(guestStatus.IsEnabled);
            Assert.Contains("Guest", guestStatus.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.False(disconnectedStatus.IsEnabled);
            Assert.Contains("Connect", disconnectedStatus.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, host.UploadCalls);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public void ClassroomSyncService_IsRegisteredByClassroomClientServices()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            using ServiceProvider provider = new ServiceCollection()
                .AddClassroomClientServices(dataDirectory)
                .BuildServiceProvider();

            Assert.IsType<ClassroomSyncService>(provider.GetRequiredService<ISyncService>());
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    private static string CreateTempDirectory()
    {
        string dataDirectory = Path.Combine(Path.GetTempPath(), $"ogma-classroom-sync-{Guid.NewGuid():N}");
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

    private sealed class FixedProfileService(ClassroomProfile? activeProfile) : IProfileService
    {
        public Task<ClassroomProfile> CreateAsync(
            CreateClassroomProfileRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ClassroomProfile> CreateGuestSessionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task ClearGuestSessionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ClassroomProfile>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ClassroomProfile>>(
                activeProfile is null ? [] : [activeProfile]);

        public Task SelectAsync(Guid profileId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ClassroomProfile?> GetActiveAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(activeProfile);

        public Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task StoreSessionTokenAsync(
            Guid profileId,
            string sessionToken,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string?> GetSessionTokenAsync(Guid profileId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task ClearSessionTokenAsync(Guid profileId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FixedConnectionService(ClassroomHostConnection? connection) : IClassroomHostConnectionService
    {
        public Task<ClassroomHostConnection?> GetActiveAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(connection);

        public Task SetActiveAsync(
            ClassroomHostConnection connection,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task ClearAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingHostClient : ILibraryHostClient
    {
        public EncryptedClassroomSyncBlob? RemoteBlob { get; init; }

        public EncryptedClassroomSyncBlob? Uploaded { get; private set; }

        public int UploadCalls { get; private set; }

        public Task<LibraryHostHealth> GetHealthAsync(
            ClassroomJoinRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostSession> IssueSessionAsync(
            ClassroomJoinRequest request,
            Guid profileId,
            ClassroomRole role,
            TimeSpan lifetime,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostCataloguePage> GetCataloguePageAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            LibraryHostCatalogueQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostBookDetail> GetBookAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            string bookId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostSearchPage> SearchCatalogueAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            LibraryHostSearchQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostResource> GetPageRenderAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            string bookId,
            int pageNumber,
            int widthPx,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostResource> GetFileStreamAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            string bookId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostResource> GetAssetAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            string assetUrl,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UploadProfileSyncBlobAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            EncryptedClassroomSyncBlob blob,
            CancellationToken cancellationToken = default)
        {
            UploadCalls++;
            Uploaded = blob;
            return Task.CompletedTask;
        }

        public Task<EncryptedClassroomSyncBlob?> DownloadProfileSyncBlobAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(RemoteBlob ?? Uploaded);
    }
}
