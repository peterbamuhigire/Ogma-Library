using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OgmaLibrary.App.Icons;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Domain;
using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.App.ViewModels.Ai;

/// <summary>View model for AI recommendation cards.</summary>
public sealed class RecommendationPanelViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IAiAdvisorService _advisor;
    private readonly IBookDetailNavigationService _navigation;
    private readonly ILocalizationService _localization;
    private readonly string _iconPath = IconCatalog.GetAvaresPath("ic_ai_advisor") ?? string.Empty;
    private string _query = string.Empty;
    private bool _isLoading;
    private string? _errorText;
    private string _statusText;

    /// <summary>Initializes a new instance of <see cref="RecommendationPanelViewModel"/>.</summary>
    public RecommendationPanelViewModel(
        IAiAdvisorService advisor,
        IBookDetailNavigationService navigation,
        ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(advisor);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(localization);

        _advisor = advisor;
        _navigation = navigation;
        _localization = localization;
        _statusText = _localization["Ai.Advisor.Status.Ready"];
        _localization.CultureChanged += OnCultureChanged;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Recommendation cards.</summary>
    public ObservableCollection<RecommendationCardViewModel> Recommendations { get; } = [];

    /// <summary>User query text.</summary>
    public string Query
    {
        get => _query;
        set
        {
            if (_query != value)
            {
                _query = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Whether recommendations are loading.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanLoad));
            }
        }
    }

    /// <summary>Whether the load action is available.</summary>
    public bool CanLoad => !IsLoading;

    /// <summary>Latest error text, if any.</summary>
    public string? ErrorText
    {
        get => _errorText;
        private set
        {
            if (_errorText != value)
            {
                _errorText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    /// <summary>Whether the panel has an error.</summary>
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

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
    public string Title => _localization["Ai.Advisor.Recommendations.Title"];

    /// <summary>Localized query placeholder.</summary>
    public string QueryWatermark => _localization["Ai.Advisor.Query.Placeholder"];

    /// <summary>Localized load button label.</summary>
    public string LoadLabel => _localization["Ai.Advisor.Recommendations.Load"];

    /// <summary>Localized empty-state text.</summary>
    public string EmptyText => _localization["Ai.Advisor.Recommendations.Empty"];

    /// <summary>Localized open-book label.</summary>
    public string OpenBookLabel => _localization["Ai.Advisor.OpenBook"];

    /// <summary>Icon path for the panel.</summary>
    public string IconPath => _iconPath;

    /// <summary>Loads recommendation cards.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorText = null;
        StatusText = _localization["Ai.Advisor.Status.Loading"];
        try
        {
            IReadOnlyList<RecommendationCard> cards = await _advisor.GetRecommendationsAsync(
                new RecommendationQuery(string.IsNullOrWhiteSpace(Query) ? _localization["Ai.Advisor.Query.Default"] : Query),
                new RecommendationGenerationOptions(AiPrivacyTier.MetadataOnly, "openai", "gpt-test"),
                cancellationToken).ConfigureAwait(false);

            Recommendations.Clear();
            foreach (RecommendationCard card in cards)
            {
                Recommendations.Add(new RecommendationCardViewModel(card, _localization));
            }

            StatusText = string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                _localization["Ai.Advisor.Status.RecommendationsLoadedFormat"],
                Recommendations.Count);
        }
        catch (Exception ex) when (ex is AiDisabledException or AdvisorParseException or NotImplementedException)
        {
            ErrorText = string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                _localization["Ai.Advisor.ErrorFormat"],
                ex.Message);
            StatusText = ErrorText;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Opens the selected book detail.</summary>
    public Task OpenBookAsync(RecommendationCardViewModel card, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        return _navigation.OpenDetailAsync(card.BookId, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose() => _localization.CultureChanged -= OnCultureChanged;

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        foreach (RecommendationCardViewModel card in Recommendations)
        {
            card.RefreshLocalization();
        }

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(QueryWatermark));
        OnPropertyChanged(nameof(LoadLabel));
        OnPropertyChanged(nameof(EmptyText));
        OnPropertyChanged(nameof(OpenBookLabel));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>Display model for one recommendation card.</summary>
public sealed class RecommendationCardViewModel : INotifyPropertyChanged
{
    private readonly RecommendationCard _card;
    private readonly ILocalizationService _localization;
    private bool _isExplanationExpanded;

    /// <summary>Initializes a new instance of <see cref="RecommendationCardViewModel"/>.</summary>
    public RecommendationCardViewModel(RecommendationCard card, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(localization);

        _card = card;
        _localization = localization;
        ProvenanceItems = card.Explanation.ProvenanceItems
            .Select(item => new ProvenanceChipViewModel(item))
            .ToArray();
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Book identifier.</summary>
    public string BookId => _card.BookId.Value;

    /// <summary>One-based recommendation rank.</summary>
    public int Rank => _card.Rank;

    /// <summary>Localized rank label.</summary>
    public string RankText => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        _localization["Ai.Advisor.Recommendations.RankFormat"],
        Rank);

    /// <summary>Localized confidence band.</summary>
    public string ConfidenceText => _localization[$"Ai.Advisor.Confidence.{_card.Confidence.Label}"];

    /// <summary>Explanation summary.</summary>
    public string ExplanationSummary => _card.Explanation.Summary;

    /// <summary>Why-button label.</summary>
    public string WhyLabel => _localization["Ai.Advisor.Why"];

    /// <summary>Accessible card label.</summary>
    public string AccessibleLabel => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        _localization["Ai.Advisor.Recommendations.AccessibleFormat"],
        Rank,
        BookId,
        ConfidenceText);

    /// <summary>Provenance chips.</summary>
    public IReadOnlyList<ProvenanceChipViewModel> ProvenanceItems { get; }

    /// <summary>Whether provenance is expanded.</summary>
    public bool IsExplanationExpanded
    {
        get => _isExplanationExpanded;
        set
        {
            if (_isExplanationExpanded != value)
            {
                _isExplanationExpanded = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Raises localization-dependent properties.</summary>
    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(RankText));
        OnPropertyChanged(nameof(ConfidenceText));
        OnPropertyChanged(nameof(WhyLabel));
        OnPropertyChanged(nameof(AccessibleLabel));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>Display model for one provenance chip.</summary>
public sealed record ProvenanceChipViewModel(ProvenanceItem Provenance)
{
    /// <summary>Chip text.</summary>
    public string Text => $"{Provenance.MatchField}: {Provenance.FieldValue}";
}
