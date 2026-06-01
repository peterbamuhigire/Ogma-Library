using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OgmaLibrary.App.Icons;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.App.ViewModels.Ai;

/// <summary>View model for generated AI reading plans.</summary>
public sealed class ReadingPlanViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IAiAdvisorService _advisor;
    private readonly ICatalogueReadModel _catalogue;
    private readonly IBookDetailNavigationService _navigation;
    private readonly ILocalizationService _localization;
    private readonly string _iconPath = IconCatalog.GetAvaresPath("ic_ai_advisor") ?? string.Empty;
    private string _goal = string.Empty;
    private bool _isLoading;
    private string? _errorText;
    private string _statusText;

    /// <summary>Initializes a new instance of <see cref="ReadingPlanViewModel"/>.</summary>
    public ReadingPlanViewModel(
        IAiAdvisorService advisor,
        ICatalogueReadModel catalogue,
        IBookDetailNavigationService navigation,
        ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(advisor);
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(localization);

        _advisor = advisor;
        _catalogue = catalogue;
        _navigation = navigation;
        _localization = localization;
        _statusText = _localization["Ai.Advisor.Status.Ready"];
        _localization.CultureChanged += OnCultureChanged;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Plan steps.</summary>
    public ObservableCollection<PlanStepViewModel> Steps { get; } = [];

    /// <summary>Plan checkpoints.</summary>
    public ObservableCollection<CheckpointViewModel> Checkpoints { get; } = [];

    /// <summary>Goal text.</summary>
    public string Goal
    {
        get => _goal;
        set
        {
            if (_goal != value)
            {
                _goal = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Whether plan generation is loading.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanGenerate));
            }
        }
    }

    /// <summary>Whether the generate action is available.</summary>
    public bool CanGenerate => !IsLoading;

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

    /// <summary>Whether the view has an error.</summary>
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
    public string Title => _localization["Ai.Advisor.Plan.Title"];

    /// <summary>Localized goal placeholder.</summary>
    public string GoalWatermark => _localization["Ai.Advisor.Plan.Goal.Placeholder"];

    /// <summary>Localized generate label.</summary>
    public string GenerateLabel => _localization["Ai.Advisor.Plan.Generate"];

    /// <summary>Localized regenerate label.</summary>
    public string RegenerateLabel => _localization["Ai.Advisor.Plan.Regenerate"];

    /// <summary>Localized open-book label.</summary>
    public string OpenBookLabel => _localization["Ai.Advisor.OpenBook"];

    /// <summary>Localized empty-state text.</summary>
    public string EmptyText => _localization["Ai.Advisor.Plan.Empty"];

    /// <summary>Icon path for the panel.</summary>
    public string IconPath => _iconPath;

    /// <summary>Generates a reading plan.</summary>
    public async Task GenerateAsync(CancellationToken cancellationToken = default)
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
            ReadingPlan plan = await _advisor.GetReadingPlanAsync(
                new ReadingPlanRequest(string.IsNullOrWhiteSpace(Goal) ? _localization["Ai.Advisor.Plan.Goal.Default"] : Goal),
                new RecommendationGenerationOptions(AiPrivacyTier.MetadataOnly, "openai", "gpt-test"),
                cancellationToken).ConfigureAwait(false);
            Goal = plan.Goal;
            await LoadPlanAsync(plan, cancellationToken).ConfigureAwait(false);
            StatusText = string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                _localization["Ai.Advisor.Status.PlanLoadedFormat"],
                Steps.Count);
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

    /// <summary>Opens a book from the plan.</summary>
    public Task OpenBookAsync(PlanStepViewModel step, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(step);
        return _navigation.OpenDetailAsync(step.BookId, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose() => _localization.CultureChanged -= OnCultureChanged;

    private async Task LoadPlanAsync(ReadingPlan plan, CancellationToken cancellationToken)
    {
        Steps.Clear();
        for (int i = 0; i < plan.Steps.Count; i++)
        {
            ReadingPlanStep step = plan.Steps[i];
            BookDetailProjection? detail = await _catalogue.GetBookDetailAsync(step.BookId.Value, cancellationToken).ConfigureAwait(false);
            Steps.Add(new PlanStepViewModel(i + 1, step, detail?.Title ?? step.BookId.Value, _localization));
        }

        Checkpoints.Clear();
        foreach (Checkpoint checkpoint in plan.Checkpoints)
        {
            Checkpoints.Add(new CheckpointViewModel(checkpoint));
        }
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        foreach (PlanStepViewModel step in Steps)
        {
            step.RefreshLocalization();
        }

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(GoalWatermark));
        OnPropertyChanged(nameof(GenerateLabel));
        OnPropertyChanged(nameof(RegenerateLabel));
        OnPropertyChanged(nameof(OpenBookLabel));
        OnPropertyChanged(nameof(EmptyText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>Display model for one reading-plan step.</summary>
public sealed class PlanStepViewModel : INotifyPropertyChanged
{
    private readonly ReadingPlanStep _step;
    private readonly ILocalizationService _localization;

    /// <summary>Initializes a new instance of <see cref="PlanStepViewModel"/>.</summary>
    public PlanStepViewModel(int rank, ReadingPlanStep step, string bookTitle, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentException.ThrowIfNullOrWhiteSpace(bookTitle);
        ArgumentNullException.ThrowIfNull(localization);
        Rank = rank;
        _step = step;
        BookTitle = bookTitle;
        _localization = localization;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>One-based step rank.</summary>
    public int Rank { get; }

    /// <summary>Book identifier.</summary>
    public string BookId => _step.BookId.Value;

    /// <summary>Book title.</summary>
    public string BookTitle { get; }

    /// <summary>Step rationale.</summary>
    public string Rationale => _step.Rationale;

    /// <summary>Localized difficulty label.</summary>
    public string DifficultyText => _localization[$"Ai.Advisor.Difficulty.{_step.Difficulty}"];

    /// <summary>Localized estimated reading time.</summary>
    public string EstimatedReadingText => _step.EstimatedReadingDays.HasValue
        ? string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            _localization["Ai.Advisor.Plan.EstimatedDaysFormat"],
            _step.EstimatedReadingDays.Value)
        : _localization["Ai.Advisor.Plan.EstimatedDaysUnknown"];

    /// <summary>Accessible step label.</summary>
    public string AccessibleLabel => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        _localization["Ai.Advisor.Plan.Step.AccessibleFormat"],
        Rank,
        BookTitle,
        DifficultyText);

    /// <summary>Raises localization-dependent properties.</summary>
    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(DifficultyText));
        OnPropertyChanged(nameof(EstimatedReadingText));
        OnPropertyChanged(nameof(AccessibleLabel));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>Display model for one reading-plan checkpoint.</summary>
public sealed record CheckpointViewModel(Checkpoint Checkpoint)
{
    /// <summary>Zero-based step index after which this checkpoint appears.</summary>
    public int AfterStepIndex => Checkpoint.AfterStepIndex;

    /// <summary>Checkpoint description.</summary>
    public string Description => Checkpoint.Description;
}
