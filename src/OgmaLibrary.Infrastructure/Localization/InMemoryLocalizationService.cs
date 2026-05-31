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

        // Phase 03 icon accessible labels (en)
        ["Icon.ic_app_logo.Label"] = "Ogma Library",
        ["Icon.ic_settings.Label"] = "Settings",
        ["Icon.ic_keyboard_shortcut.Label"] = "Keyboard shortcut",
        ["Icon.ic_close.Label"] = "Close",
        ["Icon.ic_search.Label"] = "Search",
        ["Icon.ic_lib_scan.Label"] = "Scan library",
        ["Icon.ic_lib_folder_open.Label"] = "Open library folder",
        ["Icon.ic_lib_health.Label"] = "Library health",
        ["Icon.ic_lib_preferences.Label"] = "Library preferences",
        ["Icon.ic_cat_view_grid.Label"] = "Grid view",
        ["Icon.ic_cat_view_list.Label"] = "List view",
        ["Icon.ic_cat_view_shelf3d.Label"] = "3D shelf view",
        ["Icon.ic_cat_view_directory.Label"] = "Directory view",
        ["Icon.ic_cat_shelf.Label"] = "Shelf",
        ["Icon.ic_cat_shelf_new.Label"] = "New shelf",
        ["Icon.ic_cat_filter.Label"] = "Filter",
        ["Icon.ic_cat_sort.Label"] = "Sort",
        ["Icon.ic_read_open.Label"] = "Open book",
        ["Icon.ic_read_bookmark.Label"] = "Bookmark",
        ["Icon.ic_read_zoom_in.Label"] = "Zoom in",
        ["Icon.ic_read_zoom_out.Label"] = "Zoom out",
        ["Icon.ic_read_fullscreen.Label"] = "Full screen",
        ["Icon.ic_search_metadata.Label"] = "Search books",
        ["Icon.ic_search_fulltext.Label"] = "Full-text search",
        ["Icon.ic_ai_advisor.Label"] = "AI advisor",
        ["Icon.ic_ai_privacy.Label"] = "AI privacy settings",
        ["Icon.ic_ai_disable.Label"] = "Disable AI",
        ["Icon.ic_set_appearance.Label"] = "Appearance",
        ["Icon.ic_set_language.Label"] = "Language",
        ["Icon.ic_set_updates.Label"] = "Updates",
        ["Icon.ic_set_about.Label"] = "About",
        ["Icon.ic_status_available.Label"] = "Available",
        ["Icon.ic_status_unavailable.Label"] = "Unavailable",
        ["Icon.ic_status_loading.Label"] = "Loading",

        // Phase 05 — Scan progress and status strings (en)
        ["Scan.Phase.Idle"] = "Ready",
        ["Scan.Phase.Discovering"] = "Discovering files…",
        ["Scan.Phase.Processing"] = "Processing…",
        ["Scan.Phase.GeneratingAssets"] = "Generating thumbnails…",
        ["Scan.Phase.Complete"] = "Scan complete",
        ["Scan.Phase.PartialFailure"] = "Scan complete — some files failed",
        ["Scan.Phase.Cancelled"] = "Scan cancelled",
        ["Scan.Status.Scanned"] = "Scanned {0} books",
        ["Scan.Button.Cancel"] = "Cancel",
        ["Scan.Progress.Files"] = "{0} / {1} files",
        ["Scan.Progress.Failed"] = "{0} failed",
        ["MainWindow.Status.Ready"] = "Ready — choose a library folder to begin",
    };

    private static readonly IReadOnlyDictionary<string, string> French = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["MainWindow.Title"] = "Bibliothèque Ogma",
        ["MainWindow.Tagline"] = "Votre bibliothèque PDF personnelle — élégamment gérée, intelligemment conseillée.",
        ["MainWindow.EmptyState.Heading"] = "Votre bibliothèque apparaîtra ici",
        ["MainWindow.EmptyState.Body"] = "Choisissez un dossier de PDF pour commencer. Ogma l'analysera, reconnaîtra vos livres et vous laissera les parcourir sous forme de couvertures et de tranches.",
        ["MainWindow.Action.ChooseFolder"] = "Choisir le dossier de la bibliothèque",
        ["MainWindow.Status.Skeleton"] = "Version squelette — Phase 02",

        // Phase 03 icon accessible labels (fr)
        ["Icon.ic_app_logo.Label"] = "Bibliothèque Ogma",
        ["Icon.ic_settings.Label"] = "Paramètres",
        ["Icon.ic_keyboard_shortcut.Label"] = "Raccourci clavier",
        ["Icon.ic_close.Label"] = "Fermer",
        ["Icon.ic_search.Label"] = "Rechercher",
        ["Icon.ic_lib_scan.Label"] = "Scanner la bibliothèque",
        ["Icon.ic_lib_folder_open.Label"] = "Ouvrir le dossier",
        ["Icon.ic_lib_health.Label"] = "Santé de la bibliothèque",
        ["Icon.ic_lib_preferences.Label"] = "Préférences de bibliothèque",
        ["Icon.ic_cat_view_grid.Label"] = "Vue grille",
        ["Icon.ic_cat_view_list.Label"] = "Vue liste",
        ["Icon.ic_cat_view_shelf3d.Label"] = "Vue étagère 3D",
        ["Icon.ic_cat_view_directory.Label"] = "Vue répertoire",
        ["Icon.ic_cat_shelf.Label"] = "Étagère",
        ["Icon.ic_cat_shelf_new.Label"] = "Nouvelle étagère",
        ["Icon.ic_cat_filter.Label"] = "Filtrer",
        ["Icon.ic_cat_sort.Label"] = "Trier",
        ["Icon.ic_read_open.Label"] = "Ouvrir le livre",
        ["Icon.ic_read_bookmark.Label"] = "Signet",
        ["Icon.ic_read_zoom_in.Label"] = "Zoom avant",
        ["Icon.ic_read_zoom_out.Label"] = "Zoom arrière",
        ["Icon.ic_read_fullscreen.Label"] = "Plein écran",
        ["Icon.ic_search_metadata.Label"] = "Rechercher des livres",
        ["Icon.ic_search_fulltext.Label"] = "Recherche plein texte",
        ["Icon.ic_ai_advisor.Label"] = "Conseiller IA",
        ["Icon.ic_ai_privacy.Label"] = "Confidentialité IA",
        ["Icon.ic_ai_disable.Label"] = "Désactiver l'IA",
        ["Icon.ic_set_appearance.Label"] = "Apparence",
        ["Icon.ic_set_language.Label"] = "Langue",
        ["Icon.ic_set_updates.Label"] = "Mises à jour",
        ["Icon.ic_set_about.Label"] = "À propos",
        ["Icon.ic_status_available.Label"] = "Disponible",
        ["Icon.ic_status_unavailable.Label"] = "Indisponible",
        ["Icon.ic_status_loading.Label"] = "Chargement",

        // Phase 05 — Scan progress and status strings (fr)
        ["Scan.Phase.Idle"] = "Prêt",
        ["Scan.Phase.Discovering"] = "Découverte des fichiers…",
        ["Scan.Phase.Processing"] = "Traitement…",
        ["Scan.Phase.GeneratingAssets"] = "Génération des miniatures…",
        ["Scan.Phase.Complete"] = "Analyse terminée",
        ["Scan.Phase.PartialFailure"] = "Analyse terminée — certains fichiers ont échoué",
        ["Scan.Phase.Cancelled"] = "Analyse annulée",
        ["Scan.Status.Scanned"] = "{0} livres analysés",
        ["Scan.Button.Cancel"] = "Annuler",
        ["Scan.Progress.Files"] = "{0} / {1} fichiers",
        ["Scan.Progress.Failed"] = "{0} échoués",
        ["MainWindow.Status.Ready"] = "Prêt — choisissez un dossier de bibliothèque pour commencer",
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
