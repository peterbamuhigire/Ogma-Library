using System.Globalization;
using System.Text;
using OgmaLibrary.Application.Ai;

namespace OgmaLibrary.Infrastructure.AI.Providers;

internal static class AiProviderPayload
{
    public static string BuildUserContent(AiRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Task: {request.QueryType}");
        if (!string.IsNullOrWhiteSpace(request.QueryText))
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Question: {request.QueryText}");
        }

        if (request.MetadataFields.Count > 0)
        {
            builder.AppendLine("Metadata:");
            foreach (KeyValuePair<string, string> field in request.MetadataFields.OrderBy(field => field.Key, StringComparer.Ordinal))
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"- {field.Key}: {field.Value}");
            }
        }

        if (request.ContentChunks.Count > 0)
        {
            builder.AppendLine("Content chunks:");
            foreach (AiContentChunk chunk in request.ContentChunks)
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"- {chunk.BookId} {chunk.Source}: {chunk.Text}");
            }
        }

        return builder.ToString();
    }
}
