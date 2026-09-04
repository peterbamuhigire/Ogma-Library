using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
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
    private string? _answerText;
    private readonly IAdvisorFeedbackService? _feedbackService;
    private bool _feedbackConsent;
    private int _feedbackRating;
    private string? _feedbackStatusText;

    /// <summary>Initializes a new instance of <see cref="RecommendationPanelViewModel"/>.</summary>
    public RecommendationPanelViewModel(
        IAiAdvisorService advisor,
        IBookDetailNavigationService navigation,
        ILocalizationService localization,
        IAdvisorFeedbackService? feedbackService = null)
    {
        ArgumentNullException.ThrowIfNull(advisor);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(localization);

        _advisor = advisor;
        _navigation = navigation;
        _localization = localization;
        _feedbackService = feedbackService;
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
                OnPropertyChanged(nameof(CanAsk));
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

    /// <summary>Whether a local-evidence answer can be requested.</summary>
    public bool CanAsk => !IsLoading && !string.IsNullOrWhiteSpace(Query);

    /// <summary>Latest local-evidence answer, when one has been requested.</summary>
    public string? AnswerText
    {
        get => _answerText;
        private set
        {
            if (_answerText != value)
            {
                _answerText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasAnswer));
                OnPropertyChanged(nameof(IsFeedbackAvailable));
                OnPropertyChanged(nameof(CanSubmitFeedback));
            }
        }
    }

    /// <summary>Whether an answer is available for display.</summary>
    public bool HasAnswer => !string.IsNullOrWhiteSpace(AnswerText);

    /// <summary>Whether the configured local feedback store can receive feedback.</summary>
    public bool IsFeedbackAvailable => _feedbackService is not null && HasAnswer;

    /// <summary>Whether the user has explicitly consented to store feedback.</summary>
    public bool FeedbackConsent
    {
        get => _feedbackConsent;
        set
        {
            if (_feedbackConsent != value)
            {
                _feedbackConsent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSubmitFeedback));
            }
        }
    }

    /// <summary>Selected answer rating from one to five.</summary>
    public int FeedbackRating
    {
        get => _feedbackRating;
        private set
        {
            if (_feedbackRating != value)
            {
                _feedbackRating = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSubmitFeedback));
            }
        }
    }

    /// <summary>Whether the feedback form is complete and consented.</summary>
    public bool CanSubmitFeedback => IsFeedbackAvailable && FeedbackConsent && FeedbackRating is >= 1 and <= 5;

    /// <summary>Status of the most recent feedback action.</summary>
    public string? FeedbackStatusText
    {
        get => _feedbackStatusText;
        private set
        {
            if (_feedbackStatusText != value)
            {
                _feedbackStatusText = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Whether feedback status text is available to announce.</summary>
    public bool HasFeedbackStatus => !string.IsNullOrWhiteSpace(FeedbackStatusText);

    /// <summary>Local-evidence citations for the current answer.</summary>
    public ObservableCollection<AnswerCitationViewModel> AnswerCitations { get; } = [];

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

    /// <summary>Localized answer title.</summary>
    public string AnswerTitle => _localization["Ai.Advisor.Answer.Title"];

    /// <summary>Localized answer action label.</summary>
    public string AskLabel => _localization["Ai.Advisor.Answer.Ask"];

    /// <summary>Localized answer citation label.</summary>
    public string CitationLabel => _localization["Ai.Advisor.Answer.Citation"];

    /// <summary>Localized feedback consent label.</summary>
    public string FeedbackConsentLabel => _localization["Ai.Advisor.Feedback.Consent"];

    /// <summary>Localized feedback submit label.</summary>
    public string FeedbackSubmitLabel => _localization["Ai.Advisor.Feedback.Submit"];

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

    /// <summary>Answers the current question using local indexed evidence only.</summary>
    public async Task AskAsync(CancellationToken cancellationToken = default)
    {
        if (!CanAsk)
        {
            return;
        }

        IsLoading = true;
        ErrorText = null;
        AnswerText = null;
        AnswerCitations.Clear();
        StatusText = _localization["Ai.Advisor.Status.Loading"];
        try
        {
            AnswerResponse response = await _advisor.GetAnswerAsync(
                new AnswerRequest(Query.Trim(), maxCitations: 5, allowContentAwareTier: false),
                cancellationToken).ConfigureAwait(false);

            AnswerText = response.Answer;
            foreach (AnswerCitation citation in response.Citations)
            {
                AnswerCitations.Add(new AnswerCitationViewModel(citation, _localization));
            }

            StatusText = string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                _localization["Ai.Advisor.Status.AnswerLoadedFormat"],
                AnswerCitations.Count);
        }
        catch (Exception ex) when (ex is AiDisabledException or AdvisorParseException)
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

    /// <summary>Sets a bounded rating selected by the feedback controls.</summary>
    public void SetFeedbackRating(int rating)
    {
        if (rating is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(rating), rating, "Feedback rating must be between 1 and 5.");
        }

        FeedbackRating = rating;
    }

    /// <summary>Stores consented, privacy-minimized answer feedback.</summary>
    public async Task SubmitFeedbackAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSubmitFeedback || _feedbackService is null)
        {
            return;
        }

        string requestHash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(Query.Trim())));
        try
        {
            await _feedbackService.SubmitAsync(
                    new AdvisorFeedbackEntry(
                        $"feedback-{Guid.NewGuid():N}",
                        requestHash,
                        FeedbackRating,
                        ReasonCode: null,
                        SubmittedUtc: DateTimeOffset.UtcNow),
                    consentGranted: true,
                    cancellationToken)
                .ConfigureAwait(false);
            FeedbackStatusText = _localization["Ai.Advisor.Feedback.Saved"];
        }
        catch (AdvisorFeedbackConsentRequiredException)
        {
            FeedbackStatusText = _localization["Ai.Advisor.Feedback.ConsentRequired"];
        }
        catch (InvalidOperationException ex)
        {
            FeedbackStatusText = string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                _localization["Ai.Advisor.ErrorFormat"],
                ex.Message);
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
        OnPropertyChanged(nameof(AnswerTitle));
        OnPropertyChanged(nameof(AskLabel));
        OnPropertyChanged(nameof(CitationLabel));
        OnPropertyChanged(nameof(FeedbackConsentLabel));
        OnPropertyChanged(nameof(FeedbackSubmitLabel));
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

