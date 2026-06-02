using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using OgmaLibrary.App.Icons;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>Student-facing classroom AI smart-search workflow.</summary>
public sealed class StudentSmartSearchViewModel : INotifyPropertyChanged
{
    private readonly IClassroomHostConnectionService _connections;
    private readonly ILibraryHostClient _hostClient;
    private readonly IProfileService _profiles;
    private readonly string _iconPath = IconCatalog.GetAvaresPath("ic_ai_advisor") ?? string.Empty;
    private readonly string _title = "AI Smart Search";
    private readonly string _subtitle = "Ask a question about the classroom library. Ogma shows the payload before anything leaves the Host.";

    private string _query = string.Empty;
    private string _libraryId = "default";
    private AiPrivacyTier _requestedTier = AiPrivacyTier.MetadataOnly;
    private bool _isBusy;
    private LibraryHostAiPayloadPreview? _preview;
    private string _answer = string.Empty;
    private string _statusText = "Connect to a classroom Host to use AI Smart Search.";
    private int _sessionTokensUsed;
    private int _lastTokensUsed;
    private decimal _lastEstimatedCostUsd;

    public StudentSmartSearchViewModel(
        IClassroomHostConnectionService connections,
        ILibraryHostClient hostClient,
        IProfileService profiles)
    {
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _hostClient = hostClient ?? throw new ArgumentNullException(nameof(hostClient));
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title => _title;

    public string Subtitle => _subtitle;

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

    public string PrivacyTierText => $"Privacy tier: {RequestedTier}";

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
        ? "No payload preview yet."
        : $"{_preview.EstimatedCharacters.ToString("N0", CultureInfo.CurrentCulture)} estimated characters, confirmation required: {_preview.RequiresConfirmation}";

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
        ? "Estimated cost: $0.00"
        : $"Estimated cost: ${LastEstimatedCostUsd:0.0000}";

    public string QuotaText => $"Session tokens used: {SessionTokensUsed:N0}";

    public double QuotaPercent => Math.Clamp(SessionTokensUsed / 10_000d * 100d, 0d, 100d);

    public bool CanRequestPreview => !IsBusy && !string.IsNullOrWhiteSpace(Query);

    public bool CanConfirmSearch => !IsBusy && HasPreview;

    public bool CanCancelPreview => !IsBusy && HasPreview;

    public async Task RequestPreviewAsync(CancellationToken cancellationToken = default)
    {
        if (!CanRequestPreview)
        {
            StatusText = "Enter a question before asking AI Smart Search.";
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
            StatusText = "Review the payload preview before sending the query.";
        }).ConfigureAwait(true);
    }

    public async Task ConfirmSearchAsync(CancellationToken cancellationToken = default)
    {
        if (!CanConfirmSearch)
        {
            StatusText = "Review a payload preview before confirming AI Smart Search.";
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
            ClearPreview();
            StatusText = result.WasProviderCalled
                ? "AI Smart Search answer is ready."
                : "No provider call was needed for this answer.";
        }).ConfigureAwait(true);
    }

    public void CancelPreview()
    {
        ClearPreview();
        StatusText = "Payload preview cancelled.";
    }

    public void ClearAnswer()
    {
        Answer = string.Empty;
        Citations.Clear();
        LastTokensUsed = 0;
        LastEstimatedCostUsd = 0m;
        OnPropertyChanged(nameof(HasCitations));
        StatusText = "AI Smart Search cleared.";
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
                ? "AI Smart Search was cancelled."
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
            throw new InvalidOperationException("Connect to a classroom Host before using AI Smart Search.");
        }

        ClassroomProfile? profile = await _profiles.GetActiveAsync(cancellationToken).ConfigureAwait(true);
        if (profile is null || profile.IsGuest)
        {
            throw new InvalidOperationException("Use an enrolled student profile before using AI Smart Search.");
        }

        return (connection, profile);
    }

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
            ? "No local evidence found."
            : result.Answer;
        Citations.Clear();
        foreach (LibraryHostAiCitation citation in result.Citations)
        {
            Citations.Add(new SmartSearchCitationRow(
                citation.BookId,
                citation.Title ?? citation.BookId,
                citation.PageNumber is null
                    ? "Page not specified"
                    : $"Page {citation.PageNumber.Value.ToString(CultureInfo.CurrentCulture)}"));
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
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed record SmartSearchPreviewRow(string Key, string Value);

public sealed record SmartSearchCitationRow(string BookId, string Title, string Location);
