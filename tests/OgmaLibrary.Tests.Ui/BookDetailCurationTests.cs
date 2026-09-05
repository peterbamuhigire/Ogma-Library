using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.Views.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>UI-model proof for Phase 20 book-detail curation controls.</summary>
public sealed class BookDetailCurationTests
{
    [AvaloniaFact]
    public async Task CurationActions_PersistAndRefreshTheDetailProjection()
    {
        const string bookId = "PHASE20-DETAIL-BOOK";
        var readModel = new MutableReadModel(CreateProjection(bookId));
        var curation = new RecordingCurationService(readModel);
        var viewModel = new BookDetailViewModel(
            readModel,
            new NoOpReaderNavigation(),
            new InMemoryLocalizationService(),
            curation: curation);

        await viewModel.LoadBookAsync(bookId);
        await viewModel.SetReadingStatusAsync(ReadingStatus.Finished);
        Dispatcher.UIThread.RunJobs();
        await viewModel.SetRatingAsync(5);
        Dispatcher.UIThread.RunJobs();
        await viewModel.ToggleFavouriteAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ReadingStatus.Finished, viewModel.CurrentReadingStatus);
        Assert.Equal(5, viewModel.Rating);
        Assert.True(viewModel.IsFavourite);
        Assert.Equal(3, curation.Calls.Count);
        Assert.Contains(curation.Calls, call => call.Status == ReadingStatus.Finished);
        Assert.Contains(curation.Calls, call => call.Rating == 5);
        Assert.Contains(curation.Calls, call => call.IsFavourite == true);
        Assert.Contains("saved", viewModel.CurationStatusText, StringComparison.OrdinalIgnoreCase);
    }

    [AvaloniaFact]
    public async Task BookDetailView_UsesSafeManifestCoverControl()
    {
        const string relativeCover = ".ogma/covers/detail.png";
        var readModel = new MutableReadModel(CreateProjection("PHASE20-COVER-BOOK") with
        {
            CoverRelativePath = relativeCover,
        });
        var viewModel = new BookDetailViewModel(
            readModel,
            new NoOpReaderNavigation(),
            new InMemoryLocalizationService(),
            assetRootPath: Path.GetTempPath());
        await viewModel.LoadBookAsync("PHASE20-COVER-BOOK");

        var view = new BookDetailView { DataContext = viewModel };
        var window = new Window
        {
            Width = 420,
            Height = 700,
            Content = view,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        CoverImageView cover = Assert.Single(view.GetVisualDescendants().OfType<CoverImageView>());
        Assert.Equal(relativeCover, cover.RelativePath);
        Assert.Equal(Path.GetTempPath(), cover.RootPath);
        window.Close();
    }

    [AvaloniaFact]
    public async Task BookDetailTags_SaveThroughTheCatalogueWriteBoundaryAndRenderEditor()
    {
        const string bookId = "PHASE20-TAGS-BOOK";
        var readModel = new MutableReadModel(CreateProjection(bookId) with
        {
            MetadataFields =
            [
                new MetadataFieldProjection("Tags", "Systems; Reader", "System", 1.0, false),
            ],
        });
        var writeService = new RecordingCatalogueWriteService(readModel);
        var viewModel = new BookDetailViewModel(
            readModel,
            new NoOpReaderNavigation(),
            new InMemoryLocalizationService(),
            catalogueWriteService: writeService);

        await viewModel.LoadBookAsync(bookId);
        viewModel.TagsText = "Systems, reader, Systems";
        await viewModel.SaveTagsAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.True(viewModel.CanEditTags);
        Assert.Equal("reader, Systems", viewModel.TagsText);
        Assert.Equal("Tags saved.", viewModel.TagsStatusText);
        Assert.Single(writeService.Calls);
        Assert.Equal((bookId, "Tags", "Systems; reader"), writeService.Calls[0]);

        var view = new BookDetailView { DataContext = viewModel };
        var window = new Window
        {
            Width = 420,
            Height = 700,
            Content = view,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        TabControl tabs = Assert.Single(view.GetVisualDescendants().OfType<TabControl>());
        tabs.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(view.GetVisualDescendants().OfType<TextBox>(), textBox =>
            textBox.Watermark == "tag, another tag");
        Assert.Contains(view.GetVisualDescendants().OfType<Button>(), button =>
            button.Content is TextBlock text && text.Text == "Save tags");
        window.Close();
    }

    [AvaloniaFact]
    public async Task ReadingHistory_IsLazyBoundedAndLocalizedForTheDetailPanel()
    {
        const string bookId = "PHASE20-HISTORY-BOOK";
        var readModel = new MutableReadModel(CreateProjection(bookId));
        var curation = new RecordingCurationService(readModel)
        {
            HistoryEntries =
            [
                new ReadingStateHistoryEntry(
                    ReadingStatus.Finished,
                    5,
                    true,
                    "finished book",
                    new DateTimeOffset(2026, 9, 4, 10, 30, 0, TimeSpan.Zero)),
            ],
        };
        var viewModel = new BookDetailViewModel(
            readModel,
            new NoOpReaderNavigation(),
            new InMemoryLocalizationService(),
            curation: curation);

        await viewModel.LoadBookAsync(bookId);

        Assert.Empty(viewModel.ReadingHistoryRows);
        Assert.True(viewModel.ShowLoadReadingHistoryButton);
        Assert.Equal(0, curation.HistoryCalls);

        await viewModel.LoadReadingHistoryAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Single(viewModel.ReadingHistoryRows);
        Assert.Contains("Finished", viewModel.ReadingHistoryRows[0], StringComparison.Ordinal);
        Assert.Contains("finished book", viewModel.ReadingHistoryRows[0], StringComparison.Ordinal);
        Assert.True(viewModel.IsReadingHistoryLoaded);
        Assert.False(viewModel.ShowLoadReadingHistoryButton);
        Assert.Equal(1, curation.HistoryCalls);
    }

    [AvaloniaFact]
    public async Task MetadataReview_LoadsEditsAndDecidesThroughAccessibleControls()
    {
        const string bookId = "PHASE14-REVIEW-BOOK";
        var readModel = new MutableReadModel(CreateProjection(bookId));
        var reviewService = new RecordingMetadataReviewService(bookId);
        var viewModel = new BookDetailViewModel(
            readModel,
            new NoOpReaderNavigation(),
            new InMemoryLocalizationService(),
            metadataReviewService: reviewService);

        await viewModel.LoadBookAsync(bookId);
        MetadataProposalItemViewModel proposal = Assert.Single(viewModel.PendingMetadataProposals);
        Assert.True(viewModel.CanReviewMetadata);
        Assert.Equal("Proposed title", proposal.EditableValue);

        var view = new BookDetailView { DataContext = viewModel };
        var window = new Window
        {
            Width = 420,
            Height = 700,
            Content = view,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        TabControl tabs = Assert.Single(view.GetVisualDescendants().OfType<TabControl>());
        tabs.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(view.GetVisualDescendants().OfType<TextBox>(), textBox =>
            textBox.GetValue(AutomationProperties.NameProperty) == "Title");
        Button acceptButton = Assert.Single(view.GetVisualDescendants().OfType<Button>(), button =>
            button.Content is TextBlock text && text.Text == "Accept");
        Button rejectButton = Assert.Single(view.GetVisualDescendants().OfType<Button>(), button =>
            button.Content is TextBlock text && text.Text == "Reject");
        acceptButton.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.True(acceptButton.IsFocused);
        rejectButton.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.True(rejectButton.IsFocused);

        proposal.EditableValue = "Curated title";
        await viewModel.AcceptMetadataProposalAsync(proposal);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(viewModel.PendingMetadataProposals);
        Assert.Equal((1L, true, "Curated title", true), reviewService.Decisions[0]);
        Assert.Equal("Metadata proposal accepted.", viewModel.MetadataReviewStatusText);
        window.Close();
    }

    private static BookDetailProjection CreateProjection(string bookId) => new(
        bookId,
        "Curation book",
        ["Author"],
        2026,
        null,
        null,
        null,
        0,
        null,
        "curation-book.pdf",
        null,
        null,
        new ReadingProgressProjection(bookId, 0, 0, null, (int)ReadingStatus.Unread),
        0,
        [],
        IsFavourite: false,
        IsAvailable: true);

    private sealed class MutableReadModel(BookDetailProjection initial) : ICatalogueReadModel
    {
        private BookDetailProjection _projection = initial;

        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<BookDetailProjection?> GetBookDetailAsync(
            string bookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BookDetailProjection?>(_projection);

        public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<ReadingProgressProjection?> GetProgressAsync(
            string bookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_projection.ReadingProgress);

        public void Apply(ReadingStatus? status, int? rating, bool? favourite)
        {
            ReadingProgressProjection progress = _projection.ReadingProgress ??
                new ReadingProgressProjection(_projection.BookId, 0, 0, null, (int)ReadingStatus.Unread);
            _projection = _projection with
            {
                Rating = rating ?? _projection.Rating,
                IsFavourite = favourite ?? _projection.IsFavourite,
                ReadingProgress = status is null ? progress : progress with { Status = (int)status.Value },
            };
        }

        public void ApplyTags(string? value)
        {
            IReadOnlyList<MetadataFieldProjection> fields =
                _projection.MetadataFields
                    .Where(field => field.FieldName is not ("Tag" or "Tags"))
                    .Append(new MetadataFieldProjection("Tags", value, "User", null, true))
                    .ToArray();
            _projection = _projection with { MetadataFields = fields };
        }
    }

    private sealed class RecordingCurationService(MutableReadModel readModel) : IBookCurationService
    {
        public List<CurationCall> Calls { get; } = [];
        public IReadOnlyList<ReadingStateHistoryEntry> HistoryEntries { get; init; } = [];
        public int HistoryCalls { get; private set; }

        public Task UpdateReadingStateAsync(
            string bookId,
            ReadingStatus? readingStatus = null,
            int? rating = null,
            bool? isFavourite = null,
            string reason = "user",
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new CurationCall(readingStatus, rating, isFavourite));
            readModel.Apply(readingStatus, rating, isFavourite);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ReadingStateHistoryEntry>> GetHistoryAsync(
            string bookId,
            int maxResults = 50,
            CancellationToken cancellationToken = default)
        {
            HistoryCalls++;
            return Task.FromResult<IReadOnlyList<ReadingStateHistoryEntry>>(
                HistoryEntries.Take(maxResults).ToArray());
        }
    }

    private sealed record CurationCall(ReadingStatus? Status, int? Rating, bool? IsFavourite);

    private sealed class RecordingCatalogueWriteService(MutableReadModel readModel) : ICatalogueWriteService
    {
        public List<(string BookId, string FieldName, string? Value)> Calls { get; } = [];

        public Task<string> CreateShelfAsync(
            string name,
            bool isSmart = false,
            string? query = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult("shelf-001");

        public Task RenameShelfAsync(
            string shelfId,
            string newName,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteShelfAsync(string shelfId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task AddBookToShelfAsync(
            string shelfId,
            string bookId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveBookFromShelfAsync(
            string shelfId,
            string bookId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateMetadataFieldAsync(
            string bookId,
            string fieldName,
            string? value,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((bookId, fieldName, value));
            readModel.ApplyTags(value);
            return Task.CompletedTask;
        }

        public Task BulkEditAsync(BulkEditCommand command, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class RecordingMetadataReviewService(string bookId) : IMetadataReviewService
    {
        private readonly List<MetadataProposalDescriptor> _pending =
        [
            new MetadataProposalDescriptor(
                1,
                bookId,
                "Title",
                "Proposed title",
                "Original title",
                0.91,
                "GoogleBooks",
                [new AlternativeFieldValue("Alternative title", "OpenLibrary", 0.72)],
                MetadataProposalStatus.Pending,
                DateTimeOffset.UtcNow,
                null),
        ];

        public List<(long Id, bool Accept, string? EditedValue, bool UserOverride)> Decisions { get; } = [];

        public Task<IReadOnlyList<MetadataProposalDescriptor>> CreateAsync(
            string requestedBookId,
            IReadOnlyList<MergedMetadataProposal> proposals,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MetadataProposalDescriptor>>([]);

        public Task<IReadOnlyList<MetadataProposalDescriptor>> ListPendingAsync(
            string requestedBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MetadataProposalDescriptor>>(
                _pending.Where(proposal => proposal.BookId == requestedBookId).ToList());

        public Task<MetadataProposalDescriptor> DecideAsync(
            long proposalId,
            bool accept,
            string? editedValue = null,
            bool userOverride = false,
            CancellationToken cancellationToken = default)
        {
            MetadataProposalDescriptor proposal = _pending.Single(proposal => proposal.Id == proposalId);
            Decisions.Add((proposalId, accept, editedValue, userOverride));
            _pending.Remove(proposal);
            return Task.FromResult(proposal with
            {
                Status = accept ? MetadataProposalStatus.Accepted : MetadataProposalStatus.Rejected,
                DecidedUtc = DateTimeOffset.UtcNow,
                Version = proposal.Version + 1,
            });
        }
    }

    private sealed class NoOpReaderNavigation : IReaderNavigationService
    {
        public Task OpenReaderAsync(
            string bookId,
            int? pageHint = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
