using OgmaLibrary.Application.Ai;
using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.Tests.Ai;

/// <summary>Phase 12 AI gateway contract tests.</summary>
public sealed class AiContractTests
{
    [Fact]
    public void AiPrivacyTier_DefaultIsOffline()
    {
        var tier = default(AiPrivacyTier);

        Assert.Equal(AiPrivacyTier.Offline, tier);
        Assert.Equal(0, (int)tier);
    }

    [Fact]
    public void AiConsentRecord_RevokedAt_MakesConsentInvalid()
    {
        var active = new AiConsentRecord(
            "consent-active",
            AiPrivacyTier.MetadataOnly,
            "anthropic",
            "library:default",
            DateTimeOffset.UtcNow);
        var revoked = new AiConsentRecord(
            "consent-revoked",
            AiPrivacyTier.MetadataOnly,
            "anthropic",
            "library:default",
            active.GrantedAt,
            DateTimeOffset.UtcNow);

        Assert.True(active.IsActive);
        Assert.False(revoked.IsActive);
    }

    [Fact]
    public void AiRequest_ContentChunks_ForbiddenForTier1()
    {
        var chunk = new AiContentChunk("BOOKAI000000000000001", "page:1", "private page text");

        Assert.Throws<ArgumentException>(() =>
            new AiRequest(
                AiPrivacyTier.MetadataOnly,
                "openai",
                "gpt-test",
                "recommendation",
                "Recommend a book",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["title"] = "Ogma",
                },
                [chunk]));
    }

    [Fact]
    public void AiRequest_MetadataOnly_AllowsMetadataFields()
    {
        var request = new AiRequest(
            AiPrivacyTier.MetadataOnly,
            "openai",
            "gpt-test",
            "recommendation",
            "Recommend a book",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = "Ogma",
                ["author"] = "Chwezi Core Systems",
            });

        Assert.Empty(request.ContentChunks);
        Assert.Equal("Ogma", request.MetadataFields["title"]);
    }

    [Fact]
    public void AiPayloadPreview_CharacterCount_IncludesExactFields()
    {
        var preview = new AiPayloadPreview(
            AiPrivacyTier.ContentAware,
            "anthropic",
            "claude-test",
            "recommendation",
            null,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = "Ogma",
            },
            [new AiContentChunk("BOOK1", "page:2", "chunk text")]);

        Assert.Equal(14 + 5 + 4 + 5 + 6 + 10, preview.CharacterCount);
    }
}
