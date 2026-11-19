using System.Reflection;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Domain;

namespace OgmaLibrary.Tests.Ui;

/// <summary>Shared fakes for the Sept-23 Phase 11 slice 4 review-card and raw-error tests.</summary>
internal static class Phase11ReviewFakes
{
    /// <summary>A sentinel exception text that must never reach user-visible status text.</summary>
    public const string RawSecret = "RAW-SECRET C:\\Users\\private\\book.pdf stack";

    public static BookDetailProjection Projection(string title = "Original Title", int? year = 2001) => new(
        "P11-REVIEW",
        title,
        ["Ada Example"],
        year,
        "9780306406157",
        null,
        3,
        0,
        null,
        "books/review.pdf",
        "ab12",
        1_048_576,
        new ReadingProgressProjection("P11-REVIEW", 0, 0, null, (int)ReadingStatus.Unread),
        0,
        [new MetadataFieldProjection("Title", title, "GoogleBooks", 0.8, false)],
        IsPasswordProtected: true,
        IsAvailable: true);

    public static MetadataProposalDescriptor Proposal(long id, string field, string? current, string proposed) => new(
        id,
        "P11-REVIEW",
        field,
        proposed,
        current,
        0.92,
        "OpenLibrary",
        [],
        MetadataProposalStatus.Pending,
        DateTimeOffset.UtcNow,
        null);

    /// <summary>Read model whose projection the fakes mutate.</summary>
    public sealed class MutableReadModel(BookDetailProjection detail) : ICatalogueReadModel
    {
        public BookDetailProjection Detail { get; set; } = detail;

        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<BookDetailProjection?> GetBookDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<BookDetailProjection?>(Detail);

