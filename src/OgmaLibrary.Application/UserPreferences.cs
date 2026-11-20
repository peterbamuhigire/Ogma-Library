using OgmaLibrary.Application.Search;

namespace OgmaLibrary.Application;

/// <summary>Supported application theme choices.</summary>
public enum UserTheme
{
    /// <summary>Use the light appearance.</summary>
    Light,
    /// <summary>Use the dark appearance.</summary>
    Dark,
    /// <summary>Follow the operating system appearance.</summary>
    System,
}

/// <summary>Supported application density choices.</summary>
public enum UserDensity
{
    /// <summary>Use the standard comfortable spacing and type scale.</summary>
    Comfortable,
    /// <summary>Use a reduced spacing and type scale.</summary>
    Compact,
}

/// <summary>
/// Persisted, non-sensitive per-user preferences (Sept-23 Phase 08, K16). Stored in
/// <c>user-preferences.json</c> in the Ogma data folder. Environment variables remain
/// administrator and test overrides for the capability switches and win over these values.
/// </summary>
/// <param name="Theme">The appearance theme.</param>
/// <param name="Density">The spacing density.</param>
/// <param name="Culture">
/// The chosen user-interface language (<c>"en"</c> or <c>"fr"</c>), or null to follow the
/// operating-system language when it is supported.
/// </param>
/// <param name="EnableMetadataProviders">Whether the user allows online book-detail lookups.</param>
/// <param name="EnableThreeDimensionalShelf">Whether the user turned on the 3D bookshelf preview.</param>
/// <param name="EnableClassroomHost">Whether the user allows this device to act as a classroom Host.</param>
/// <param name="SchemaVersion">The file schema version; older files are migrated when read.</param>
/// <param name="InspectorWidth">
/// The book-detail inspector width in device-independent pixels (Sept-23 Phase 11, 11.9),
/// clamped to <see cref="MinInspectorWidth"/>–<see cref="MaxInspectorWidth"/>.
/// </param>
/// <param name="SemanticProvider">
/// The semantic search provider (Sept-23 Phase 14, D-103). Files written before schema 4 have
/// no value and read as <see cref="SemanticProviderChoice.BuiltIn"/>.
/// </param>
public sealed record UserPreferences(
    UserTheme Theme = UserTheme.Light,
    UserDensity Density = UserDensity.Comfortable,
    string? Culture = null,
    bool EnableMetadataProviders = false,
    bool EnableThreeDimensionalShelf = false,
    bool EnableClassroomHost = false,
    int SchemaVersion = UserPreferences.CurrentSchemaVersion,
    double InspectorWidth = UserPreferences.DefaultInspectorWidth,
    SemanticProviderChoice SemanticProvider = SemanticProviderChoice.BuiltIn)
{
    /// <summary>
    /// The schema version written by this build (1 = theme and density only; 2 adds language and
    /// capability switches; 3 adds the inspector width; 4 adds the semantic provider).
    /// </summary>
    public const int CurrentSchemaVersion = 4;

    /// <summary>The inspector width used when none is stored.</summary>
    public const double DefaultInspectorWidth = 360;

    /// <summary>The narrowest inspector width the user can choose.</summary>
    public const double MinInspectorWidth = 320;

    /// <summary>The widest inspector width the user can choose.</summary>
    public const double MaxInspectorWidth = 560;

    /// <summary>Clamps a width to the allowed range; a non-finite value becomes the default.</summary>
    /// <param name="width">The requested width.</param>
    /// <returns>A width between <see cref="MinInspectorWidth"/> and <see cref="MaxInspectorWidth"/>.</returns>
    public static double ClampInspectorWidth(double width) =>
        double.IsFinite(width)
            ? Math.Clamp(width, MinInspectorWidth, MaxInspectorWidth)
            : DefaultInspectorWidth;
}

/// <summary>Loads and saves non-sensitive desktop preferences.</summary>
public interface IUserPreferencesService
{
    /// <summary>
    /// True when the last <see cref="GetAsync"/> found an unreadable preference file and fell
    /// back to defaults (the damaged file is kept beside it for support).
    /// </summary>
    bool LastLoadWasRecovered => false;

    /// <summary>Returns validated preferences, falling back to safe defaults.</summary>
    Task<UserPreferences> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Atomically persists validated preferences.</summary>
    Task SaveAsync(UserPreferences preferences, CancellationToken cancellationToken = default);
}
