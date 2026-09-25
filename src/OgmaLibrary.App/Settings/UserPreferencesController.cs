using System.Globalization;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Navigation;

namespace OgmaLibrary.App.Settings;

/// <summary>
/// The single source of truth for the user's preferences in the running app (Sept-23 Phase 08,
/// task 8.7). Settings, the command palette and start-up all go through it, so they can never
/// disagree. Every change is applied live: capabilities are re-resolved, the interface language
/// switches, and <see cref="Changed"/> lets the application re-apply theme and density. The
/// value is updated before it is persisted, so quick successive changes never lose one another.
/// </summary>
public sealed class UserPreferencesController : IDisposable
{
    /// <summary>The interface languages Ogma ships (two-letter names).</summary>
    public static readonly IReadOnlyList<string> SupportedCultures = ["en", "fr"];

    private readonly IUserPreferencesService? _store;
    private readonly ICapabilitySettings? _capabilities;
    private readonly ILocalizationService? _localization;
    private readonly Func<CultureInfo> _systemCulture;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    /// <summary>Initializes a new instance of the <see cref="UserPreferencesController"/> class.</summary>
    /// <param name="store">The preference store, or null to keep preferences in memory only.</param>
    /// <param name="capabilities">The capability resolver that receives the user switches.</param>
    /// <param name="localization">The localization service whose culture follows the preference.</param>
    /// <param name="systemCulture">Reads the operating-system UI culture (tests substitute it).</param>
    public UserPreferencesController(
        IUserPreferencesService? store,
        ICapabilitySettings? capabilities = null,
        ILocalizationService? localization = null,
        Func<CultureInfo>? systemCulture = null)
    {
        _store = store;
        _capabilities = capabilities;
        _localization = localization;
        _systemCulture = systemCulture ?? (() => CultureInfo.CurrentUICulture);
    }

    /// <summary>Raised on the caller's thread after the preferences change.</summary>
    public event EventHandler<UserPreferences>? Changed;

    /// <summary>The current preferences.</summary>
    public UserPreferences Current { get; private set; } = new();

    /// <summary>True when the last load found a damaged file and fell back to defaults.</summary>
    public bool LastLoadWasRecovered => _store?.LastLoadWasRecovered ?? false;

    /// <summary>The supported language that the operating system asks for, or English.</summary>
    public string SystemLanguage
    {
        get
        {
            string language = _systemCulture().TwoLetterISOLanguageName;
            return SupportedCultures.Contains(language, StringComparer.OrdinalIgnoreCase)
                ? language.ToLowerInvariant()
                : "en";
        }
    }

    /// <summary>The language in effect: the user's choice, otherwise the OS language when supported.</summary>
    public string EffectiveLanguage => Current.Culture ?? SystemLanguage;

    /// <summary>Loads the persisted preferences and applies them.</summary>
    /// <param name="cancellationToken">A token to cancel the load.</param>
    /// <returns>A task that completes when the preferences are applied.</returns>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        UserPreferences loaded = _store is null
            ? new UserPreferences()
            : await _store.GetAsync(cancellationToken).ConfigureAwait(true);
        Apply(loaded);
    }

    /// <summary>Changes the preferences, applies them live and persists them atomically.</summary>
    /// <param name="change">Produces the new preferences from the current ones.</param>
    /// <param name="cancellationToken">A token to cancel the save.</param>
    /// <returns>A task that completes when the preferences are saved.</returns>
    public async Task UpdateAsync(
        Func<UserPreferences, UserPreferences> change,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);
        UserPreferences next = change(Current);
        if (next == Current)
        {
            return;
        }

        Apply(next);
        if (_store is null)
        {
            return;
        }

        // Saves are serialised and always write the latest value, so quick successive changes
        // can never leave an older snapshot on disk.
        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(true);
        try
        {
            await _store.SaveAsync(Current, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            _saveGate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _saveGate.Dispose();

    private void Apply(UserPreferences preferences)
    {
        Current = preferences;
        _capabilities?.ApplyPreferences(preferences);
        if (_localization is not null)
        {
            string language = EffectiveLanguage;
            if (!string.Equals(_localization.CurrentCulture.TwoLetterISOLanguageName, language, StringComparison.OrdinalIgnoreCase))
            {
                _localization.SetCulture(language);
            }
        }

        Changed?.Invoke(this, preferences);
    }
}
