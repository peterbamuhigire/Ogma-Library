using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;
using static OgmaLibrary.Tests.Ui.Phase11ReviewFakes;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Sept-23 Phase 11 slice 4 (task 11.7/11.9): every failing-service path in the inspector shows a
/// localised, actionable message and never the thrown exception text (AGENT_BRIEF rule 6).
/// </summary>
public sealed class Phase11NoRawErrorTests
{
    private static readonly HashSet<string> None = [];

    public static TheoryData<string> FailingPaths() =>
    [
        "WriteBackConfirm",
        "WriteBackRestore",
        "History",
        "TagsValidation",
        "Tags",
        "ReviewDecide",
        "ReviewList",
        "EnrichThrows",
        "EnrichReportsFailure",
        "OcrThrows",
        "OcrReportsFailure",
        "ForgetPassword",
        "ReadingMemory",
        "Curation",
        "AcceptAll",
        "Undo",
    ];

    [AvaloniaTheory]
    [MemberData(nameof(FailingPaths))]
    public async Task FailingService_ShowsLocalisedStatus_WithoutTheThrownMessage(string path)
    {
        var localization = new InMemoryLocalizationService();
        string? status = await RunAsync(path, localization);

        Assert.False(string.IsNullOrWhiteSpace(status), $"{path} produced no status text");
        Assert.DoesNotContain("RAW-SECRET", status, StringComparison.Ordinal);
        Assert.DoesNotContain("{0}", status, StringComparison.Ordinal);

        localization.SetCulture("fr-FR");
        string? french = await RunAsync(path, localization);
        Assert.False(string.IsNullOrWhiteSpace(french));
        Assert.DoesNotContain("RAW-SECRET", french, StringComparison.Ordinal);
        Assert.NotEqual(status, french);
    }

