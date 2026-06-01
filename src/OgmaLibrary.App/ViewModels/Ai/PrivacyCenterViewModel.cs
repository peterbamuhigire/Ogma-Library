using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OgmaLibrary.App.Icons;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.App.ViewModels.Ai;

/// <summary>View model for the Phase 12 Privacy Center.</summary>
public sealed class PrivacyCenterViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IAiPrivacyService _privacy;
    private readonly IAiAuditRepository _audit;
    private readonly IAiQueryHistoryRepository _history;
    private readonly IEmbeddingErasureService _embeddings;
    private readonly IAiCostFormatter _costFormatter;
    private readonly ILocalizationService _localization;
    private readonly string _privacyIconPath = IconCatalog.GetAvaresPath("ic_ai_privacy") ?? string.Empty;
    private AiPrivacyTier _activeTier;
    private string _statusText;

    /// <summary>Initializes a new instance of <see cref="PrivacyCenterViewModel"/>.</summary>
    public PrivacyCenterViewModel(
        IAiPrivacyService privacy,
        IAiAuditRepository audit,
        IAiQueryHistoryRepository history,
        IEmbeddingErasureService embeddings,
        IAiCostFormatter costFormatter,
        ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(privacy);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(embeddings);
        ArgumentNullException.ThrowIfNull(costFormatter);
        ArgumentNullException.ThrowIfNull(localization);
        _privacy = privacy;
        _audit = audit;
        _history = history;
        _embeddings = embeddings;
        _costFormatter = costFormatter;
        _localization = localization;
        _activeTier = _privacy.GetActiveTier();
        _statusText = _localization["Ai.Privacy.Status.Ready"];
        _localization.CultureChanged += OnCultureChanged;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Recent immutable audit events.</summary>
    public ObservableCollection<PrivacyCenterAuditRow> RecentCalls { get; } = [];

    /// <summary>Available privacy tiers.</summary>
    public IReadOnlyList<AiPrivacyTier> Tiers { get; } =
    [
        AiPrivacyTier.Offline,
        AiPrivacyTier.MetadataOnly,
        AiPrivacyTier.ContentAware,
        AiPrivacyTier.LocalOllama,
    ];

    /// <summary>Active privacy tier.</summary>
    public AiPrivacyTier ActiveTier
    {
        get => _activeTier;
        private set
        {
            if (_activeTier != value)
            {
                _activeTier = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActiveTierLabel));
            }
        }
    }

    /// <summary>Status text for screen-reader announcements and footer display.</summary>
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

    /// <summary>Localized title.</summary>
    public string Title => _localization["Ai.Privacy.Title"];

    /// <summary>Localized active tier label.</summary>
    public string ActiveTierLabel => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        _localization["Ai.Privacy.ActiveTierFormat"],
        ActiveTier);

    /// <summary>Localized recent calls label.</summary>
    public string RecentCallsLabel => _localization["Ai.Privacy.RecentCalls"];

    /// <summary>Localized delete-history label.</summary>
    public string DeleteHistoryLabel => _localization["Ai.Privacy.DeleteHistory"];

    /// <summary>Localized erase-embeddings label.</summary>
    public string EraseEmbeddingsLabel => _localization["Ai.Privacy.EraseEmbeddings"];

    /// <summary>Localized export audit label.</summary>
    public string ExportAuditLabel => _localization["Ai.Privacy.ExportAudit"];

    /// <summary>Icon path for Privacy Center.</summary>
    public string PrivacyIconPath => _privacyIconPath;

    /// <summary>Loads recent audit calls.</summary>
    public async Task LoadAsync(int count = 25, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AiAuditEvent> events = await _audit.GetRecentAsync(count, cancellationToken)
            .ConfigureAwait(false);
        RecentCalls.Clear();
        foreach (AiAuditEvent auditEvent in events)
        {
            RecentCalls.Add(ToRow(auditEvent));
        }

        StatusText = string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            _localization["Ai.Privacy.Status.LoadedFormat"],
            RecentCalls.Count);
    }

    /// <summary>Sets the active privacy tier.</summary>
    public async Task SetTierAsync(AiPrivacyTier tier, CancellationToken cancellationToken = default)
    {
        await _privacy.SetTierAsync(tier, cancellationToken).ConfigureAwait(false);
        ActiveTier = tier;
        StatusText = ActiveTierLabel;
    }

    /// <summary>Deletes erasable AI query history without deleting immutable audit events.</summary>
    public async Task<int> DeleteHistoryAsync(CancellationToken cancellationToken = default)
    {
        int deleted = await _history.HardDeleteAllAsync(cancellationToken).ConfigureAwait(false);
        StatusText = string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            _localization["Ai.Privacy.Status.HistoryDeletedFormat"],
            deleted);
        return deleted;
    }

    /// <summary>Erases local semantic-search embeddings.</summary>
    public async Task<EmbeddingErasureResult> EraseEmbeddingsAsync(CancellationToken cancellationToken = default)
    {
        EmbeddingErasureResult result = await _embeddings.EraseAllAsync(cancellationToken).ConfigureAwait(false);
        StatusText = string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            _localization["Ai.Privacy.Status.EmbeddingsErasedFormat"],
            result.VectorsErased,
            result.BooksReset);
        return result;
    }

    /// <summary>Exports immutable audit events to JSON.</summary>
    public async Task ExportAuditAsync(Stream output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        await _audit.ExportToJsonAsync(output, cancellationToken).ConfigureAwait(false);
        StatusText = _localization["Ai.Privacy.Status.AuditExported"];
    }

    /// <inheritdoc />
    public void Dispose() => _localization.CultureChanged -= OnCultureChanged;

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        List<AiAuditEvent> events = RecentCalls.Select(row => row.AuditEvent).ToList();
        RecentCalls.Clear();
        foreach (AiAuditEvent auditEvent in events)
        {
            RecentCalls.Add(ToRow(auditEvent));
        }

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ActiveTierLabel));
        OnPropertyChanged(nameof(RecentCallsLabel));
        OnPropertyChanged(nameof(DeleteHistoryLabel));
        OnPropertyChanged(nameof(EraseEmbeddingsLabel));
        OnPropertyChanged(nameof(ExportAuditLabel));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private PrivacyCenterAuditRow ToRow(AiAuditEvent auditEvent) =>
        new(
            auditEvent,
            _costFormatter.FormatUsd(auditEvent.EstimatedCostUsd, _localization.CurrentCulture));
}

/// <summary>Display row for one immutable AI audit event.</summary>
public sealed record PrivacyCenterAuditRow(AiAuditEvent AuditEvent, string CostText)
{
    /// <summary>Timestamp of the AI call.</summary>
    public DateTimeOffset OccurredAt => AuditEvent.OccurredAt;

    /// <summary>Privacy tier of the AI call.</summary>
    public AiPrivacyTier Tier => AuditEvent.Tier;

    /// <summary>Provider key.</summary>
    public string Provider => AuditEvent.Provider;

    /// <summary>Provider model.</summary>
    public string Model => AuditEvent.Model;

    /// <summary>Prompt token count.</summary>
    public int? PromptTokens => AuditEvent.PromptTokens;

    /// <summary>Completion token count.</summary>
    public int? CompletionTokens => AuditEvent.CompletionTokens;
}
