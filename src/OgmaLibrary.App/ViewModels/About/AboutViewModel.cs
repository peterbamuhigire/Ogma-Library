using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.App.About;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Diagnostics;

namespace OgmaLibrary.App.ViewModels.About;

/// <summary>One contact or web link shown in the About dialog.</summary>
public sealed class AboutLink
{
    private readonly ILocalizationService _localization;
    private readonly string _labelKey;

    /// <summary>Initializes a new instance of the <see cref="AboutLink"/> class.</summary>
    /// <param name="id">The stable link identifier (used in logs and the automation id).</param>
    /// <param name="labelKey">The localisation key of the label.</param>
    /// <param name="display">The address as displayed (data, not translated).</param>
    /// <param name="address">The address opened on activation.</param>
    /// <param name="localization">The localisation service.</param>
    public AboutLink(string id, string labelKey, string display, string address, ILocalizationService localization)
    {
        Id = id;
        _labelKey = labelKey;
        Display = display;
        Address = new Uri(address, UriKind.Absolute);
        _localization = localization;
    }

    /// <summary>The stable link identifier, for example <c>Website</c>.</summary>
    public string Id { get; }

    /// <summary>The UI Automation id, for example <c>About.Link.Website</c>.</summary>
    public string AutomationId => "About.Link." + Id;

    /// <summary>The localised label, for example "Website".</summary>
    public string Label => _localization[_labelKey];

    /// <summary>The address as displayed.</summary>
    public string Display { get; }

    /// <summary>The address opened on activation.</summary>
    public Uri Address { get; }

    /// <summary>The accessible name: label and address.</summary>
    public string AccessibleName =>
        string.Format(CultureInfo.CurrentCulture, _localization["About.Link.NameFormat"], Label, Display);

    /// <summary>The accessible help text: what activating the link does.</summary>
    public string HelpText => _localization["About.Link.Help"];
}

/// <summary>
/// The About Ogma Library dialog: product identity, version, the lead developer's credit and
/// contacts, copyright and open-source licences. Credit data comes from <see cref="AppCredits"/>.
/// </summary>
public sealed class AboutViewModel : INotifyPropertyChanged
{
    private readonly ILocalizationService _localization;
    private readonly IExternalLinkLauncher _launcher;
    private readonly ILogger _logger;
    private readonly string _version;
    private readonly int _year;
    private string? _statusMessage;

    /// <summary>Initializes a new instance of the <see cref="AboutViewModel"/> class.</summary>
    /// <param name="localization">The localisation service.</param>
    /// <param name="launcher">Opens allowed external links.</param>
    /// <param name="logger">The logger; a null logger when omitted.</param>
    /// <param name="version">The version to show; the assembly informational version when omitted.</param>
    /// <param name="year">The copyright year; the current year when omitted.</param>
    public AboutViewModel(
        ILocalizationService localization,
        IExternalLinkLauncher launcher,
        ILogger? logger = null,
        string? version = null,
        int? year = null)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(launcher);
        _localization = localization;
        _launcher = launcher;
        _logger = logger ?? NullLogger.Instance;
        _version = DisplayVersion(version ?? AppDiagnostics.AppVersion);
        _year = year ?? DateTime.Now.Year;

        Website = new AboutLink("Website", "About.Website", AppCredits.WebsiteDisplay, AppCredits.WebsiteUrl, localization);
        Email = new AboutLink("Email", "About.Email", AppCredits.Email, AppCredits.EmailUrl, localization);
        Phone = new AboutLink("Phone", "About.Phone", AppCredits.PhoneDisplay, AppCredits.PhoneUrl, localization);
        OgmaSite = new AboutLink("OgmaSite", "About.OgmaSite", AppCredits.OgmaSiteDisplay, AppCredits.OgmaSiteUrl, localization);
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The dialog title, also its accessible name.</summary>
    public string Title => _localization["About.Title"];

    /// <summary>The one-line tagline.</summary>
    public string Tagline => _localization["About.Tagline"];

    /// <summary>The bare version, for example <c>1.0.0</c>.</summary>
    public string Version => _version;

    /// <summary>The version line, for example "Version 1.0.0".</summary>
    public string VersionText =>
        string.Format(CultureInfo.CurrentCulture, _localization["About.Version.Format"], _version);

    /// <summary>The "Lead Developer" eyebrow.</summary>
    public string LeadDeveloperLabel => _localization["About.LeadDeveloper"];

    /// <summary>The accessible name of the credit block, for example "Lead Developer: Peter Bamuhigire".</summary>
    public string LeadDeveloperAccessibleName =>
        string.Format(CultureInfo.CurrentCulture, _localization["About.Link.NameFormat"], LeadDeveloperLabel, AppCredits.LeadDeveloperName);

    /// <summary>The small-capitals eyebrow above the credit, for example "LEAD DEVELOPER".</summary>
    public string LeadDeveloperEyebrow => LeadDeveloperLabel.ToUpper(_localization.CurrentCulture);

    /// <summary>The small-capitals eyebrow above the product website, for example "OGMA ONLINE".</summary>
    public string OnlineEyebrow => _localization["About.Online"].ToUpper(_localization.CurrentCulture);

    /// <summary>The lead developer's website.</summary>
    public AboutLink Website { get; }

    /// <summary>The lead developer's email.</summary>
    public AboutLink Email { get; }

    /// <summary>The lead developer's phone.</summary>
    public AboutLink Phone { get; }

    /// <summary>The Ogma product website.</summary>
    public AboutLink OgmaSite { get; }

    /// <summary>The links in display order.</summary>
    public IReadOnlyList<AboutLink> Links => [Website, Email, Phone, OgmaSite];

    /// <summary>The copyright line, for example "© 2026 Chwezi Core Systems".</summary>
    public string CopyrightText =>
        string.Format(CultureInfo.CurrentCulture, _localization["About.Copyright.Format"], _year, AppCredits.CopyrightHolder);

    /// <summary>The open-source licence line.</summary>
    public string LicencesText =>
        string.Format(CultureInfo.CurrentCulture, _localization["About.Licences.Format"], AppCredits.BundledTypefaces);

    /// <summary>The Close button label.</summary>
    public string CloseText => _localization["About.Close"];

    /// <summary>A localised message after a link could not be opened; otherwise <see langword="null"/>.</summary>
    public string? StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (!string.Equals(_statusMessage, value, StringComparison.Ordinal))
            {
                _statusMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    /// <summary>Whether <see cref="StatusMessage"/> is shown.</summary>
    public bool HasStatusMessage => _statusMessage is not null;

    /// <summary>
    /// Opens a link. A refusal or failure is logged as a warning and shown as a localised
    /// message; it never throws for an ordinary failure.
    /// </summary>
    /// <param name="link">The link.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns><see langword="true"/> when the platform opened the link.</returns>
    public async Task<bool> OpenAsync(AboutLink link, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(link);
        StatusMessage = null;
        bool opened;
        Exception? failure = null;
        try
        {
            opened = await _launcher.TryOpenAsync(link.Address, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (!ExceptionClassification.IsFatal(exception))
        {
            opened = false;
            failure = exception;
        }

        if (!opened)
        {
            AboutLog.LinkFailed(_logger, link.Id, failure);
            StatusMessage = _localization["About.LinkFailed"];
        }

        return opened;
    }

    // Build metadata after '+' (for example a commit hash) is noise in the dialog.
    private static string DisplayVersion(string version)
    {
        int plus = version.IndexOf('+', StringComparison.Ordinal);
        string trimmed = (plus > 0 ? version[..plus] : version).Trim();
        return trimmed.Length == 0 ? "0.0.0" : trimmed;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