/// <summary>Display model for one grounded local-evidence citation.</summary>
public sealed class AnswerCitationViewModel
{
    private readonly AnswerCitation _citation;
    private readonly ILocalizationService _localization;

    /// <summary>Initializes a citation display model.</summary>
    public AnswerCitationViewModel(AnswerCitation citation, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(citation);
        ArgumentNullException.ThrowIfNull(localization);
        _citation = citation;
        _localization = localization;
    }

    /// <summary>Short source and location label.</summary>
    public string CitationText => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        _localization["Ai.Advisor.Answer.CitationFormat"],
        _citation.SourceLabel ?? _localization["Ai.Advisor.Answer.LocalSource"],
        _citation.PageNumber?.ToString(System.Globalization.CultureInfo.CurrentCulture) ?? "—");

    /// <summary>Evidence excerpt.</summary>
    public string RelevantText => _citation.RelevantText;

    /// <summary>Uncertainty note, when the source has a limitation.</summary>
    public string? UncertaintyText => _citation.UncertaintyLabel;

    /// <summary>Whether the citation carries an uncertainty note.</summary>
    public bool HasUncertainty => !string.IsNullOrWhiteSpace(UncertaintyText);

    /// <summary>Accessible citation label.</summary>
    public string AccessibleLabel => $"{CitationText}: {RelevantText}";
}