    private static async Task<string?> RunAsync(string path, InMemoryLocalizationService localization)
    {
        var reads = new MutableReadModel(Projection());
        bool listThrows = path == "ReviewList";
        var review = new FakeReviewService([Proposal(1, "Title", "Original Title", "Better Title")]);
        IMetadataReviewService reviewService = listThrows
            ? Fake<IMetadataReviewService>(new HashSet<string> { nameof(IMetadataReviewService.ListPendingAsync) })
            : path == "ReviewDecide"
                ? new ThrowingDecideReview(review)
                : review;
        var bulk = new FakeBulkService(reads, review);
        IMetadataBulkReviewService bulkService = path switch
        {
            "AcceptAll" => Fake<IMetadataBulkReviewService>(new HashSet<string> { nameof(IMetadataBulkReviewService.PreviewAsync) }),
            "Undo" => new ThrowingUndoBulk(bulk),
            _ => bulk,
        };

        object? enrichResult = path == "EnrichReportsFailure" ? (false, (string?)RawSecret) : null;
        object? ocrResult = path == "OcrReportsFailure" ? new OcrQueueResult(false, false, null, RawSecret) : null;

        var vm = new BookDetailViewModel(
            reads,
            new NoOpReaderNavigation(),
            localization,
            metadataEnrichment: Fake<IBookMetadataEnrichmentService>(
                path == "EnrichThrows" ? new HashSet<string> { "EnrichAsync" } : None,
                enrichResult is null ? null : new Dictionary<string, object?> { ["EnrichAsync"] = enrichResult }),
            readingMemoryService: Fake<IReadingMemoryService>(path == "ReadingMemory" ? new HashSet<string> { "SaveAsync" } : None),
            ocrJobs: Fake<IOcrJobQueueService>(
                path == "OcrThrows" ? new HashSet<string> { "QueueBookAsync" } : None,
                ocrResult is null ? null : new Dictionary<string, object?> { ["QueueBookAsync"] = ocrResult }),
            passwordProvider: Fake<IPasswordProvider>(new HashSet<string> { "ForgetPasswordAsync" }),
            curation: Fake<IBookCurationService>(new HashSet<string> { "GetHistoryAsync", "UpdateReadingStateAsync" }),
            catalogueWriteService: Fake<ICatalogueWriteService>(new HashSet<string> { "UpdateMetadataFieldAsync" }),
            metadataReviewService: reviewService,
            fileLocator: Fake<IBookFileLocator>(None, new Dictionary<string, object?> { ["LocateAsync"] = "C:\\fake\\review.pdf" }),
            writeBackService: Fake<IMetadataWriteBackService>(
                new HashSet<string> { "WriteAsync", "RestoreBackupAsync" },
                new Dictionary<string, object?>
                {
                    ["BuildDiffAsync"] = (IReadOnlyList<FieldDiff>)[new FieldDiff("Title", "Old", "New")],
                    ["PrepareBackupAsync"] = new BackupToken("C:\\b.pdf", "C:\\o.pdf", "ab12"),
                }),
            metadataBulkReviewService: bulkService);

        await vm.LoadBookAsync(reads.Detail.BookId);
        Dispatcher.UIThread.RunJobs();
        if (listThrows)
        {
            return vm.MetadataReviewStatusText;
        }

        switch (path)
        {
            case "WriteBackConfirm":
                await vm.PrepareWriteBackAsync();
                await vm.ConfirmWriteBackAsync();
                Dispatcher.UIThread.RunJobs();
                return vm.WriteBackStatusText;
            case "WriteBackRestore":
                await vm.PrepareWriteBackAsync();
                await vm.RestoreWriteBackAsync();
                Dispatcher.UIThread.RunJobs();
                return vm.WriteBackStatusText;
            case "History":
                await vm.LoadReadingHistoryAsync();
                return vm.ReadingHistoryStatusText;
            case "TagsValidation":
                vm.TagsText = string.Join(",", Enumerable.Range(0, 40).Select(i => "tag" + i));
                await vm.SaveTagsAsync();
                return vm.TagsStatusText;
            case "Tags":
                vm.TagsText = "history";
                await vm.SaveTagsAsync();
                return vm.TagsStatusText;
            case "ReviewDecide":
                await vm.RejectMetadataProposalAsync(vm.PendingMetadataProposals[0]);
                return vm.MetadataReviewStatusText;
            case "EnrichThrows":
            case "EnrichReportsFailure":
                await vm.EnrichMetadataAsync();
                return vm.EnrichmentStatusText;
            case "OcrThrows":
            case "OcrReportsFailure":
                await vm.RunOcrAsync();
                return vm.OcrStatusText;
            case "ForgetPassword":
                await vm.ForgetPasswordAsync();
                return vm.PasswordStatusText;
            case "ReadingMemory":
                vm.ReadingMemoryKeyInsight = "insight";
                await vm.SaveReadingMemoryAsync();
                return vm.ReadingMemoryStatusText;
            case "Curation":
                await vm.ToggleFavouriteAsync();
                return vm.CurationStatusText;
            case "AcceptAll":
                await vm.AcceptAllProposalsAsync();
                return vm.MetadataReviewStatusText;
            case "Undo":
                await vm.AcceptAllProposalsAsync();
                await vm.UndoReviewAsync();
                return vm.MetadataReviewStatusText;
            default:
                throw new ArgumentOutOfRangeException(nameof(path), path, null);
        }
    }

    private sealed class ThrowingDecideReview(FakeReviewService inner) : IMetadataReviewService
    {
        public Task<IReadOnlyList<MetadataProposalDescriptor>> CreateAsync(string bookId, IReadOnlyList<MergedMetadataProposal> proposals, CancellationToken cancellationToken = default) =>
            inner.CreateAsync(bookId, proposals, cancellationToken);

        public Task<IReadOnlyList<MetadataProposalDescriptor>> ListPendingAsync(string bookId, CancellationToken cancellationToken = default) =>
            inner.ListPendingAsync(bookId, cancellationToken);

        public Task<MetadataProposalDescriptor> DecideAsync(long proposalId, bool accept, string? editedValue = null, bool userOverride = false, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(RawSecret);
    }

    private sealed class ThrowingUndoBulk(FakeBulkService inner) : IMetadataBulkReviewService
    {
        public Task<MetadataBulkReviewPreview> PreviewAsync(IReadOnlyList<long> proposalIds, CancellationToken cancellationToken = default) =>
            inner.PreviewAsync(proposalIds, cancellationToken);

        public Task<MetadataBulkReviewResult> ApplyAsync(MetadataBulkReviewPreview preview, string actorId, CancellationToken cancellationToken = default) =>
            inner.ApplyAsync(preview, actorId, cancellationToken);

        public Task<bool> UndoAsync(string batchId, string undoToken, string actorId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(RawSecret);
    }
}
