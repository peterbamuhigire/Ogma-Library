namespace OgmaLibrary.Tests;

/// <summary>Resolves source-controlled test fixtures from any build output layout.</summary>
public static class RepositoryTestPaths
{
    public static string Root
    {
        get
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "OgmaLibrary.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                $"Could not locate the Ogma Library repository from '{AppContext.BaseDirectory}'.");
        }
    }
}
