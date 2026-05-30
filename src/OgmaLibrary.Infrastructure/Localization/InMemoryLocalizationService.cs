using System.Globalization;
using OgmaLibrary.Application;

namespace OgmaLibrary.Infrastructure.Localization;

/// <summary>
/// A simple in-memory <see cref="ILocalizationService"/> carrying the MVP English and
/// French resource sets. It establishes the localization pattern for Phase 02; the
/// production resource format (satellite <c>.resx</c> vs structured JSON) is selected
/// by a dedicated ADR and migrated in Phase 03 without changing this contract.
/// </summary>
public sealed class InMemoryLocalizationService : ILocalizationService
{
    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["MainWindow.Title"] = "Ogma Library",
        ["MainWindow.Tagline"] = "Your personal PDF library — beautifully managed, intelligently advised.",
        ["MainWindow.EmptyState.Heading"] = "Your library will appear here",
        ["MainWindow.EmptyState.Body"] = "Choose a folder of PDFs to begin. Ogma will scan it, recognise your books, and let you browse them as covers and spines.",
        ["MainWindow.Action.ChooseFolder"] = "Choose library folder",
        ["MainWindow.Status.Skeleton"] = "Skeleton build — Phase 02",
    };

    private static readonly IReadOnlyDictionary<string, string> French = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["MainWindow.Title"] = "Bibliothèque Ogma",
        ["MainWindow.Tagline"] = "Votre bibliothèque PDF personnelle — élégamment gérée, intelligemment conseillée.",
        ["MainWindow.EmptyState.Heading"] = "Votre bibliothèque apparaîtra ici",
        ["MainWindow.EmptyState.Body"] = "Choisissez un dossier de PDF pour commencer. Ogma l'analysera, reconnaîtra vos livres et vous laissera les parcourir sous forme de couvertures et de tranches.",
        ["MainWindow.Action.ChooseFolder"] = "Choisir le dossier de la bibliothèque",
        ["MainWindow.Status.Skeleton"] = "Version squelette — Phase 02",
    };

    private IReadOnlyDictionary<string, string> _active = English;

    /// <inheritdoc />
    public CultureInfo CurrentCulture { get; private set; } = CultureInfo.GetCultureInfo("en");

    /// <inheritdoc />
    public event EventHandler? CultureChanged;

    /// <inheritdoc />
    public string this[string key]
    {
        get
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return _active.TryGetValue(key, out string? value) ? value : $"⟦{key}⟧";
        }
    }

    /// <inheritdoc />
    public void SetCulture(string cultureName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);
        string twoLetter = CultureInfo.GetCultureInfo(cultureName).TwoLetterISOLanguageName;
        _active = twoLetter == "fr" ? French : English;
        CurrentCulture = CultureInfo.GetCultureInfo(twoLetter);
        CultureChanged?.Invoke(this, EventArgs.Empty);
    }
}
