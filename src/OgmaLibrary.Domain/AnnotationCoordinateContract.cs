namespace OgmaLibrary.Domain;

/// <summary>
/// Version policy for persisted annotation coordinates. The first durable format
/// stores regions normalized to the un-rotated page dimensions.
/// </summary>
public static class AnnotationCoordinateContract
{
    /// <summary>The current normalized coordinate representation.</summary>
    public const string CurrentVersion = "normalized-v1";

    /// <summary>
    /// Converts an omitted legacy version to the current representation while
    /// retaining unknown versions for fail-closed handling.
    /// </summary>
    public static string NormalizeVersion(string? version) =>
        string.IsNullOrWhiteSpace(version) ? CurrentVersion : version.Trim();

    /// <summary>Returns whether persisted coordinates can be rendered safely.</summary>
    public static bool IsSupported(string? version) =>
        string.Equals(NormalizeVersion(version), CurrentVersion, StringComparison.Ordinal);
}
