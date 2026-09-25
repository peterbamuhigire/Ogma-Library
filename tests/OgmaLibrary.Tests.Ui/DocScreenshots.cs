namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Publishes render-test screenshots into <c>docs/developer-guide/images</c> only on request.
/// Ordinary test runs must not modify tracked files (Sept-23 Kaizen K04); set
/// <c>OGMA_UPDATE_DOC_SCREENSHOTS=1</c> to refresh the developer-guide images deliberately.
/// </summary>
internal static class DocScreenshots
{
    /// <summary>Environment variable that enables writing into the docs tree.</summary>
    public const string UpdateVariable = "OGMA_UPDATE_DOC_SCREENSHOTS";

    /// <summary>Whether this run should refresh the developer-guide images.</summary>
    public static bool IsUpdateRequested =>
        string.Equals(Environment.GetEnvironmentVariable(UpdateVariable), "1", StringComparison.Ordinal);

    /// <summary>Copies <paramref name="sourcePath"/> into the developer-guide images folder when requested.</summary>
    public static void Publish(string sourcePath, string fileName)
    {
        if (!IsUpdateRequested)
        {
            return;
        }

        string imagesDirectory = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "developer-guide", "images"));
        Directory.CreateDirectory(imagesDirectory);
        File.Copy(sourcePath, Path.Combine(imagesDirectory, fileName), overwrite: true);
    }
}
