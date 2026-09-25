using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.App.Settings;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Navigation;

namespace OgmaLibrary.App.ViewModels.Settings;

/// <summary>The sections of the Settings destination, in display order (Sept-23 Phase 08).</summary>
public enum SettingsSection
{
    /// <summary>Library folders (D-03).</summary>
    Library,

    /// <summary>Theme and density.</summary>
    Appearance,

    /// <summary>Interface language.</summary>
    Language,

    /// <summary>Online book-detail providers.</summary>
    OnlineServices,

    /// <summary>The 3D shelf preview and the classroom Host.</summary>
    Features,

    /// <summary>AI state and the Privacy Center mount point (Phase 15).</summary>
    Privacy,

    /// <summary>Text recognition (OCR) policy mount point (Phase 17).</summary>
    TextRecognition,

    /// <summary>Version, data location, capability probe, logs and diagnostics export.</summary>
    Diagnostics,
}

/// <summary>Where the app keeps its data, shown in Diagnostics (Sept-23 Phase 08, 8.9).</summary>
/// <param name="Version">The application version.</param>
/// <param name="DataDirectory">The Ogma data folder.</param>
/// <param name="LogsDirectory">The log folder.</param>
/// <param name="AssetDirectory">The derived covers and caches folder (D-04).</param>
public sealed record SettingsEnvironmentInfo(
    string Version,
    string DataDirectory,
    string LogsDirectory,
    string AssetDirectory);

/// <summary>One entry in the Settings section list.</summary>
public sealed class SettingsSectionItem : INotifyPropertyChanged
{
    private string _label = string.Empty;

    /// <summary>Initializes a new instance of the <see cref="SettingsSectionItem"/> class.</summary>
    /// <param name="section">The section.</param>
    /// <param name="labelKey">The localization key of its label.</param>
    /// <param name="routeId">The stable route and automation identifier.</param>
    public SettingsSectionItem(SettingsSection section, string labelKey, string routeId)
    {
        Section = section;
        LabelKey = labelKey;
        RouteId = routeId;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The section.</summary>
    public SettingsSection Section { get; }

    /// <summary>The localization key of the label.</summary>
    public string LabelKey { get; }

    /// <summary>The route section id (for example <c>appearance</c>).</summary>
    public string RouteId { get; }

    /// <summary>The stable UI Automation id.</summary>
    public string AutomationId => "Settings.Section." + RouteId;

    /// <summary>The visible, localised label (also the accessible name).</summary>
    public string Label
    {
        get => _label;
        internal set
        {
            if (!string.Equals(_label, value, StringComparison.Ordinal))
            {
                _label = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
            }
        }
    }
}

/// <summary>
/// A user switch for an optional capability (Sept-23 Phase 08, 8.2/8.3). When an environment
/// override is active the switch is read-only and explains why.
/// </summary>
public sealed class CapabilityToggleViewModel : INotifyPropertyChanged
{
    private readonly ICapabilitySettings _capabilities;
    private readonly ILocalizationService _localization;
    private readonly Func<bool, Task> _setPreference;
    private readonly string _titleKey;
    private readonly string _helpKey;

