using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.Infrastructure.AI;

/// <summary>Builds exact provider-neutral AI payload previews and audit hashes.</summary>
public sealed class AiPayloadBuilder : IAiPayloadBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public AiPayloadPreview BuildPreview(AiRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Tier == AiPrivacyTier.MetadataOnly && request.ContentChunks.Count > 0)
        {
            throw new AiTierViolationException("Metadata-only AI requests cannot include content chunks.");
        }

        if (request.Tier == AiPrivacyTier.Offline)
        {
            throw new AiDisabledException();
        }

        return new AiPayloadPreview(
            request.Tier,
            request.Provider,
            request.Model,
            request.QueryType,
            request.QueryText,
            request.MetadataFields,
            request.ContentChunks);
    }

    /// <inheritdoc />
    public string ComputePayloadHash(AiPayloadPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        var payload = new
        {
            tier = preview.Tier.ToString(),
            provider = preview.Provider,
            model = preview.Model,
            queryType = preview.QueryType,
            queryText = preview.QueryText,
            metadataFields = preview.MetadataFields
                .OrderBy(field => field.Key, StringComparer.Ordinal)
                .Select(field => new { field.Key, field.Value })
                .ToArray(),
            contentChunks = preview.ContentChunks
                .Select(chunk => new { chunk.BookId, chunk.Source, chunk.Text })
                .ToArray(),
        };
        string json = JsonSerializer.Serialize(payload, SerializerOptions);
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexStringLower(digest);
    }
}
