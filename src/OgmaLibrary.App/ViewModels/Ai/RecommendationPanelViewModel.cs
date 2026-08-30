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
    private AdvisorIntent? _interpretedIntent;
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
                _interpretedIntent = string.IsNullOrWhiteSpace(value)
                    ? null
                    : AdvisorIntentParser.Parse(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(InterpretedIntentText));
                OnPropertyChanged(nameof(HasInterpretedIntent));
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

    /// <summary>Whether the current request has an interpreted intent.</summary>
    public bool HasInterpretedIntent => _interpretedIntent is not null;

    /// <summary>Plain-language interpreted constraints shown before generation.</summary>
    public string InterpretedIntentText
    {
        get
        {
            if (_interpretedIntent is null)
            {
                return _localization["Ai.Advisor.Intent.None"];
            }

            List<string> parts = [];
            if (_interpretedIntent.PositiveTerms.Count > 0)
            {
                parts.Add($"Topics: {string.Join(", ", _interpretedIntent.PositiveTerms.Take(5))}");
            }

            if (_interpretedIntent.NegativeTerms.Count > 0)
            {
                parts.Add($"Avoids: {string.Join(", ", _interpretedIntent.NegativeTerms.Take(5))}");
            }

            if (_interpretedIntent.Difficulty is not null)
            {
                parts.Add($"Level: {_interpretedIntent.Difficulty}");
            }

            if (_interpretedIntent.Length != AdvisorLengthPreference.Any)
            {
                parts.Add($"Length: {_interpretedIntent.Length}");
            }

            if (_interpretedIntent.MoodTerms.Count > 0)
            {
                parts.Add($"Mood: {string.Join(", ", _interpretedIntent.MoodTerms)}");
            }

            if (_interpretedIntent.ComparisonReference is not null)
            {
                parts.Add($"Like: {_interpretedIntent.ComparisonReference}");
            }

            if (_interpretedIntent.IsBroadDiscovery)
            {
                parts.Add("Broad discovery");
            }

            return parts.Count == 0 ? _localization["Ai.Advisor.Intent.None"] : string.Join(" · ", parts);
        }
    }

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
        OnPropertyChanged(nameof(InterpretedIntentText));
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

    /// <summary>Whether this card has an uncertainty note.</summary>
    public bool HasUncertainty => _card.Explanation.ProvenanceItems.Any(item => !string.IsNullOrWhiteSpace(item.UncertaintyLabel));

    /// <summary>Evidence limitation shown alongside the card.</summary>
    public string UncertaintyText => string.Join(" ", _card.Explanation.ProvenanceItems
        .Where(item => !string.IsNullOrWhiteSpace(item.UncertaintyLabel))
        .Select(item => item.UncertaintyLabel));

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
        OnPropertyChanged(nameof(UncertaintyText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>Display model for one provenance chip.</summary>
public sealed record ProvenanceChipViewModel(ProvenanceItem Provenance)
{
    /// <summary>Chip text.</summary>
    public string Text => $"{Provenance.SourceLabel ?? Provenance.MatchField.ToString()}: {Provenance.FieldValue}";
}