    /// <summary>Initializes a new instance of the <see cref="CapabilityToggleViewModel"/> class.</summary>
    /// <param name="flag">The capability.</param>
    /// <param name="titleKey">Localization key of the label.</param>
    /// <param name="helpKey">Localization key of the help line.</param>
    /// <param name="capabilities">The capability resolver.</param>
    /// <param name="localization">The localization service.</param>
    /// <param name="setPreference">Persists the user's choice.</param>
    public CapabilityToggleViewModel(
        UserCapability flag,
        string titleKey,
        string helpKey,
        ICapabilitySettings capabilities,
        ILocalizationService localization,
        Func<bool, Task> setPreference)
    {
        Flag = flag;
        _titleKey = titleKey;
        _helpKey = helpKey;
        _capabilities = capabilities;
        _localization = localization;
        _setPreference = setPreference;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The capability.</summary>
    public UserCapability Flag { get; }

    /// <summary>The stable UI Automation id of the switch.</summary>
    public string AutomationId => "Settings.Toggle." + Flag;

    /// <summary>The label (also the accessible name).</summary>
    public string Title => _localization[_titleKey];

    /// <summary>What the capability does and what it needs.</summary>
    public string Help => _localization[_helpKey];

    /// <summary>Whether the capability is effectively on. Setting it persists the user's choice.</summary>
    public bool IsOn
    {
        get => _capabilities.IsEnabled(Flag);
        set
        {
            if (value != IsOn && CanChange)
            {
                UiActions.Run(() => _setPreference(value), "settings.capability." + Flag);
            }
        }
    }

    /// <summary>False when an environment override manages the value.</summary>
    public bool CanChange => !_capabilities.IsManagedByEnvironment(Flag);

    /// <summary>True when an environment override manages the value.</summary>
    public bool IsManaged => !CanChange;

    /// <summary>The explanation shown when the value is managed by the environment.</summary>
    public string ManagedText => IsManaged
        ? string.Format(
            CultureInfo.CurrentCulture,
            _localization["Settings.Managed.Format"],
            _capabilities.EnvironmentVariableName(Flag))
        : string.Empty;

    /// <summary>"On" or "Off", for the status line beside the switch.</summary>
    public string StateText => _localization[IsOn ? "Settings.Toggle.On" : "Settings.Toggle.Off"];

    /// <summary>Re-reads the state and labels.</summary>
    public void Refresh()
    {
        foreach (string name in new[]
                 {
                     nameof(Title), nameof(Help), nameof(IsOn), nameof(CanChange), nameof(IsManaged),
                     nameof(ManagedText), nameof(StateText),
                 })
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}

/// <summary>
/// The Settings destination (Sept-23 Phase 08, K16): one place for every user-controllable
/// preference and capability, persisted per user and applied live. Environment variables remain
/// administrator and test overrides and are shown as managed.
/// </summary>
public sealed class SettingsViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ILocalizationService _localization;
    private readonly UserPreferencesController _preferences;
    private readonly ICapabilitySettings _capabilities;
    private readonly SettingsEnvironmentInfo _environment;
    private SettingsSectionItem _selectedSection;
    private string? _diagnosticsStatus;
    private object? _textRecognitionOptions;

    /// <summary>Initializes a new instance of the <see cref="SettingsViewModel"/> class.</summary>
    /// <param name="localization">The localization service.</param>
    /// <param name="preferences">The shared preference controller.</param>
    /// <param name="capabilities">The capability resolver.</param>
    /// <param name="environment">Version and folder locations for Diagnostics.</param>
    /// <param name="libraryFolders">The Library folders panel (D-03), when composed.</param>
    public SettingsViewModel(
        ILocalizationService localization,
        UserPreferencesController preferences,
        ICapabilitySettings capabilities,
        SettingsEnvironmentInfo environment,
        LibraryFoldersViewModel? libraryFolders = null)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(environment);
        _localization = localization;
        _preferences = preferences;
        _capabilities = capabilities;
        _environment = environment;
        LibraryFolders = libraryFolders;

        Sections =
        [
            new(SettingsSection.Library, "Settings.Section.Library", "library"),
            new(SettingsSection.Appearance, "Settings.Section.Appearance", "appearance"),
            new(SettingsSection.Language, "Settings.Section.Language", "language"),
            new(SettingsSection.OnlineServices, "Settings.Section.Online", "online"),
            new(SettingsSection.Features, "Settings.Section.Features", "features"),
            new(SettingsSection.Privacy, "Settings.Section.Privacy", "privacy"),
            new(SettingsSection.TextRecognition, "Settings.Section.Ocr", "ocr"),
            new(SettingsSection.Diagnostics, "Settings.Section.Diagnostics", "diagnostics"),
        ];
        _selectedSection = Sections[0];

        MetadataProviders = Toggle(UserCapability.MetadataProviders, "Settings.Online.Metadata.Title", "Settings.Online.Metadata.Help");
        Shelf3D = Toggle(UserCapability.ThreeDimensionalShelf, "Settings.Features.Shelf3D.Title", "Settings.Features.Shelf3D.Help");
        ClassroomHost = Toggle(UserCapability.ClassroomHost, "Settings.Features.Classroom.Title", "Settings.Features.Classroom.Help");

        _localization.CultureChanged += OnCultureChanged;
        _capabilities.Changed += OnCapabilitiesChanged;
        _preferences.Changed += OnPreferencesChanged;
        RefreshLabels();
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The section list in display order.</summary>
    public ObservableCollection<SettingsSectionItem> Sections { get; }

    /// <summary>The section shown on the right.</summary>
    public SettingsSectionItem SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (value is not null && !ReferenceEquals(value, _selectedSection))
            {
                _selectedSection = value;
                RaiseSectionChanged();
            }
        }
    }

