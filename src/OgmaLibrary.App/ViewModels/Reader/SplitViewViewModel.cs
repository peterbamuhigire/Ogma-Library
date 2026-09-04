using System.ComponentModel;
using System.Runtime.CompilerServices;
using OgmaLibrary.Application;

namespace OgmaLibrary.App.ViewModels.Reader;

/// <summary>Two-session split-view reader workspace.</summary>
public sealed class SplitViewViewModel : INotifyPropertyChanged
{
    private readonly ILocalizationService _localization;
    private readonly ReaderViewModel? _leftReader;
    private readonly ReaderViewModel? _rightReader;
    private string _referenceBookId = string.Empty;

    /// <summary>Initializes a new instance of <see cref="SplitViewViewModel"/>.</summary>
    public SplitViewViewModel(ILocalizationService localization)
        : this(localization, null, null)
    {
    }

    /// <summary>Initializes a split workspace with independent reader sessions.</summary>
    public SplitViewViewModel(
        ILocalizationService localization,
        ReaderViewModel? leftReader,
        ReaderViewModel? rightReader)
    {
        ArgumentNullException.ThrowIfNull(localization);
        _localization = localization;
        _leftReader = leftReader;
        _rightReader = rightReader;
        _localization.CultureChanged += OnCultureChanged;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Localized title.</summary>
    public string Title => _localization["SplitView.Title"];

    /// <summary>Localized left-pane placeholder.</summary>
    public string LeftPaneText => _localization["SplitView.LeftPane"];

    /// <summary>Localized right-pane placeholder.</summary>
    public string RightPaneText => _localization["SplitView.RightPane"];

    /// <summary>Localized V2 placeholder message.</summary>
    public string PlaceholderText => _localization["SplitView.Placeholder"];

    /// <summary>Accessibility label for the scaffold surface.</summary>
    public string AccessibleLabel => _localization["SplitView.AccessibleLabel"];

    /// <summary>Localized reference-reader action.</summary>
    public string OpenReferenceLabel => _localization["SplitView.OpenReference"];

    /// <summary>Primary reader session shown in the left pane.</summary>
    public ReaderViewModel? LeftReader => _leftReader;

    /// <summary>Independent reference reader session shown in the right pane.</summary>
    public ReaderViewModel? RightReader => _rightReader;

    /// <summary>Book ID entered for the reference reader.</summary>
    public string ReferenceBookId
    {
        get => _referenceBookId;
        set
        {
            string normalized = value ?? string.Empty;
            if (_referenceBookId != normalized)
            {
                _referenceBookId = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanOpenReference));
            }
        }
    }

    /// <summary>True when the reference reader can open the entered book ID.</summary>
    public bool CanOpenReference => _rightReader is not null &&
        !string.IsNullOrWhiteSpace(ReferenceBookId);

    /// <summary>Opens the selected reference book in the independent right session.</summary>
    public async Task OpenReferenceAsync(CancellationToken cancellationToken = default)
    {
        if (!CanOpenReference || _rightReader is null)
        {
            return;
        }

        await _rightReader.OpenAsync(ReferenceBookId.Trim(), null, cancellationToken)
            .ConfigureAwait(false);
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(LeftPaneText));
        OnPropertyChanged(nameof(RightPaneText));
        OnPropertyChanged(nameof(PlaceholderText));
        OnPropertyChanged(nameof(AccessibleLabel));
        OnPropertyChanged(nameof(OpenReferenceLabel));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
