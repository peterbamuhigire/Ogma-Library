using System.ComponentModel;
using System.Runtime.CompilerServices;
using OgmaLibrary.Application;

namespace OgmaLibrary.App.ViewModels.Reader;

/// <summary>Phase 15 V2 scaffold for future split-view reading.</summary>
public sealed class SplitViewViewModel : INotifyPropertyChanged
{
    private readonly ILocalizationService _localization;

    /// <summary>Initializes a new instance of <see cref="SplitViewViewModel"/>.</summary>
    public SplitViewViewModel(ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(localization);
        _localization = localization;
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

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(LeftPaneText));
        OnPropertyChanged(nameof(RightPaneText));
        OnPropertyChanged(nameof(PlaceholderText));
        OnPropertyChanged(nameof(AccessibleLabel));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
