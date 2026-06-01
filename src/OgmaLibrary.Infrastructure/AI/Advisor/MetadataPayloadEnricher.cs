using OgmaLibrary.Application.Ai;

namespace OgmaLibrary.Infrastructure.AI.Advisor;

/// <summary>Builds bounded Tier-1 metadata payloads for recommendation prompts.</summary>
public sealed class MetadataPayloadEnricher : IMetadataPayloadEnricher
{
    private const int CandidateLimit = 50;
    private const int CharacterBudget = 12_000;
    private const int LongFieldLimit = 1_200;
    private const int ShortFieldLimit = 300;

    /// <inheritdoc />
    public MetadataPayload BuildPayload(IReadOnlyList<BookMetadataDto> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        List<BookMetadataDto> selected = [];
        Dictionary<string, string> fields = new(StringComparer.Ordinal);
        int estimated = 0;

        Add("schema", "phase13.recommendation.metadata.v1", ShortFieldLimit, fields, ref estimated);
        foreach (BookMetadataDto candidate in candidates.Take(CandidateLimit))
        {
            int index = selected.Count;
            int before = estimated;
            Add($"books.{index}.id", candidate.BookId, ShortFieldLimit, fields, ref estimated);
            Add($"books.{index}.title", candidate.Title, ShortFieldLimit, fields, ref estimated);
            Add($"books.{index}.authors", Join(candidate.Authors), ShortFieldLimit, fields, ref estimated);
            Add($"books.{index}.year", candidate.Year?.ToString(System.Globalization.CultureInfo.InvariantCulture), ShortFieldLimit, fields, ref estimated);
            Add($"books.{index}.tags", Join(candidate.Tags), ShortFieldLimit, fields, ref estimated);
            Add($"books.{index}.categories", Join(candidate.Categories), ShortFieldLimit, fields, ref estimated);
            Add($"books.{index}.description", candidate.Description, LongFieldLimit, fields, ref estimated);
            Add($"books.{index}.notes", candidate.Notes, LongFieldLimit, fields, ref estimated);

            if (estimated > CharacterBudget)
            {
                RemoveBook(index, fields);
                estimated = before;
                break;
            }

            selected.Add(candidate);
        }

        fields["books.count"] = selected.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        estimated += "books.count".Length + fields["books.count"].Length;
        return new MetadataPayload(selected, fields, estimated);
    }

    private static void Add(
        string key,
        string? value,
        int maxLength,
        Dictionary<string, string> fields,
        ref int estimated)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        string trimmed = value.Length > maxLength ? value[..maxLength] : value;
        fields[key] = trimmed;
        estimated += key.Length + trimmed.Length;
    }

    private static string? Join(IReadOnlyList<string> values) =>
        values.Count == 0 ? null : string.Join(", ", values.Where(value => !string.IsNullOrWhiteSpace(value)));

    private static void RemoveBook(int index, Dictionary<string, string> fields)
    {
        string prefix = $"books.{index}.";
        foreach (string key in fields.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
        {
            fields.Remove(key);
        }
    }
}
