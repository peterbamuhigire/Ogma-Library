using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using OgmaLibrary.App.Icons;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Ai;

namespace OgmaLibrary.App.ViewModels.Ai;

/// <summary>View model for the Phase 12 AI payload-preview dialog.</summary>
public sealed class PayloadPreviewViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ILocalizationService _localization;
    private readonly string _privacyIconPath = IconCatalog.GetAvaresPath("ic_ai_privacy") ?? string.Empty;
    private readonly string _sendIconPath = IconCatalog.GetAvaresPath("ic_status_available") ?? string.Empty;
    private readonly string _cancelIconPath = IconCatalog.GetAvaresPath("ic_close") ?? string.Empty;
    private AiPreviewDecision? _decision;

    /// <summary>Initializes a new instance of <see cref="PayloadPreviewViewModel"/>.</summary>
    public PayloadPreviewViewModel(AiPayloadPreview preview, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(localization);
        Preview = preview;
        _localization = localization;
        foreach (PayloadPreviewItem item in BuildItems(preview))
        {
            Items.Add(item);
        }

        _localization.CultureChanged += OnCultureChanged;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The exact preview payload.</summary>
    public AiPayloadPreview Preview { get; }

    /// <summary>Flattened payload fields shown to the user.</summary>
    public ObservableCollection<PayloadPreviewItem> Items { get; } = [];

    /// <summary>The selected dialog decision, if any.</summary>
    public AiPreviewDecision? Decision
    {
        get => _decision;
        private set
        {
            if (_decision != value)
            {
                _decision = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasDecision));
            }
        }
    }

    /// <summary>Whether the user has made a decision.</summary>
    public bool HasDecision => Decision is not null;

    /// <summary>Localized dialog title.</summary>
    public string Title => _localization["Ai.Preview.Title"];

    /// <summary>Localized explanatory text.</summary>
    public string Explanation => _localization["Ai.Preview.Explanation"];

    /// <summary>Localized provider summary.</summary>
    public string ProviderSummary => string.Format(
        CultureInfo.CurrentCulture,
        _localization["Ai.Preview.ProviderFormat"],
        Preview.Provider,
        Preview.Model);

    /// <summary>Localized tier summary.</summary>
    public string TierSummary => string.Format(
        CultureInfo.CurrentCulture,
        _localization["Ai.Preview.TierFormat"],
        Preview.Tier);

    /// <summary>Localized payload size summary.</summary>
    public string PayloadSizeSummary => string.Format(
        CultureInfo.CurrentCulture,
        _localization["Ai.Preview.SizeFormat"],
        Preview.CharacterCount);

    /// <summary>Localized send label.</summary>
    public string SendLabel => _localization["Ai.Preview.Send"];

    /// <summary>Localized cancel label.</summary>
    public string CancelLabel => _localization["Ai.Preview.Cancel"];

    /// <summary>Localized remember-for-session label.</summary>
    public string RememberLabel => _localization["Ai.Preview.RememberSession"];

    /// <summary>Accessible label for the preview dialog.</summary>
    public string AccessibleLabel => string.Format(
        CultureInfo.CurrentCulture,
        _localization["Ai.Preview.AccessibleFormat"],
        Preview.Provider,
        Preview.Model,
        Preview.CharacterCount);

    /// <summary>Icon path for AI privacy context.</summary>
    public string PrivacyIconPath => _privacyIconPath;

    /// <summary>Icon path for sending the payload.</summary>
    public string SendIconPath => _sendIconPath;

    /// <summary>Icon path for cancelling the payload.</summary>
    public string CancelIconPath => _cancelIconPath;

    /// <summary>Records a Send decision.</summary>
    public void Send() => Decision = AiPreviewDecision.Send;

    /// <summary>Records a Cancel decision.</summary>
    public void Cancel() => Decision = AiPreviewDecision.Cancel;

    /// <summary>Records a Remember-for-session decision.</summary>
    public void RememberForSession() => Decision = AiPreviewDecision.RememberForSession;

    /// <inheritdoc />
    public void Dispose() => _localization.CultureChanged -= OnCultureChanged;

    private static IEnumerable<PayloadPreviewItem> BuildItems(AiPayloadPreview preview)
    {
        yield return new PayloadPreviewItem("query-type", "Query type", preview.QueryType);
        if (!string.IsNullOrWhiteSpace(preview.QueryText))
        {
            yield return new PayloadPreviewItem("query", "Query", preview.QueryText);
        }

        foreach (KeyValuePair<string, string> field in preview.MetadataFields.OrderBy(field => field.Key, StringComparer.Ordinal))
        {
            yield return new PayloadPreviewItem("metadata", field.Key, field.Value);
        }

        foreach (AiContentChunk chunk in preview.ContentChunks)
        {
            yield return new PayloadPreviewItem("content", $"{chunk.BookId} {chunk.Source}", chunk.Text);
        }
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Explanation));
        OnPropertyChanged(nameof(ProviderSummary));
        OnPropertyChanged(nameof(TierSummary));
        OnPropertyChanged(nameof(PayloadSizeSummary));
        OnPropertyChanged(nameof(SendLabel));
        OnPropertyChanged(nameof(CancelLabel));
        OnPropertyChanged(nameof(RememberLabel));
        OnPropertyChanged(nameof(AccessibleLabel));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>One row in the payload-preview field list.</summary>
public sealed record PayloadPreviewItem(string Kind, string Label, string Value);
