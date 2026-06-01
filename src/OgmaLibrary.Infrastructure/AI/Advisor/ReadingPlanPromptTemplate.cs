using System.Reflection;

namespace OgmaLibrary.Infrastructure.AI.Advisor;

/// <summary>Loads the embedded Phase 13 reading-plan prompt template.</summary>
public static class ReadingPlanPromptTemplate
{
    private const string ResourceName = "OgmaLibrary.Infrastructure.AI.Advisor.prompts.reading-plan.txt";

    /// <summary>Loads the structured reading-plan prompt.</summary>
    public static string Load()
    {
        Assembly assembly = typeof(ReadingPlanPromptTemplate).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            throw new InvalidOperationException("The embedded reading-plan prompt template was not found.");
        }

        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
