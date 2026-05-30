using System.ComponentModel;
using System.Runtime.CompilerServices;
using OgmaLibrary.Application;

namespace OgmaLibrary.App.ViewModels;

/// <summary>
/// The view model for the main window skeleton. All visible text is resolved through
/// <see cref="ILocalizationService"/> so no string is hard-coded in the view, and the
/// bound text updates when the active culture changes (en/fr at MVP).
/// </summary>
public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly ILocalizationService _localization;

    /// <summary>Creates the view model with the localization service.</summary>
    /// <param name="localization">The localization service.</param>
    public MainWindowViewModel(ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(localization);
        _localization = localization;
        _localization.CultureChanged += (_, _) => RaiseAllChanged();
    }

    /// <summary>A design-time instance so the XAML previewer can render the window.</summary>
    public MainWindowViewModel()
        : this(new DesignLocalizationService())
    {
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The window title.</summary>
    public string Title => _localization["MainWindow.Title"];

    /// <summary>The product tagline shown under the title.</summary>
    public string Tagline => _localization["MainWindow.Tagline"];

    /// <summary>The empty-state heading shown before a library is chosen.</summary>
    public string EmptyStateHeading => _localization["MainWindow.EmptyState.Heading"];

    /// <summary>The empty-state body text.</summary>
    public string EmptyStateBody => _localization["MainWindow.EmptyState.Body"];

    /// <summary>The label of the primary "choose folder" action.</summary>
    public string ChooseFolderText => _localization["MainWindow.Action.ChooseFolder"];

    /// <summary>The status-bar text identifying this skeleton build.</summary>
    public string StatusText => _localization["MainWindow.Status.Skeleton"];

    /// <summary>Accessible label for the application logo icon.</summary>
    public string AppLogoLabel => _localization["Icon.ic_app_logo.Label"];

    /// <summary>Accessible label for the settings icon/button.</summary>
    public string SettingsLabel => _localization["Icon.ic_settings.Label"];

    /// <summary>Accessible label for the open-folder icon.</summary>
    public string LibFolderLabel => _localization["Icon.ic_lib_folder_open.Label"];

    private void RaiseAllChanged()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Tagline));
        OnPropertyChanged(nameof(EmptyStateHeading));
        OnPropertyChanged(nameof(EmptyStateBody));
        OnPropertyChanged(nameof(ChooseFolderText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(AppLogoLabel));
        OnPropertyChanged(nameof(SettingsLabel));
        OnPropertyChanged(nameof(LibFolderLabel));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>A minimal English localization used only by the design-time constructor.</summary>
    private sealed class DesignLocalizationService : ILocalizationService
    {
        public System.Globalization.CultureInfo CurrentCulture { get; } =
            System.Globalization.CultureInfo.GetCultureInfo("en");

        public event EventHandler? CultureChanged { add { } remove { } }

        public string this[string key] => key switch
        {
            "MainWindow.Title" => "Ogma Library",
            "MainWindow.Tagline" => "Your personal PDF library.",
            "MainWindow.EmptyState.Heading" => "Your library will appear here",
            "MainWindow.EmptyState.Body" => "Choose a folder of PDFs to begin.",
            "MainWindow.Action.ChooseFolder" => "Choose library folder",
            "MainWindow.Status.Skeleton" => "Skeleton build — Phase 02",
            "Icon.ic_app_logo.Label" => "Ogma Library logo",
            "Icon.ic_settings.Label" => "Settings",
            "Icon.ic_lib_folder_open.Label" => "Open library folder",
            _ => key,
        };

        public void SetCulture(string cultureName)
        {
        }
    }
}
