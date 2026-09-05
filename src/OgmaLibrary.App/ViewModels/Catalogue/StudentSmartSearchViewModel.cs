using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using OgmaLibrary.App.Icons;
using OgmaLibrary.Application;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>Student-facing classroom AI smart-search workflow.</summary>
public sealed class StudentSmartSearchViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IClassroomHostConnectionService _connections;
    private readonly ILibraryHostClient _hostClient;
    private readonly IProfileService _profiles;
    private readonly IStudentPrivateRepository _privateRepository;
    private readonly ILocalizationService _localization;
    private readonly string _iconPath = IconCatalog.GetAvaresPath("ic_ai_advisor") ?? string.Empty;

    private string _query = string.Empty;
    private string _libraryId = "default";
    private AiPrivacyTier _requestedTier = AiPrivacyTier.MetadataOnly;
    private bool _isBusy;
    private LibraryHostAiPayloadPreview? _preview;
    private string _answer = string.Empty;
    private string _statusText = string.Empty;
    private int _sessionTokensUsed;
    private int _lastTokensUsed;
    private decimal _lastEstimatedCostUsd;

    public StudentSmartSearchViewModel(
        IClassroomHostConnectionService connections,
        ILibraryHostClient hostClient,
        IProfileService profiles,
        IStudentPrivateRepository privateRepository,
        ILocalizationService localization)
    {
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _hostClient = hostClient ?? throw new ArgumentNullException(nameof(hostClient));
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _privateRepository = privateRepository ?? throw new ArgumentNullException(nameof(privateRepository));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _localization.CultureChanged += OnCultureChanged;
        _statusText = _localization["Classroom.SmartSearch.Status.Connect"];
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title => _localization["Classroom.SmartSearch.Title"];

    public string Subtitle => _localization["Classroom.SmartSearch.Subtitle"];

    public string QueryWatermark => _localization["Classroom.SmartSearch.QueryWatermark"];

    public string QueryAccessibleLabel => _localization["Classroom.SmartSearch.QueryLabel"];

    public string PreviewLabel => _localization["Classroom.SmartSearch.Preview"];

    public string DeleteHistoryLabel => _localization["Classroom.SmartSearch.DeleteHistory"];

    public string DeleteHistoryAccessibleLabel => _localization["Classroom.SmartSearch.DeleteHistoryAccessible"];

    public string PayloadPreviewLabel => _localization["Classroom.SmartSearch.PayloadPreview"];

    public string CancelPreviewLabel => _localization["Classroom.SmartSearch.CancelPreview"];

    public string ConfirmSearchLabel => _localization["Classroom.SmartSearch.ConfirmSearch"];

    public string AnswerLabel => _localization["Classroom.SmartSearch.Answer"];

    public string ClearAnswerLabel => _localization["Classroom.SmartSearch.ClearAnswer"];

    public string ClearAnswerAccessibleLabel => _localization["Classroom.SmartSearch.ClearAnswerAccessible"];

    public string CitationsLabel => _localization["Classroom.SmartSearch.Citations"];

    public string GroundingNotice => _localization["Classroom.SmartSearch.GroundingNotice"];

    public string IconPath => _iconPath;

    public string Query
    {
        get => _query;
        set
        {
            if (_query != value)
            {
                _query = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanRequestPreview));
            }
        }
    }

    public string LibraryId
    {
        get => _libraryId;
        set
        {
            if (_libraryId != value)
            {
                _libraryId = string.IsNullOrWhiteSpace(value) ? "default" : value.Trim();
                OnPropertyChanged();
            }
        }
    }

    public AiPrivacyTier RequestedTier
    {
        get => _requestedTier;
        set
        {
            if (_requestedTier != value)
            {
                _requestedTier = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PrivacyTierText));
            }
        }
    }

    public string PrivacyTierText => string.Format(
        _localization.CurrentCulture,
        _localization["Classroom.SmartSearch.PrivacyTierFormat"],
        _localization[$"Classroom.SmartSearch.Tier.{RequestedTier}"]);

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy != value)
            {
                _isBusy = value;
                OnPropertyChanged();
                RaiseCapabilitiesChanged();
            }
        }
    }

    public bool HasPreview => _preview is not null;

    public string PreviewSummary => _preview is null
        ? _localization["Classroom.SmartSearch.NoPreview"]
        : string.Format(
            _localization.CurrentCulture,
            _localization["Classroom.SmartSearch.PreviewSummaryFormat"],
            _preview.EstimatedCharacters,
            _preview.RequiresConfirmation
                ? _localization["Classroom.SmartSearch.ConfirmationRequired"]
                : _localization["Classroom.SmartSearch.ConfirmationNotRequired"]);

    public ObservableCollection<SmartSearchPreviewRow> MetadataPreviewRows { get; } = [];

    public bool HasAnswer => !string.IsNullOrWhiteSpace(Answer);

    public string Answer
    {
        get => _answer;
        private set
        {
            if (_answer != value)
            {
                _answer = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasAnswer));
            }
        }
    }

    public ObservableCollection<SmartSearchCitationRow> Citations { get; } = [];

    public bool HasCitations => Citations.Count > 0;

    public int SessionTokensUsed
    {
        get => _sessionTokensUsed;
        private set
        {
            if (_sessionTokensUsed != value)
            {
                _sessionTokensUsed = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(QuotaText));
                OnPropertyChanged(nameof(QuotaPercent));
            }
        }
    }

    public int LastTokensUsed
    {
        get => _lastTokensUsed;
        private set
        {
            if (_lastTokensUsed != value)
            {
                _lastTokensUsed = value;
                OnPropertyChanged();
            }
        }
    }

    public decimal LastEstimatedCostUsd
    {
        get => _lastEstimatedCostUsd;
        private set
        {
            if (_lastEstimatedCostUsd != value)
            {
                _lastEstimatedCostUsd = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EstimatedCostText));
            }
        }
    }

    public string EstimatedCostText => LastEstimatedCostUsd <= 0m
        ? _localization["Classroom.SmartSearch.CostZero"]
        : string.Format(
            _localization.CurrentCulture,
            _localization["Classroom.SmartSearch.CostFormat"],
            LastEstimatedCostUsd);

    public string QuotaText => string.Format(
        _localization.CurrentCulture,
        _localization["Classroom.SmartSearch.QuotaFormat"],
        SessionTokensUsed);

    public double QuotaPercent => Math.Clamp(SessionTokensUsed / 10_000d * 100d, 0d, 100d);

    public bool CanRequestPreview => !IsBusy && !string.IsNullOrWhiteSpace(Query);

    public bool CanConfirmSearch => !IsBusy && HasPreview;

    public bool CanCancelPreview => !IsBusy && HasPreview;

    public bool CanDeleteHistory => !IsBusy;

    public async Task RequestPreviewAsync(CancellationToken cancellationToken = default)
    {
        if (!CanRequestPreview)
        {
            StatusText = _localization["Classroom.SmartSearch.Status.EnterQuestion"];
            return;
        }

        await RunAsync(async () =>
        {
            (ClassroomHostConnection connection, ClassroomProfile profile) =
                await ResolveContextAsync(cancellationToken).ConfigureAwait(true);
            LibraryHostAiPayloadPreview preview = await _hostClient
                .PreviewAiSearchAsync(
                    connection.Request,
                    connection.SessionToken,
                    CreateRequest(profile.ProfileId, confirmed: false),
                    cancellationToken)
                .ConfigureAwait(true);

            ApplyPreview(preview);
            Answer = string.Empty;
            Citations.Clear();
            OnPropertyChanged(nameof(HasCitations));
            StatusText = _localization["Classroom.SmartSearch.Status.ReviewPreview"];
        }).ConfigureAwait(true);
    }

    public async Task ConfirmSearchAsync(CancellationToken cancellationToken = default)
    {
        if (!CanConfirmSearch)
        {
            StatusText = _localization["Classroom.SmartSearch.Status.ConfirmPreview"];
            return;
        }

        await RunAsync(async () =>
        {
            (ClassroomHostConnection connection, ClassroomProfile profile) =
                await ResolveContextAsync(cancellationToken).ConfigureAwait(true);
            LibraryHostAiSearchResult result = await _hostClient
                .SearchAiAsync(
                    connection.Request,
                    connection.SessionToken,
                    CreateRequest(profile.ProfileId, confirmed: true),
                    cancellationToken)
                .ConfigureAwait(true);

            ApplyResult(result);
            await SaveHistoryAsync(connection, profile, result, cancellationToken).ConfigureAwait(true);
            ClearPreview();
            StatusText = result.WasProviderCalled
                ? _localization["Classroom.SmartSearch.Status.AnswerReady"]
                : _localization["Classroom.SmartSearch.Status.NoProviderCall"];
        }).ConfigureAwait(true);
    }

    public void CancelPreview()
    {
        ClearPreview();
        StatusText = _localization["Classroom.SmartSearch.Status.PreviewCancelled"];
    }

    public void ClearAnswer()
    {
        Answer = string.Empty;
        Citations.Clear();
        LastTokensUsed = 0;
        LastEstimatedCostUsd = 0m;
        OnPropertyChanged(nameof(HasCitations));
        StatusText = _localization["Classroom.SmartSearch.Status.Cleared"];
    }

    public async Task DeleteHistoryAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            (ClassroomHostConnection connection, ClassroomProfile profile) =
                await ResolveContextAsync(cancellationToken).ConfigureAwait(true);
            int deleted = await _privateRepository
                .DeleteAiHistoryAsync(profile.ProfileId, CreateHostHistoryScope(connection), cancellationToken)
                .ConfigureAwait(true);
            StatusText = deleted == 1
                ? _localization["Classroom.SmartSearch.Status.HistoryDeletedOne"]
                : string.Format(
                    _localization.CurrentCulture,
                    _localization["Classroom.SmartSearch.Status.HistoryDeletedMany"],
                    deleted);
        }).ConfigureAwait(true);
    }

    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            StatusText = ex is TaskCanceledException
                ? _localization["Classroom.SmartSearch.Status.Cancelled"]
                : ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<(ClassroomHostConnection Connection, ClassroomProfile Profile)> ResolveContextAsync(
        CancellationToken cancellationToken)
    {
        ClassroomHostConnection? connection = await _connections
            .GetActiveAsync(cancellationToken)
            .ConfigureAwait(true);
        if (connection is null)
        {
            throw new InvalidOperationException(_localization["Classroom.SmartSearch.Status.Connect"]);
        }

        ClassroomProfile? profile = await _profiles.GetActiveAsync(cancellationToken).ConfigureAwait(true);
        if (profile is null || profile.IsGuest)
        {
            throw new InvalidOperationException(_localization["Classroom.SmartSearch.Status.EnrolledProfileRequired"]);
        }

        return (connection, profile);
    }

    private async Task SaveHistoryAsync(
        ClassroomHostConnection connection,
        ClassroomProfile profile,
        LibraryHostAiSearchResult result,
        CancellationToken cancellationToken)
    {
        string summary = string.IsNullOrWhiteSpace(result.Answer)
            ? _localization["Classroom.SmartSearch.NoLocalEvidence"]
            : result.Answer.Trim();
        if (summary.Length > 512)
        {
            summary = summary[..512];
        }

        await _privateRepository
            .SaveAiHistoryAsync(
                profile.ProfileId,
                new StudentAiHistoryEntry(
                    $"smart-search-{Guid.NewGuid():N}",
                    CreateHostHistoryScope(connection),
                    Query.Trim(),
                    summary,
                    RequestedTier.ToString(),
                    DateTimeOffset.UtcNow),
                cancellationToken)
            .ConfigureAwait(true);
    }

    private static string CreateHostHistoryScope(ClassroomHostConnection connection) =>
        $"{connection.Request.Address.Trim()}:{connection.Request.Port.ToString(CultureInfo.InvariantCulture)}:{connection.Request.CertificateFingerprint.Trim()}";

    private LibraryHostAiSearchRequest CreateRequest(Guid profileId, bool confirmed) =>
        new(
            profileId,
            Query.Trim(),
            string.IsNullOrWhiteSpace(LibraryId) ? "default" : LibraryId.Trim(),
            RequestedTier,
            confirmed);

    private void ApplyPreview(LibraryHostAiPayloadPreview preview)
    {
        _preview = preview;
        MetadataPreviewRows.Clear();
        foreach (KeyValuePair<string, string> field in preview.MetadataFields.OrderBy(field => field.Key, StringComparer.Ordinal))
        {
            MetadataPreviewRows.Add(new SmartSearchPreviewRow(field.Key, field.Value));
        }

        OnPropertyChanged(nameof(HasPreview));
        OnPropertyChanged(nameof(PreviewSummary));
        RaiseCapabilitiesChanged();
    }

    private void ApplyResult(LibraryHostAiSearchResult result)
    {
        Answer = string.IsNullOrWhiteSpace(result.Answer)
            ? _localization["Classroom.SmartSearch.NoLocalEvidence"]
            : result.Answer;
        Citations.Clear();
        foreach (LibraryHostAiCitation citation in result.Citations)
        {
            Citations.Add(new SmartSearchCitationRow(
                citation.BookId,
                citation.Title ?? citation.BookId,
                citation.PageNumber is null
                    ? _localization["Classroom.SmartSearch.PageNotSpecified"]
                    : string.Format(
                        _localization.CurrentCulture,
                        _localization["Classroom.SmartSearch.PageFormat"],
                        citation.PageNumber.Value)));
        }

        LastTokensUsed = result.TokensUsed;
        LastEstimatedCostUsd = result.EstimatedCostUsd;
        SessionTokensUsed += Math.Max(0, result.TokensUsed);
        OnPropertyChanged(nameof(HasCitations));
    }

    private void ClearPreview()
    {
        _preview = null;
        MetadataPreviewRows.Clear();
        OnPropertyChanged(nameof(HasPreview));
        OnPropertyChanged(nameof(PreviewSummary));
        RaiseCapabilitiesChanged();
    }

    private void RaiseCapabilitiesChanged()
    {
        OnPropertyChanged(nameof(CanRequestPreview));
        OnPropertyChanged(nameof(CanConfirmSearch));
        OnPropertyChanged(nameof(CanCancelPreview));
        OnPropertyChanged(nameof(CanDeleteHistory));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(QueryWatermark));
        OnPropertyChanged(nameof(QueryAccessibleLabel));
        OnPropertyChanged(nameof(PreviewLabel));
        OnPropertyChanged(nameof(DeleteHistoryLabel));
        OnPropertyChanged(nameof(DeleteHistoryAccessibleLabel));
        OnPropertyChanged(nameof(PayloadPreviewLabel));
        OnPropertyChanged(nameof(CancelPreviewLabel));
        OnPropertyChanged(nameof(ConfirmSearchLabel));
        OnPropertyChanged(nameof(AnswerLabel));
        OnPropertyChanged(nameof(ClearAnswerLabel));
        OnPropertyChanged(nameof(ClearAnswerAccessibleLabel));
        OnPropertyChanged(nameof(CitationsLabel));
        OnPropertyChanged(nameof(GroundingNotice));
        OnPropertyChanged(nameof(PrivacyTierText));
        OnPropertyChanged(nameof(PreviewSummary));
        OnPropertyChanged(nameof(EstimatedCostText));
        OnPropertyChanged(nameof(QuotaText));
    }

    public void Dispose() => _localization.CultureChanged -= OnCultureChanged;
}

public sealed record SmartSearchPreviewRow(string Key, string Value);

public sealed record SmartSearchCitationRow(string BookId, string Title, string Location);
