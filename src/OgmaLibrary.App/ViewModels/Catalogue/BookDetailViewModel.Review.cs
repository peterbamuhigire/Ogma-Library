using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Metadata;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>
/// Sept-23 Phase 11 slice 4 (task 11.6): the Activity review card. Accepts go through the
/// undoable bulk-review service; rejects stay on the single-decision service.
/// </summary>
public sealed partial class BookDetailViewModel
{
    // The desktop app has one local user. This matches the actor used by the other local metadata
    // writers (IdentityGroupingViewModel); no shared actor provider exists yet (D-68).
    private const string ReviewActorId = "local-user";

    // The bulk service rejects batches above this size.
    private const int MaximumReviewBatch = 100;

    private readonly IMetadataBulkReviewService? _bulkReview;
    private string? _reviewBatchId;
    private string? _reviewUndoToken;
    private string? _reviewBatchBookId;

    /// <summary>Number of pending suggested changes for the loaded book.</summary>
    public int SuggestedChangeCount => PendingMetadataProposals.Count;

    /// <summary>True when at least one suggested change is pending.</summary>
    public bool HasSuggestedChanges => SuggestedChangeCount > 0;

    /// <summary>True when the review card should be shown under Activity.</summary>
    public bool ShowReviewCard =>
        _book is not null && _metadataReviewService is not null &&
        (HasSuggestedChanges || CanUndoReview || HasMetadataReviewStatus);

    /// <summary>Card heading, for example "3 suggested changes".</summary>
    public string ReviewCardTitleText => SuggestedChangeCount == 1
        ? _localization["Catalogue.BookDetail.Review.CountOne"]
        : string.Format(
            _localization.CurrentCulture,
            _localization["Catalogue.BookDetail.Review.CountFormat"],
            SuggestedChangeCount);

    /// <summary>Count shown as a badge on the Activity segment.</summary>
    public string ActivityBadgeText => SuggestedChangeCount.ToString(_localization.CurrentCulture);

    /// <summary>Accessible name of the Activity segment, including the pending count when any.</summary>
    public string ActivitySectionAccessibleText => HasSuggestedChanges
        ? string.Format(
            _localization.CurrentCulture,
            _localization["Catalogue.BookDetail.Review.SectionBadgeFormat"],
            ActivitySectionText,
            ReviewCardTitleText)
        : ActivitySectionText;

    /// <summary>Localised Accept all label.</summary>
    public string AcceptAllText => _localization["Catalogue.BookDetail.Review.AcceptAll"];

    /// <summary>Localised Undo label for the last accepted batch.</summary>
    public string UndoReviewText => _localization["Catalogue.BookDetail.Review.Undo"];

    /// <summary>Accessible name for the review Undo button.</summary>
    public string UndoReviewAccessibleText => _localization["Catalogue.BookDetail.Review.UndoAccessible"];

    /// <summary>True when every pending change can be accepted as one undoable batch.</summary>
    public bool CanAcceptAll => HasSuggestedChanges && _bulkReview is not null && !IsReviewingMetadata;

    /// <summary>True when the last accepted batch for this book can be undone.</summary>
    public bool CanUndoReview =>
        _reviewBatchId is not null &&
        _bulkReview is not null &&
        !IsReviewingMetadata &&
        string.Equals(_reviewBatchBookId, _book?.BookId, StringComparison.Ordinal);

    /// <summary>True when the book has no bibliographic details beyond its file name.</summary>
    public bool HasNoMetadata =>
        _book is not null &&
        !HasSuggestedChanges &&
        _book.Authors.Count == 0 &&
        _book.Year is null &&
        string.IsNullOrWhiteSpace(_book.Isbn) &&
        string.IsNullOrWhiteSpace(_book.Doi);

    /// <summary>True when the no-metadata state should offer Enrich.</summary>
    public bool ShowNoMetadataEnrich => HasNoMetadata && CanEnrich;

    /// <summary>True when the no-metadata state should point to Settings instead.</summary>
    public bool ShowNoMetadataSettingsHint => HasNoMetadata && _metadataEnrichment is null;

