using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Ocr;

namespace OgmaLibrary.App.ViewModels.Settings;

/// <summary>
/// Settings → Text recognition (Sept-23 Phases 08 and 17): automatic OCR for scanned books,
/// pausing on battery, and the recognition language chosen only from installed, verified packs.
/// Every change is saved immediately through the OCR policy store.
/// </summary>
public sealed class OcrSettingsViewModel : INotifyPropertyChanged
{
    private readonly IOcrPolicySettingsStore _store;
    private readonly ILocalizationService _localization;
    private readonly ILogger _logger;
    private OcrPolicySettings _settings = OcrPolicySettings.Default;
    private bool _isLoaded;

    /// <summary>Creates the view model; call <see cref="LoadAsync"/> before showing it.</summary>
    /// <param name="store">The OCR policy store.</param>
    /// <param name="languages">The installed, checksum-verified language catalog.</param>
    /// <param name="localization">Localized labels.</param>
    /// <param name="logger">Diagnostics logger.</param>
    public OcrSettingsViewModel(
        IOcrPolicySettingsStore store,
        IOcrLanguageCatalog languages,
        ILocalizationService localization,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(languages);
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Languages = new ReadOnlyCollection<string>(languages.GetInstalledLanguages().ToList());
        _localization.CultureChanged += (_, _) => RaiseLabels();
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Installed, verified recognition languages (for example <c>eng</c>).</summary>
    public IReadOnlyList<string> Languages { get; }

    /// <summary>Whether more than one language is installed, so a choice is meaningful.</summary>
    public bool HasLanguageChoice => Languages.Count > 1;

    /// <summary>Queue OCR automatically for scanned, image-only books.</summary>
    public bool AutoOcrScannedBooks
    {
        get => _settings.AutoOcrScannedBooks;
        set => Update(_settings with { AutoOcrScannedBooks = value });
    }

    /// <summary>Do not start automatic OCR while the device runs on battery.</summary>
    public bool PauseOnBattery
    {
        get => _settings.PauseOnBattery;
        set => Update(_settings with { PauseOnBattery = value });
    }

    /// <summary>The recognition language.</summary>
    public string Language
    {
        get => _settings.Language;
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && Languages.Contains(value, StringComparer.Ordinal))
            {
                Update(_settings with { Language = value });
            }
        }
    }

    /// <summary>Label for the automatic OCR switch.</summary>
    public string AutoOcrLabel => _localization["Settings.Ocr.Auto"];

    /// <summary>Label for the pause-on-battery switch.</summary>
    public string PauseOnBatteryLabel => _localization["Settings.Ocr.PauseOnBattery"];

    /// <summary>Label for the language picker.</summary>
    public string LanguageLabel => _localization["Settings.Ocr.Language"];

    /// <summary>Explains which languages are available and why.</summary>
    public string LanguagesHelp => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        _localization["Settings.Ocr.LanguagesHelpFormat"],
        string.Join(", ", Languages));

    /// <summary>Loads the saved policy.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when loaded.</returns>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _settings = await _store.GetAsync(cancellationToken).ConfigureAwait(true);
        _isLoaded = true;
        OnPropertyChanged(nameof(AutoOcrScannedBooks));
        OnPropertyChanged(nameof(PauseOnBattery));
        OnPropertyChanged(nameof(Language));
    }

    private void Update(OcrPolicySettings next)
    {
        if (next == _settings)
        {
            return;
        }

        _settings = next;
        OnPropertyChanged(nameof(AutoOcrScannedBooks));
        OnPropertyChanged(nameof(PauseOnBattery));
        OnPropertyChanged(nameof(Language));
        if (_isLoaded)
        {
            _ = SaveAsync(next);
        }
    }

    private async Task SaveAsync(OcrPolicySettings settings)
    {
        try
        {
            await _store.SaveAsync(settings).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppLog.ViewModelStepIgnored(_logger, exception, nameof(OcrSettingsViewModel), "settings.ocr.save");
        }
    }

    private void RaiseLabels()
    {
        OnPropertyChanged(nameof(AutoOcrLabel));
        OnPropertyChanged(nameof(PauseOnBatteryLabel));
        OnPropertyChanged(nameof(LanguageLabel));
        OnPropertyChanged(nameof(LanguagesHelp));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
