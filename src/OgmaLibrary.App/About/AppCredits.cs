namespace OgmaLibrary.App.About;

/// <summary>
/// The single source of the About dialog's credit data: names, contacts and web addresses.
/// These are data, not interface text, so they are never translated. Labels such as
/// "Lead Developer" are localised separately.
/// </summary>
public static class AppCredits
{
    /// <summary>The product name.</summary>
    public const string ProductName = "Ogma Library";

    /// <summary>The copyright holder.</summary>
    public const string CopyrightHolder = "Chwezi Core Systems";

    /// <summary>The lead developer's name.</summary>
    public const string LeadDeveloperName = "Peter Bamuhigire";

    /// <summary>The lead developer's website, as displayed.</summary>
    public const string WebsiteDisplay = "www.techguypeter.com";

    /// <summary>The lead developer's website address.</summary>
    public const string WebsiteUrl = "https://www.techguypeter.com";

    /// <summary>The lead developer's email address.</summary>
    public const string Email = "peter@techguypeter.com";

    /// <summary>The mailto link for <see cref="Email"/>.</summary>
    public const string EmailUrl = "mailto:peter@techguypeter.com";

    /// <summary>The lead developer's phone number, as displayed.</summary>
    public const string PhoneDisplay = "+256 784 464178";

    /// <summary>The tel link for <see cref="PhoneDisplay"/>.</summary>
    public const string PhoneUrl = "tel:+256784464178";

    /// <summary>The Ogma product website, as displayed.</summary>
    public const string OgmaSiteDisplay = "ogma.techguypeter.com";

    /// <summary>The Ogma product website address.</summary>
    public const string OgmaSiteUrl = "https://ogma.techguypeter.com/";

    /// <summary>
    /// The bundled typefaces, used under the SIL Open Font License 1.1. Each licence text ships
    /// beside its font as <c>Assets/Fonts/&lt;family&gt;/OFL.txt</c>; the application code is
    /// MIT-licensed (<c>LICENSE</c>).
    /// </summary>
    public const string BundledTypefaces = "Spectral, Public Sans, JetBrains Mono";

    /// <summary>The only addresses the About dialog may open.</summary>
    public static IReadOnlyList<Uri> KnownLinks { get; } =
    [
        new Uri(WebsiteUrl, UriKind.Absolute),
        new Uri(EmailUrl, UriKind.Absolute),
        new Uri(PhoneUrl, UriKind.Absolute),
        new Uri(OgmaSiteUrl, UriKind.Absolute),
    ];
}