    /// <summary>Localised no-metadata heading.</summary>
    public string NoMetadataTitleText => _localization["Catalogue.BookDetail.NoMetadata.Title"];

    /// <summary>Localised no-metadata explanation.</summary>
    public string NoMetadataBodyText => _localization["Catalogue.BookDetail.NoMetadata.Body"];

    /// <summary>Localised hint that points to Settings when lookup is unavailable.</summary>
    public string NoMetadataSettingsHintText => _localization["Catalogue.BookDetail.NoMetadata.SettingsHint"];

    /// <summary>Accepts every pending suggestion as one undoable batch.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task AcceptAllProposalsAsync(CancellationToken cancellationToken = default)
    {
        if (!CanAcceptAll)
        {
            return Task.CompletedTask;
        }

        long[] ids = PendingMetadataProposals
            .Select(p => p.ProposalId)
            .Take(MaximumReviewBatch)
            .ToArray();
        return ApplyBulkAsync(ids, cancellationToken);
    }

    /// <summary>Undoes the last accepted batch, explaining a conflict when a later edit blocks it.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task UndoReviewAsync(CancellationToken cancellationToken = default)
    {
        if (!CanUndoReview || _book is null)
        {
            return;
        }

        string bookId = _book.BookId;
        string batchId = _reviewBatchId!;
        string undoToken = _reviewUndoToken!;
        IsReviewingMetadata = true;
        try
        {
            bool undone = await _bulkReview!
                .UndoAsync(batchId, undoToken, ReviewActorId, cancellationToken)
                .ConfigureAwait(false);
            if (!undone)
            {
                AppLog.ViewModelOperationRejected(_logger, ViewModelName, "review.undo", "conflict");
            }

            BookDetailProjection? detail = await _readModel
                .GetBookDetailAsync(bookId, cancellationToken)
                .ConfigureAwait(false);
            IReadOnlyList<MetadataProposalDescriptor> proposals = await LoadPendingMetadataAsync(
                bookId,
                cancellationToken).ConfigureAwait(false);
            UpdateOnUiThread(() =>
            {
                // The token is one-time either way: a conflict cannot be retried into success.
                ClearReviewBatch();
                if (string.Equals(_book?.BookId, bookId, StringComparison.Ordinal))
                {
                    Book = detail ?? _book;
                    ReplacePendingMetadataProposals(proposals);
                }

                MetadataReviewStatusText = _localization[undone
                    ? "Catalogue.BookDetail.Review.Undone"
                    : "Catalogue.BookDetail.Review.UndoConflict"];
                IsReviewingMetadata = false;
            });
        }
        catch (OperationCanceledException)
        {
            UpdateOnUiThread(() => IsReviewingMetadata = false);
        }
        catch (Exception ex)
        {
            AppLog.ViewModelOperationFailed(_logger, ex, ViewModelName, "review.undo");
            UpdateOnUiThread(() =>
            {
                MetadataReviewStatusText = _localization["Catalogue.BookDetail.Review.UndoFailed"];
                IsReviewingMetadata = false;
            });
        }
    }

