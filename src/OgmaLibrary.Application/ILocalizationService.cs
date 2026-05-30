using System.Globalization;

namespace OgmaLibrary.Application;

/// <summary>
/// Resolves localized user-facing strings by key so that no UI string is hard-coded
/// (I18N strategy; the OGMA0001 analyzer enforces this). The active culture can be
/// switched at runtime. MVP ships full English and French; the final product adds
/// Spanish, Italian, and German.
/// </summary>
public interface ILocalizationService
{
    /// <summary>The culture currently in effect for string resolution.</summary>
    CultureInfo CurrentCulture { get; }

    /// <summary>Raised after <see cref="SetCulture"/> changes the active culture.</summary>
    event EventHandler? CultureChanged;

    /// <summary>Resolves a localized string for the given resource key.</summary>
    /// <param name="key">The stable resource key (for example <c>MainWindow.Title</c>).</param>
    /// <returns>The localized string, or a visibly-flagged fallback if the key is missing.</returns>
    string this[string key] { get; }

    /// <summary>Switches the active culture (for example <c>"en"</c> or <c>"fr"</c>).</summary>
    /// <param name="cultureName">The culture name to activate.</param>
    void SetCulture(string cultureName);
}