    /// <summary>The Library folders panel, when composed.</summary>
    public LibraryFoldersViewModel? LibraryFolders { get; }

    /// <summary>The online metadata provider switch.</summary>
    public CapabilityToggleViewModel MetadataProviders { get; }

    /// <summary>The 3D shelf preview switch.</summary>
    public CapabilityToggleViewModel Shelf3D { get; }

    /// <summary>The classroom Host switch.</summary>
    public CapabilityToggleViewModel ClassroomHost { get; }

    /// <summary>Exports the redacted diagnostics bundle (bound by the application).</summary>
    public Func<Task>? ExportDiagnostics { get; set; }

    /// <summary>Opens a folder in the platform file manager (substituted by tests).</summary>
    public Func<string, bool> OpenFolder { get; set; } = FolderLauncher.TryOpen;

    /// <summary>
    /// Phase 17 extension point: the text-recognition (OCR) policy options. When the OCR phase
    /// supplies a view model with a matching data template, it appears in the Text recognition
    /// section; until then the section states the current behaviour and no control is faked.
    /// </summary>
    public object? TextRecognitionOptions
    {
        get => _textRecognitionOptions;
        set
        {
            _textRecognitionOptions = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasTextRecognitionOptions));
            OnPropertyChanged(nameof(IsTextRecognitionPendingVisible));
        }
    }

    /// <summary>Whether OCR options were supplied.</summary>
    public bool HasTextRecognitionOptions => _textRecognitionOptions is not null;

    /// <summary>Whether to show the "options will appear here" line.</summary>
    public bool IsTextRecognitionPendingVisible => _textRecognitionOptions is null;

    // ── Section visibility ─────────────────────────────────────────────────────

    /// <summary>The Library section is shown.</summary>
    public bool IsLibrarySection => _selectedSection.Section == SettingsSection.Library;

    /// <summary>The Appearance section is shown.</summary>
    public bool IsAppearanceSection => _selectedSection.Section == SettingsSection.Appearance;

    /// <summary>The Language section is shown.</summary>
    public bool IsLanguageSection => _selectedSection.Section == SettingsSection.Language;

    /// <summary>The Online services section is shown.</summary>
    public bool IsOnlineSection => _selectedSection.Section == SettingsSection.OnlineServices;

    /// <summary>The Optional features section is shown.</summary>
    public bool IsFeaturesSection => _selectedSection.Section == SettingsSection.Features;

    /// <summary>The AI and privacy section is shown.</summary>
    public bool IsPrivacySection => _selectedSection.Section == SettingsSection.Privacy;

    /// <summary>The Text recognition section is shown.</summary>
    public bool IsTextRecognitionSection => _selectedSection.Section == SettingsSection.TextRecognition;

    /// <summary>The Diagnostics section is shown.</summary>
    public bool IsDiagnosticsSection => _selectedSection.Section == SettingsSection.Diagnostics;

    /// <summary>The selected section's heading.</summary>
    public string SectionTitle => _selectedSection.Label;

    /// <summary>The accessible name of the section list.</summary>
    public string SectionsLabel => _localization["Settings.Sections.Label"];

    // ── Library ────────────────────────────────────────────────────────────────

    /// <summary>Library section introduction.</summary>
    public string LibraryIntro => _localization["Settings.Library.Intro"];

    /// <summary>Whether the folder panel is available.</summary>
    public bool HasLibraryFolders => LibraryFolders is not null;

    /// <summary>Shown when the folder panel is not available.</summary>
    public string LibraryUnavailableText => _localization["Settings.Library.Unavailable"];

    // ── Appearance ─────────────────────────────────────────────────────────────

    /// <summary>Theme label.</summary>
    public string ThemeLabel => _localization["Settings.Appearance.Theme"];

    /// <summary>Theme help line.</summary>
    public string ThemeHelp => _localization["Settings.Appearance.Theme.Help"];

    /// <summary>Light option label.</summary>
    public string ThemeLightText => _localization["Settings.Appearance.Theme.Light"];

    /// <summary>Dark option label.</summary>
    public string ThemeDarkText => _localization["Settings.Appearance.Theme.Dark"];

    /// <summary>System option label.</summary>
    public string ThemeSystemText => _localization["Settings.Appearance.Theme.System"];

    /// <summary>Light theme selected.</summary>
    public bool IsThemeLight
    {
        get => _preferences.Current.Theme == UserTheme.Light;
        set => SetTheme(value, UserTheme.Light);
    }

    /// <summary>Dark theme selected.</summary>
    public bool IsThemeDark
    {
        get => _preferences.Current.Theme == UserTheme.Dark;
        set => SetTheme(value, UserTheme.Dark);
    }

    /// <summary>System theme selected.</summary>
    public bool IsThemeSystem
    {
        get => _preferences.Current.Theme == UserTheme.System;
        set => SetTheme(value, UserTheme.System);
    }

    /// <summary>Density label.</summary>
    public string DensityLabel => _localization["Settings.Appearance.Density"];

    /// <summary>Density help line.</summary>
    public string DensityHelp => _localization["Settings.Appearance.Density.Help"];

    /// <summary>Comfortable option label.</summary>
    public string DensityComfortableText => _localization["Settings.Appearance.Density.Comfortable"];

    /// <summary>Compact option label.</summary>
    public string DensityCompactText => _localization["Settings.Appearance.Density.Compact"];

    /// <summary>Comfortable density selected.</summary>
    public bool IsDensityComfortable
    {
        get => _preferences.Current.Density == UserDensity.Comfortable;
        set => SetDensity(value, UserDensity.Comfortable);
    }

    /// <summary>Compact density selected.</summary>
    public bool IsDensityCompact
    {
        get => _preferences.Current.Density == UserDensity.Compact;
        set => SetDensity(value, UserDensity.Compact);
    }

    // ── Language ───────────────────────────────────────────────────────────────

    /// <summary>Language label.</summary>
    public string LanguageLabel => _localization["Settings.Language.Title"];

    /// <summary>Language help line.</summary>
    public string LanguageHelp => _localization["Settings.Language.Help"];

    /// <summary>"Use the system language (English)".</summary>
    public string LanguageSystemText => string.Format(
        CultureInfo.CurrentCulture,
        _localization["Settings.Language.SystemFormat"],
        Endonym(_preferences.SystemLanguage));

    /// <summary>The English option (always in English).</summary>
    public string LanguageEnglishText => _localization["Settings.Language.English"];

    /// <summary>The French option (always in French).</summary>
    public string LanguageFrenchText => _localization["Settings.Language.French"];

    /// <summary>Follow the operating-system language.</summary>
    public bool IsLanguageSystem
    {
        get => _preferences.Current.Culture is null;
        set => SetCulture(value, null);
    }

    /// <summary>English chosen.</summary>
    public bool IsLanguageEnglish
    {
        get => string.Equals(_preferences.Current.Culture, "en", StringComparison.Ordinal);
        set => SetCulture(value, "en");
    }

    /// <summary>French chosen.</summary>
    public bool IsLanguageFrench
    {
        get => string.Equals(_preferences.Current.Culture, "fr", StringComparison.Ordinal);
        set => SetCulture(value, "fr");
    }

    // ── Online services ────────────────────────────────────────────────────────

    /// <summary>"What is shared" heading.</summary>
    public string DisclosureTitle => _localization["Settings.Online.Disclosure.Title"];

    /// <summary>What is sent and to which hosts.</summary>
    public string DisclosureSent => _localization["Settings.Online.Disclosure.Sent"];

    /// <summary>What is never sent.</summary>
    public string DisclosureNotSent => _localization["Settings.Online.Disclosure.NotSent"];

    /// <summary>No connection while off.</summary>
    public string DisclosureOff => _localization["Settings.Online.Disclosure.Off"];

    // ── Optional features ──────────────────────────────────────────────────────

    /// <summary>Features introduction.</summary>
    public string FeaturesIntro => _localization["Settings.Features.Intro"];

    // ── AI and privacy ─────────────────────────────────────────────────────────

    /// <summary>"Current state" label.</summary>
    public string PrivacyStatusTitle => _localization["Settings.Privacy.Status.Title"];

    /// <summary>The honest current AI state.</summary>
    public string PrivacyStatusText => _localization[_capabilities.IsAiConfigured
        ? "Settings.Privacy.Status.On"
        : "Settings.Privacy.Status.Off"];

    /// <summary>Local features never need AI.</summary>
    public string PrivacyLocalText => _localization["Settings.Privacy.Local"];

    /// <summary>Where provider set-up will appear (the Phase 15 Privacy Center mount point).</summary>
    public string PrivacyNextText => _localization["Settings.Privacy.Next"];

    // ── Text recognition ───────────────────────────────────────────────────────

    /// <summary>Current OCR behaviour.</summary>
    public string TextRecognitionBody => _localization["Settings.Ocr.Body"];

    /// <summary>Where OCR options will appear.</summary>
    public string TextRecognitionPendingText => _localization["Settings.Ocr.Pending"];

    // ── Diagnostics and about ──────────────────────────────────────────────────

    /// <summary>Version label.</summary>
    public string VersionLabel => _localization["Settings.Diagnostics.Version"];

    /// <summary>The application version.</summary>
    public string VersionText => _environment.Version;

    /// <summary>Data folder label.</summary>
    public string DataFolderLabel => _localization["Settings.Diagnostics.DataFolder"];

    /// <summary>Data folder help line.</summary>
    public string DataFolderHelp => _localization["Settings.Diagnostics.DataFolder.Help"];

    /// <summary>The data folder path.</summary>
    public string DataFolderPath => _environment.DataDirectory;

    /// <summary>Covers and caches label.</summary>
    public string AssetFolderLabel => _localization["Settings.Diagnostics.AssetFolder"];

    /// <summary>The derived covers and caches folder.</summary>
    public string AssetFolderPath => _environment.AssetDirectory;

    /// <summary>Log folder label.</summary>
    public string LogsFolderLabel => _localization["Settings.Diagnostics.LogsFolder"];

    /// <summary>The log folder path.</summary>
    public string LogsFolderPath => _environment.LogsDirectory;

    /// <summary>Open data folder button.</summary>
    public string OpenDataFolderText => _localization["Settings.Diagnostics.OpenDataFolder"];

    /// <summary>Open log folder button.</summary>
    public string OpenLogsText => _localization["Settings.Diagnostics.OpenLogs"];

    /// <summary>Export diagnostics button.</summary>
    public string ExportText => _localization["Settings.Diagnostics.Export"];

    /// <summary>Export help line.</summary>
    public string ExportHelp => _localization["Settings.Diagnostics.Export.Help"];

    /// <summary>Capability probe heading.</summary>
    public string CapabilitiesTitle => _localization["Settings.Diagnostics.Capabilities"];

    /// <summary>The capability probe in plain words.</summary>
    public IReadOnlyList<CapabilityRow> CapabilityRows =>
    [
        Row("Settings.Capability.Ai", _capabilities.IsAiConfigured),
        Row("Settings.Capability.Classroom", _capabilities.IsClassroomAvailable),
        Row("Settings.Capability.Shelf3D", _capabilities.IsShelf3DAvailable),
        Row("Settings.Capability.Metadata", _capabilities.AreMetadataProvidersEnabled),
    ];

    /// <summary>The last diagnostics action result, if any.</summary>
    public string? DiagnosticsStatus
    {
        get => _diagnosticsStatus;
        private set
        {
            _diagnosticsStatus = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasDiagnosticsStatus));
        }
    }

    /// <summary>Whether a diagnostics status line is shown.</summary>
    public bool HasDiagnosticsStatus => !string.IsNullOrEmpty(_diagnosticsStatus);

    /// <summary>Selects a section from a route id (for example <c>ai</c> or <c>classroom</c>).</summary>
    /// <param name="routeSection">The route section; null or unknown keeps the current section.</param>
    public void SelectSection(string? routeSection)
    {
        string? id = routeSection?.Trim().ToLowerInvariant() switch
        {
            null or "" => null,
            "ai" or "privacy" => "privacy",
            "classroom" or "shelf3d" or "3d" or "features" => "features",
            "metadata" or "online" => "online",
            "folders" or "library" => "library",
            var other => other,
        };
        if (id is not null && Sections.FirstOrDefault(item => item.RouteId == id) is { } match)
        {
            SelectedSection = match;
        }
    }

    /// <summary>Opens the data folder.</summary>
    public void OpenDataFolder() => Open(_environment.DataDirectory);

    /// <summary>Opens the log folder.</summary>
    public void OpenLogsFolder() => Open(_environment.LogsDirectory);

    /// <summary>Exports redacted diagnostics (no book content).</summary>
    /// <returns>A task that completes when the export finishes.</returns>
    public Task ExportDiagnosticsAsync() => ExportDiagnostics?.Invoke() ?? Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose()
    {
        _localization.CultureChanged -= OnCultureChanged;
        _capabilities.Changed -= OnCapabilitiesChanged;
        _preferences.Changed -= OnPreferencesChanged;
    }

    // Language names are shown in their own language in every interface language.
    private string Endonym(string language) =>
        _localization[string.Equals(language, "fr", StringComparison.Ordinal)
            ? "Settings.Language.French"
            : "Settings.Language.English"];

    private CapabilityToggleViewModel Toggle(UserCapability flag, string titleKey, string helpKey) =>
        new(flag, titleKey, helpKey, _capabilities, _localization, value => _preferences.UpdateAsync(current => flag switch
        {
            UserCapability.MetadataProviders => current with { EnableMetadataProviders = value },
            UserCapability.ThreeDimensionalShelf => current with { EnableThreeDimensionalShelf = value },
            UserCapability.ClassroomHost => current with { EnableClassroomHost = value },
            _ => current,
        }));

    private CapabilityRow Row(string key, bool available) => new(
        _localization[key],
        _localization[available ? "Settings.Capability.Available" : "Settings.Capability.Unavailable"],
        _localization[key + (available ? ".On" : ".Off")],
        available);

    private void SetTheme(bool selected, UserTheme theme)
    {
        if (selected && _preferences.Current.Theme != theme)
        {
            UiActions.Run(() => _preferences.UpdateAsync(current => current with { Theme = theme }), "settings.theme");
        }
    }

    private void SetDensity(bool selected, UserDensity density)
    {
        if (selected && _preferences.Current.Density != density)
        {
            UiActions.Run(() => _preferences.UpdateAsync(current => current with { Density = density }), "settings.density");
        }
    }

    private void SetCulture(bool selected, string? culture)
    {
        if (selected && !string.Equals(_preferences.Current.Culture, culture, StringComparison.Ordinal))
        {
            UiActions.Run(() => _preferences.UpdateAsync(current => current with { Culture = culture }), "settings.language");
        }
    }

    private void Open(string path)
    {
        DiagnosticsStatus = OpenFolder(path) ? null : _localization["Settings.Diagnostics.OpenFailed"];
    }

    private void OnCultureChanged(object? sender, EventArgs e) => RefreshLabels();

    private void OnCapabilitiesChanged(object? sender, EventArgs e)
    {
        // The classroom client state can change on a background thread.
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => OnCapabilitiesChanged(sender, e));
            return;
        }

        MetadataProviders.Refresh();
        Shelf3D.Refresh();
        ClassroomHost.Refresh();
        OnPropertyChanged(nameof(CapabilityRows));
        OnPropertyChanged(nameof(PrivacyStatusText));
    }

    private void OnPreferencesChanged(object? sender, UserPreferences preferences)
    {
        foreach (string name in new[]
                 {
                     nameof(IsThemeLight), nameof(IsThemeDark), nameof(IsThemeSystem),
                     nameof(IsDensityComfortable), nameof(IsDensityCompact),
                     nameof(IsLanguageSystem), nameof(IsLanguageEnglish), nameof(IsLanguageFrench),
                 })
        {
            OnPropertyChanged(name);
        }

        OnCapabilitiesChanged(sender, EventArgs.Empty);
    }

    private void RefreshLabels()
    {
        foreach (SettingsSectionItem item in Sections)
        {
            item.Label = _localization[item.LabelKey];
        }

        MetadataProviders.Refresh();
        Shelf3D.Refresh();
        ClassroomHost.Refresh();
        if (_diagnosticsStatus is not null)
        {
            DiagnosticsStatus = null;
        }

        // Every label is a computed property: one reset notification re-reads them all.
        OnPropertyChanged(string.Empty);
    }

    private void RaiseSectionChanged()
    {
        OnPropertyChanged(nameof(SelectedSection));
        OnPropertyChanged(nameof(SectionTitle));
        OnPropertyChanged(nameof(IsLibrarySection));
        OnPropertyChanged(nameof(IsAppearanceSection));
        OnPropertyChanged(nameof(IsLanguageSection));
        OnPropertyChanged(nameof(IsOnlineSection));
        OnPropertyChanged(nameof(IsFeaturesSection));
        OnPropertyChanged(nameof(IsPrivacySection));
        OnPropertyChanged(nameof(IsTextRecognitionSection));
        OnPropertyChanged(nameof(IsDiagnosticsSection));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        UiThreadGuard.Verify(this, name, PropertyChanged);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
