using System.Reflection;

namespace OgmaLibrary.Infrastructure.AI.Advisor;

/// <summary>Loads the embedded Phase 13 recommendation prompt template.</summary>
public static class RecommendationPromptTemplate
{
    private const string ResourceName = "OgmaLibrary.Infrastructure.AI.Advisor.prompts.recommendation.txt";

    /// <summary>Loads the metadata-only recommendation prompt.</summary>
    public static string Load()
    {
        Assembly assembly = typeof(RecommendationPromptTemplate).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            throw new InvalidOperationException("The embedded recommendation prompt template was not found.");
        }

        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
