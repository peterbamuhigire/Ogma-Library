using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Ingestion;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>Accessible operator workflow for ambiguous filesystem relocations.</summary>
public sealed class ReconciliationReviewPanelViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IReconciliationReviewService _service;
    private readonly ILocalizationService _localization;
    private ReconciliationReviewItemViewModel? _selectedReview;
    private bool _isLoading;
    private string? _statusText;

    /// <summary>Initializes the relocation review panel.</summary>
    public ReconciliationReviewPanelViewModel(
        IReconciliationReviewService service,
        ILocalizationService localization)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _localization.CultureChanged += OnCultureChanged;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Pending relocation reviews.</summary>
    public ObservableCollection<ReconciliationReviewItemViewModel> Reviews { get; } = [];

    /// <summary>The selected review in the operator list.</summary>
    public ReconciliationReviewItemViewModel? SelectedReview
    {
        get => _selectedReview;
        set
        {
            if (!ReferenceEquals(_selectedReview, value))
            {
                _selectedReview = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanAccept));
                OnPropertyChanged(nameof(CanReject));
            }
        }
    }

    /// <summary>True while pending reviews are being loaded or decided.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanAccept));
                OnPropertyChanged(nameof(CanReject));
            }
        }
    }

    /// <summary>True when the selected review has a valid candidate.</summary>
    public bool CanAccept => !IsLoading && SelectedReview?.CanAccept == true;

    /// <summary>True when the selected review can be rejected.</summary>
    public bool CanReject => !IsLoading && SelectedReview is not null;

    /// <summary>Latest localized operation status.</summary>
    public string? StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    /// <summary>True when a status message is available.</summary>
    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusText);

    /// <summary>True when no pending review is available.</summary>
    public bool HasNoReviews => Reviews.Count == 0;

    /// <summary>Panel heading.</summary>
    public string Title => _localization["Reconciliation.Review.Title"];

    /// <summary>Panel explanation.</summary>
    public string Description => _localization["Reconciliation.Review.Description"];

    /// <summary>Candidate selector label.</summary>
    public string CandidateLabel => _localization["Reconciliation.Review.Candidate"];

    /// <summary>Accept action label.</summary>
    public string AcceptLabel => _localization["Reconciliation.Review.Accept"];

    /// <summary>Reject action label.</summary>
    public string RejectLabel => _localization["Reconciliation.Review.Reject"];

    /// <summary>Reload action label.</summary>
    public string ReloadLabel => _localization["Reconciliation.Review.Reload"];

    /// <summary>Close action label.</summary>
    public string CloseLabel => _localization["Reconciliation.Review.Close"];

    /// <summary>Empty-list message.</summary>
    public string EmptyText => _localization["Reconciliation.Review.Empty"];

    /// <summary>Loads all pending reviews.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            IReadOnlyList<ReconciliationReviewDescriptor> reviews =
                await _service.ListPendingAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Reviews.Clear();
                foreach (ReconciliationReviewDescriptor review in reviews)
                {
                    Reviews.Add(new ReconciliationReviewItemViewModel(review));
                }

                OnPropertyChanged(nameof(HasNoReviews));
                SelectedReview = Reviews.FirstOrDefault();
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            StatusText = string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                _localization["Reconciliation.Review.FailedFormat"],
                exception.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Accepts the selected persisted candidate path.</summary>
    public async Task AcceptSelectedAsync(CancellationToken cancellationToken = default)
    {
        ReconciliationReviewItemViewModel? review = SelectedReview;
        if (review?.SelectedPath is null)
        {
            StatusText = _localization["Reconciliation.Review.CandidateRequired"];
            return;
        }

        await DecideAsync(review, ReconciliationReviewDecision.Accept, review.SelectedPath, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Rejects the selected relocation without changing its path.</summary>
    public async Task RejectSelectedAsync(CancellationToken cancellationToken = default)
    {
        ReconciliationReviewItemViewModel? review = SelectedReview;
        if (review is null)
        {
            StatusText = _localization["Reconciliation.Review.NoneSelected"];
            return;
        }

        await DecideAsync(review, ReconciliationReviewDecision.Reject, null, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Releases the localization subscription.</summary>
    public void Dispose() => _localization.CultureChanged -= OnCultureChanged;

    private async Task DecideAsync(
        ReconciliationReviewItemViewModel review,
        ReconciliationReviewDecision decision,
        string? selectedPath,
        CancellationToken cancellationToken)
    {
        IsLoading = true;
        try
        {
            await _service.DecideAsync(review.ReviewId, decision, selectedPath, cancellationToken)
                .ConfigureAwait(false);
            StatusText = decision == ReconciliationReviewDecision.Accept
                ? _localization["Reconciliation.Review.Accepted"]
                : _localization["Reconciliation.Review.Rejected"];
            await LoadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            StatusText = string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                _localization["Reconciliation.Review.FailedFormat"],
                exception.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(CandidateLabel));
        OnPropertyChanged(nameof(AcceptLabel));
        OnPropertyChanged(nameof(RejectLabel));
        OnPropertyChanged(nameof(ReloadLabel));
        OnPropertyChanged(nameof(CloseLabel));
        OnPropertyChanged(nameof(EmptyText));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
