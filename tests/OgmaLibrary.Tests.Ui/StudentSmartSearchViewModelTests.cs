using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.Views.Classroom;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

public sealed class StudentSmartSearchViewModelTests
{
    private static readonly Guid ProfileId = Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee");
    private static readonly ClassroomJoinRequest JoinRequest = new(
        "192.168.1.13",
        7473,
        new string('a', 64),
        DisplayName: "Classroom Host");

    [Fact]
    public async Task StudentSmartSearchViewModel_PreviewAndConfirm_UpdatesAnswerCitationsAndUsage()
    {
        var host = new RecordingLibraryHostClient();
        var privateRepository = new RecordingStudentPrivateRepository();
        var vm = new StudentSmartSearchViewModel(
            new FixedConnectionService(new ClassroomHostConnection(JoinRequest, "session-token", DateTimeOffset.UtcNow)),
            host,
            new FixedProfileService(new ClassroomProfile(ProfileId, "Amina", ClassroomRole.Student, IsGuest: false)),
            privateRepository,
            new InMemoryLocalizationService())
        {
            Query = "How do I learn linear equations?",
        };

        await vm.RequestPreviewAsync();

        Assert.True(vm.HasPreview);
        Assert.True(vm.CanConfirmSearch);
        Assert.Contains(vm.MetadataPreviewRows, row => row.Key == "books.0.title" && row.Value == "Algebra I");
        Assert.Equal(ProfileId, host.PreviewRequest!.ProfileId);
        Assert.False(host.PreviewRequest.ConfirmedPayloadPreview);

        await vm.ConfirmSearchAsync();

        Assert.False(vm.HasPreview);
        Assert.True(vm.HasAnswer);
        Assert.Contains("Algebra I", vm.Answer, StringComparison.Ordinal);
        SmartSearchCitationRow citation = Assert.Single(vm.Citations);
        Assert.Equal("Algebra I", citation.Title);
        Assert.Equal("Page 12", citation.Location);
        Assert.Equal(42, vm.SessionTokensUsed);
        Assert.Equal("$0.0125", vm.EstimatedCostText.Replace("Estimated cost: ", string.Empty, StringComparison.Ordinal));
        Assert.True(host.SearchRequest!.ConfirmedPayloadPreview);
        StudentAiHistoryEntry history = Assert.Single(privateRepository.SavedHistory);
        Assert.Equal(ProfileId, privateRepository.SavedProfileId);
        Assert.Equal(vm.Query, history.Query);
        Assert.Contains("Algebra I", history.ResponseSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void StudentSmartSearchViewModel_LocalizedSurface_RaisesCultureChanges()
    {
        var localization = new InMemoryLocalizationService();
        var vm = new StudentSmartSearchViewModel(
            new FixedConnectionService(null),
            new RecordingLibraryHostClient(),
            new FixedProfileService(new ClassroomProfile(ProfileId, "Amina", ClassroomRole.Student, IsGuest: false)),
            new RecordingStudentPrivateRepository(),
            localization);
        var changed = new HashSet<string>(StringComparer.Ordinal);
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not null)
            {
                changed.Add(args.PropertyName);
            }
        };

        string englishTitle = vm.Title;
        string englishWatermark = vm.QueryWatermark;
        localization.SetCulture("fr");

        Assert.NotEqual(englishTitle, vm.Title);
        Assert.NotEqual(englishWatermark, vm.QueryWatermark);
        Assert.Contains(nameof(vm.Title), changed);
        Assert.Contains(nameof(vm.QueryWatermark), changed);

        localization.SetCulture("qps-ploc");
        Assert.StartsWith("[!!", vm.Title, StringComparison.Ordinal);
        Assert.StartsWith("[!!", vm.GroundingNotice, StringComparison.Ordinal);
        vm.Dispose();
    }

