using OgmaLibrary.Infrastructure.AI.Onnx;

namespace OgmaLibrary.Tests.Ai.Onnx;

/// <summary>
/// Locates the verified embedding model for model-gated tests. Model files
/// never enter git; a developer or CI job points
/// <c>OGMA_TEST_EMBEDDING_MODEL_DIR</c> at a folder holding the manifest's
/// files. Without it the tests skip visibly (NOT_ASSESSED, never a pass).
/// </summary>
internal static class EmbeddingModelTestFolder
{
    /// <summary>Environment variable naming the verified model folder.</summary>
    internal const string VariableName = "OGMA_TEST_EMBEDDING_MODEL_DIR";

    /// <summary>Skips the calling test when no model folder is configured.</summary>
    /// <returns>The configured model folder.</returns>
    internal static string RequireOrSkip()
    {
        string? directory = Environment.GetEnvironmentVariable(VariableName);
        Skip.If(
            string.IsNullOrWhiteSpace(directory),
            $"NOT_ASSESSED: {VariableName} is not set, so the built-in embedding model is unavailable.");
        return directory!;
    }

    /// <summary>Creates a provider over the configured, verified model.</summary>
    internal static OnnxEmbeddingProvider CreateProviderOrSkip()
    {
        string directory = RequireOrSkip();
        var store = EmbeddingModelStore.ForModelDirectory(directory, EmbeddingModelManifest.LoadDefault());
        return new OnnxEmbeddingProvider(store);
    }
}