        public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<ReadingProgressProjection?> GetProgressAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Detail.ReadingProgress);
    }

    /// <summary>Pending proposals and single decisions.</summary>
    public sealed class FakeReviewService(List<MetadataProposalDescriptor> pending) : IMetadataReviewService
    {
        public List<MetadataProposalDescriptor> Pending { get; } = pending;

        public List<(long Id, bool Accept)> Decisions { get; } = [];

        public Task<IReadOnlyList<MetadataProposalDescriptor>> CreateAsync(
            string bookId,
            IReadOnlyList<MergedMetadataProposal> proposals,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MetadataProposalDescriptor>>([]);

        public Task<IReadOnlyList<MetadataProposalDescriptor>> ListPendingAsync(
            string bookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MetadataProposalDescriptor>>(Pending.ToArray());

        public Task<MetadataProposalDescriptor> DecideAsync(
            long proposalId,
            bool accept,
            string? editedValue = null,
            bool userOverride = false,
            CancellationToken cancellationToken = default)
        {
            Decisions.Add((proposalId, accept));
            MetadataProposalDescriptor proposal = Pending.Single(p => p.Id == proposalId);
            Pending.Remove(proposal);
            return Task.FromResult(proposal with
            {
                Status = accept ? MetadataProposalStatus.Accepted : MetadataProposalStatus.Rejected,
            });
        }
    }

    /// <summary>
    /// Bulk service that applies Title/Year to the read model and restores them on undo.
    /// </summary>
    public sealed class FakeBulkService(MutableReadModel reads, FakeReviewService review) : IMetadataBulkReviewService
    {
        private BookDetailProjection? _beforeApply;
        private List<MetadataProposalDescriptor> _applied = [];

        public List<IReadOnlyList<long>> Previewed { get; } = [];

        public HashSet<string> FailFields { get; } = new(StringComparer.Ordinal);

        public bool UndoConflict { get; set; }

        public string? LastActorId { get; private set; }

        public Task<MetadataBulkReviewPreview> PreviewAsync(
            IReadOnlyList<long> proposalIds,
            CancellationToken cancellationToken = default)
        {
            Previewed.Add(proposalIds);
            MetadataBulkReviewItem[] items = review.Pending
                .Where(p => proposalIds.Contains(p.Id))
                .Select(p => new MetadataBulkReviewItem(p.Id, p.BookId, p.FieldName, p.CurrentValue, p.ProposedValue, p.Source, p.Confidence, p.Version))
                .ToArray();
            return Task.FromResult(new MetadataBulkReviewPreview("batch-1", items, DateTimeOffset.UtcNow));
        }

        public Task<MetadataBulkReviewResult> ApplyAsync(
            MetadataBulkReviewPreview preview,
            string actorId,
            CancellationToken cancellationToken = default)
        {
            LastActorId = actorId;
            _beforeApply = reads.Detail;
            _applied = [];
            var decisions = new List<MetadataBulkDecisionResult>();
            foreach (MetadataBulkReviewItem item in preview.Items)
            {
                if (FailFields.Contains(item.FieldName))
                {
                    decisions.Add(new MetadataBulkDecisionResult(item.ProposalId, item.BookId, item.FieldName, false, RawSecret));
                    continue;
                }

                reads.Detail = item.FieldName switch
                {
                    "Title" => reads.Detail with { Title = item.ProposedValue },
                    "Year" => reads.Detail with { Year = int.Parse(item.ProposedValue!, System.Globalization.CultureInfo.InvariantCulture) },
                    _ => reads.Detail,
                };
                MetadataProposalDescriptor proposal = review.Pending.Single(p => p.Id == item.ProposalId);
                review.Pending.Remove(proposal);
                _applied.Add(proposal);
                decisions.Add(new MetadataBulkDecisionResult(item.ProposalId, item.BookId, item.FieldName, true, null));
            }

            return Task.FromResult(new MetadataBulkReviewResult(preview.BatchId, "undo-1", decisions, DateTimeOffset.UtcNow));
        }

        public Task<bool> UndoAsync(
            string batchId,
            string undoToken,
            string actorId,
            CancellationToken cancellationToken = default)
        {
            if (UndoConflict || batchId != "batch-1" || undoToken != "undo-1" || _beforeApply is null)
            {
                return Task.FromResult(false);
            }

            reads.Detail = _beforeApply;
            review.Pending.AddRange(_applied);
            _applied = [];
            _beforeApply = null;
            return Task.FromResult(true);
        }
    }

    public sealed class NoOpReaderNavigation : IReaderNavigationService
    {
        public Task OpenReaderAsync(string bookId, int? pageHint = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    /// <summary>
    /// Creates an interface fake whose named methods throw <see cref="RawSecret"/>, and whose other
    /// Task-returning methods complete with an empty or configured result.
    /// </summary>
    public static T Fake<T>(IReadOnlySet<string> throwing, IReadOnlyDictionary<string, object?>? results = null)
        where T : class
    {
        T proxy = DispatchProxy.Create<T, FakeProxy>();
        var fake = (FakeProxy)(object)proxy;
        fake.Throwing = throwing;
        fake.Results = results ?? new Dictionary<string, object?>();
        return proxy;
    }

#pragma warning disable CA1852 // DispatchProxy requires a non-sealed proxy type.
    public class FakeProxy : DispatchProxy
#pragma warning restore CA1852
    {
        public IReadOnlySet<string> Throwing { get; set; } = new HashSet<string>();

        public IReadOnlyDictionary<string, object?> Results { get; set; } = new Dictionary<string, object?>();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (Throwing.Contains(targetMethod.Name))
            {
                throw new InvalidOperationException(RawSecret);
            }

            Type type = targetMethod.ReturnType;
            Results.TryGetValue(targetMethod.Name, out object? configured);
            if (type == typeof(Task))
            {
                return Task.CompletedTask;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                Type inner = type.GetGenericArguments()[0];
                object? value = configured ?? EmptyValue(inner);
                return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(inner).Invoke(null, [value]);
            }

            return configured ?? EmptyValue(type);
        }

        private static object? EmptyValue(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            {
                return Array.CreateInstance(type.GetGenericArguments()[0], 0);
            }

            return type.IsValueType && type != typeof(void) ? Activator.CreateInstance(type) : null;
        }
    }
}