    [Fact]
    public async Task StudentSmartSearchViewModel_NoActiveConnection_ShowsActionableStatus()
    {
        var vm = new StudentSmartSearchViewModel(
            new FixedConnectionService(null),
            new RecordingLibraryHostClient(),
            new FixedProfileService(new ClassroomProfile(ProfileId, "Amina", ClassroomRole.Student, IsGuest: false)),
            new RecordingStudentPrivateRepository(),
            new InMemoryLocalizationService())
        {
            Query = "What should I read?",
        };

        await vm.RequestPreviewAsync();

        Assert.Contains("Connect to a classroom Host", vm.StatusText, StringComparison.Ordinal);
        Assert.False(vm.HasPreview);
    }

    [Fact]
    public async Task StudentSmartSearchViewModel_DeleteHistory_UsesActiveHostAndProfileScope()
    {
        var privateRepository = new RecordingStudentPrivateRepository { DeleteCount = 3 };
        var vm = new StudentSmartSearchViewModel(
            new FixedConnectionService(new ClassroomHostConnection(JoinRequest, "session-token", DateTimeOffset.UtcNow)),
            new RecordingLibraryHostClient(),
            new FixedProfileService(new ClassroomProfile(ProfileId, "Amina", ClassroomRole.Student, IsGuest: false)),
            privateRepository,
            new InMemoryLocalizationService());

        await vm.DeleteHistoryAsync();

        Assert.Equal(ProfileId, privateRepository.DeletedProfileId);
        Assert.Contains(JoinRequest.Address, privateRepository.DeletedHostId, StringComparison.Ordinal);
        Assert.Contains("Deleted 3", vm.StatusText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void StudentSmartSearchView_RendersPrimaryWorkflowText()
    {
        var vm = new StudentSmartSearchViewModel(
            new FixedConnectionService(new ClassroomHostConnection(JoinRequest, "session-token", DateTimeOffset.UtcNow)),
            new RecordingLibraryHostClient(),
            new FixedProfileService(new ClassroomProfile(ProfileId, "Amina", ClassroomRole.Student, IsGuest: false)),
            new RecordingStudentPrivateRepository(),
            new InMemoryLocalizationService())
        {
            Query = "What should I read?",
        };
        var window = new Window
        {
            Width = 1120,
            Height = 760,
            Content = new StudentSmartSearchView { DataContext = vm },
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        List<string?> visibleText = window.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(block => block.Text)
            .ToList();
        Assert.Contains("AI Smart Search", visibleText);
        Assert.Contains("Preview", visibleText);
        Assert.Contains("Answer", visibleText);
        Assert.Contains("Delete history", visibleText);
        Assert.Contains("Session tokens used: 0", visibleText);
    }

    private sealed class FixedConnectionService(ClassroomHostConnection? connection) : IClassroomHostConnectionService
    {
        public Task<ClassroomHostConnection?> GetActiveAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(connection);

        public Task SetActiveAsync(
            ClassroomHostConnection connection,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ClearAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FixedProfileService(ClassroomProfile? profile) : IProfileService
    {
        public Task<ClassroomProfile> CreateAsync(
            CreateClassroomProfileRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(profile ?? new ClassroomProfile(Guid.NewGuid(), request.DisplayName, request.Role, IsGuest: false));

        public Task<ClassroomProfile> CreateGuestSessionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ClassroomProfile(Guid.NewGuid(), "Guest", ClassroomRole.Guest, IsGuest: true));

        public Task ClearGuestSessionAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<ClassroomProfile>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ClassroomProfile>>(profile is null ? [] : [profile]);

        public Task SelectAsync(Guid profileId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<ClassroomProfile?> GetActiveAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(profile);

        public Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task StoreSessionTokenAsync(
            Guid profileId,
            string sessionToken,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string?> GetSessionTokenAsync(Guid profileId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>("session-token");

        public Task ClearSessionTokenAsync(Guid profileId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class RecordingLibraryHostClient : ILibraryHostClient
    {
        public LibraryHostAiSearchRequest? PreviewRequest { get; private set; }

        public LibraryHostAiSearchRequest? SearchRequest { get; private set; }

        public Task<LibraryHostAiPayloadPreview> PreviewAiSearchAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            LibraryHostAiSearchRequest query,
            CancellationToken cancellationToken = default)
        {
            PreviewRequest = query;
            return Task.FromResult(new LibraryHostAiPayloadPreview(
                AiPrivacyTier.MetadataOnly,
                new Dictionary<string, string>
                {
                    ["books.0.title"] = "Algebra I",
                },
                EstimatedCharacters: 128,
                RequiresConfirmation: true));
        }

        public Task<LibraryHostAiSearchResult> SearchAiAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            LibraryHostAiSearchRequest query,
            CancellationToken cancellationToken = default)
        {
            SearchRequest = query;
            return Task.FromResult(new LibraryHostAiSearchResult(
                "Use Algebra I for linear equations.",
                [new LibraryHostAiCitation("book-1", "Algebra I", 12)],
                TokensUsed: 42,
                EstimatedCostUsd: 0.0125m,
                WasProviderCalled: true));
        }

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
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EncryptedClassroomSyncBlob?> DownloadProfileSyncBlobAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingStudentPrivateRepository : IStudentPrivateRepository
    {
        public List<StudentAiHistoryEntry> SavedHistory { get; } = [];

        public Guid SavedProfileId { get; private set; }

        public Guid DeletedProfileId { get; private set; }

        public string DeletedHostId { get; private set; } = string.Empty;

        public int DeleteCount { get; init; }

        public string GetPrivateDatabasePath(Guid profileId) => "private.db";

        public Task EnsureCreatedAsync(Guid profileId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<StudentReadingProgress?> GetReadingProgressAsync(
            Guid profileId,
            string hostId,
            string bookId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<StudentReadingProgress>> ListReadingProgressAsync(
            Guid profileId,
            string hostId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveReadingProgressAsync(
            Guid profileId,
            StudentReadingProgress progress,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<StudentAnnotation>> ListAnnotationsAsync(
            Guid profileId,
            string hostId,
            string bookId,
            bool includeDeleted = false,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<StudentAnnotation>> ListAnnotationsForHostAsync(
            Guid profileId,
            string hostId,
            bool includeDeleted = false,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveAnnotationAsync(
            Guid profileId,
            StudentAnnotation annotation,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<StudentAnnotationConflict>> ListAnnotationConflictsAsync(
            Guid profileId,
            string hostId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveAnnotationConflictAsync(
            Guid profileId,
            StudentAnnotationConflict conflict,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteAnnotationConflictAsync(
            Guid profileId,
            string hostId,
            string annotationId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SoftDeleteAnnotationAsync(
            Guid profileId,
            string annotationId,
            DateTimeOffset updatedUtc,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<StudentBookmark>> ListBookmarksAsync(
            Guid profileId,
            string hostId,
            string bookId,
            bool includeDeleted = false,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<StudentBookmark>> ListBookmarksForHostAsync(
            Guid profileId,
            string hostId,
            bool includeDeleted = false,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveBookmarkAsync(
            Guid profileId,
            StudentBookmark bookmark,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SoftDeleteBookmarkAsync(
            Guid profileId,
            string bookmarkId,
            DateTimeOffset updatedUtc,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<StudentAiHistoryEntry>> ListAiHistoryAsync(
            Guid profileId,
            string hostId,
            bool includeDeleted = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StudentAiHistoryEntry>>(SavedHistory);

        public Task SaveAiHistoryAsync(
            Guid profileId,
            StudentAiHistoryEntry entry,
            CancellationToken cancellationToken = default)
        {
            SavedProfileId = profileId;
            SavedHistory.Add(entry);
            return Task.CompletedTask;
        }

        public Task<int> DeleteAiHistoryAsync(
            Guid profileId,
            string hostId,
            CancellationToken cancellationToken = default)
        {
            DeletedProfileId = profileId;
            DeletedHostId = hostId;
            return Task.FromResult(DeleteCount);
        }

        public Task<StudentSyncState?> GetSyncStateAsync(
            Guid profileId,
            string hostId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveSyncStateAsync(
            Guid profileId,
            StudentSyncState state,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
