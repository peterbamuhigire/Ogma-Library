using OgmaLibrary.App.ViewModels.Ai;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.Localization;

namespace OgmaLibrary.Tests.Ai;

/// <summary>Phase 12 payload-preview UI model tests.</summary>
public sealed class PayloadPreviewViewModelTests
{
    [Fact]
    public void PayloadPreviewViewModel_FlattensExactPayloadFields()
    {
        var localization = new InMemoryLocalizationService();
        var preview = new AiPayloadPreview(
            AiPrivacyTier.ContentAware,
            "anthropic",
            "claude-test",
            "answer",
            "What does this page say?",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = "Ogma",
                ["author"] = "Chwezi Core Systems",
            },
            [new AiContentChunk("book-1", "page:2", "chunk text")]);

        using var viewModel = new PayloadPreviewViewModel(preview, localization);

        Assert.Equal(5, viewModel.Items.Count);
        Assert.Contains(viewModel.Items, item => item.Kind == "query" && item.Value == "What does this page say?");
        Assert.Contains(viewModel.Items, item => item.Kind == "metadata" && item.Label == "author");
        Assert.Contains(viewModel.Items, item => item.Kind == "content" && item.Value == "chunk text");
        Assert.Contains("anthropic", viewModel.ProviderSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void PayloadPreviewViewModel_RecordsDecision()
    {
        var localization = new InMemoryLocalizationService();
        using var viewModel = new PayloadPreviewViewModel(CreatePreview(), localization);

        viewModel.RememberForSession();

        Assert.True(viewModel.HasDecision);
        Assert.Equal(AiPreviewDecision.RememberForSession, viewModel.Decision);
    }

    [Fact]
    public void PayloadPreviewViewModel_UpdatesLocalizedLabels()
    {
        var localization = new InMemoryLocalizationService();
        using var viewModel = new PayloadPreviewViewModel(CreatePreview(), localization);

        localization.SetCulture("fr");

        Assert.Equal("Envoyer", viewModel.SendLabel);
        Assert.Contains("Fournisseur", viewModel.ProviderSummary, StringComparison.Ordinal);
    }

    private static AiPayloadPreview CreatePreview() =>
        new(
            AiPrivacyTier.MetadataOnly,
            "openai",
            "gpt-test",
            "recommendation",
            "Recommend",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = "Ogma",
            },
            []);
}