    private async Task ApplyBulkAsync(long[] proposalIds, CancellationToken cancellationToken)
    {
        if (_book is null || _bulkReview is null || IsReviewingMetadata || proposalIds.Length == 0)
        {
            return;
        }

        string bookId = _book.BookId;
        IsReviewingMetadata = true;
        MetadataReviewStatusText = null;
        try
        {
            MetadataBulkReviewPreview preview = await _bulkReview
                .PreviewAsync(proposalIds, cancellationToken)
                .ConfigureAwait(false);
            MetadataBulkReviewResult result = await _bulkReview
                .ApplyAsync(preview, ReviewActorId, cancellationToken)
                .ConfigureAwait(false);
            MetadataBulkDecisionResult[] failed = result.Decisions.Where(d => !d.Applied).ToArray();
            int appliedCount = result.Decisions.Count - failed.Length;
            foreach (MetadataBulkDecisionResult decision in failed)
            {
                AppLog.ViewModelOperationRejected(
                    _logger, ViewModelName, "review.apply", decision.FieldName);
            }

            BookDetailProjection? detail = await _readModel
                .GetBookDetailAsync(bookId, cancellationToken)
                .ConfigureAwait(false);
            IReadOnlyList<MetadataProposalDescriptor> proposals = await LoadPendingMetadataAsync(
                bookId,
                cancellationToken).ConfigureAwait(false);
            UpdateOnUiThread(() =>
            {
                if (appliedCount > 0)
                {
                    _reviewBatchId = result.BatchId;
                    _reviewUndoToken = result.UndoToken;
                    _reviewBatchBookId = bookId;
                }

                if (string.Equals(_book?.BookId, bookId, StringComparison.Ordinal))
                {
                    Book = detail ?? _book;
                    ReplacePendingMetadataProposals(proposals);
                }

                MetadataReviewStatusText = failed.Length > 0
                    ? string.Format(
                        _localization.CurrentCulture,
                        _localization["Catalogue.BookDetail.Review.PartialFormat"],
                        string.Join(
                            _localization["Catalogue.BookDetail.Review.ListSeparator"],
                            failed.Select(d => ReviewFieldLabel(d.FieldName)).Distinct(StringComparer.Ordinal)))
                    : appliedCount == 1
                        ? _localization["Catalogue.BookDetail.Review.AcceptedOne"]
                        : string.Format(
                            _localization.CurrentCulture,
                            _localization["Catalogue.BookDetail.Review.AcceptedFormat"],
                            appliedCount);
                IsReviewingMetadata = false;
            });
        }
        catch (OperationCanceledException)
        {
            UpdateOnUiThread(() => IsReviewingMetadata = false);
        }
        catch (Exception ex)
        {
            AppLog.ViewModelOperationFailed(_logger, ex, ViewModelName, "review.apply");
            UpdateOnUiThread(() =>
            {
                MetadataReviewStatusText = _localization["Catalogue.BookDetail.MetadataReview.FailedFormat"];
                IsReviewingMetadata = false;
            });
        }
    }

    // Uses the inline-edit label when the field has one; other provider fields keep their canonical name.
    private string ReviewFieldLabel(string fieldName)
    {
        string key = "Catalogue.BookDetail.Edit.Label." + fieldName;
        string label = _localization[key];
        return string.Equals(label, key, StringComparison.Ordinal) ? fieldName : label;
    }

    private void ClearReviewBatch()
    {
        _reviewBatchId = null;
        _reviewUndoToken = null;
        _reviewBatchBookId = null;
        OnReviewStateChanged();
    }

    private void OnReviewProposalsChanged()
    {
        OnPropertyChanged(nameof(SuggestedChangeCount));
        OnPropertyChanged(nameof(HasSuggestedChanges));
        OnPropertyChanged(nameof(ReviewCardTitleText));
        OnPropertyChanged(nameof(ActivityBadgeText));
        OnPropertyChanged(nameof(ActivitySectionAccessibleText));
        OnReviewBookChanged();
        OnReviewStateChanged();
    }

    private void OnReviewStateChanged()
    {
        OnPropertyChanged(nameof(CanAcceptAll));
        OnPropertyChanged(nameof(CanUndoReview));
        OnPropertyChanged(nameof(ShowReviewCard));
    }

    private void OnReviewBookChanged()
    {
        OnPropertyChanged(nameof(HasNoMetadata));
        OnPropertyChanged(nameof(ShowNoMetadataEnrich));
        OnPropertyChanged(nameof(ShowNoMetadataSettingsHint));
        OnPropertyChanged(nameof(CanUndoReview));
        OnPropertyChanged(nameof(ShowReviewCard));
    }

    private void OnReviewCultureChanged()
    {
        OnPropertyChanged(nameof(AcceptAllText));
        OnPropertyChanged(nameof(UndoReviewText));
        OnPropertyChanged(nameof(UndoReviewAccessibleText));
        OnPropertyChanged(nameof(NoMetadataTitleText));
        OnPropertyChanged(nameof(NoMetadataBodyText));
        OnPropertyChanged(nameof(NoMetadataSettingsHintText));
        ReplacePendingMetadataProposals(PendingMetadataProposals.Select(p => p.Proposal).ToArray());
    }
}
