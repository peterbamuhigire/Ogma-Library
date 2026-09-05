using System.Text.Json;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Domain;
using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.Infrastructure.AI.Advisor;

/// <summary>Parses structured reading-plan provider output.</summary>
public sealed class ReadingPlanParser : IReadingPlanParser
{
    /// <inheritdoc />
    public ReadingPlan Parse(string responseText, IReadOnlyList<BookMetadataDto> localCandidates)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responseText);
        ArgumentNullException.ThrowIfNull(localCandidates);

        HashSet<string> localIds = localCandidates.Select(candidate => candidate.BookId).ToHashSet(StringComparer.Ordinal);
        try
        {
            using JsonDocument document = JsonDocument.Parse(responseText);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new AdvisorParseException("Reading plan response must be a JSON object.");
            }

            string goal = GetRequiredString(root, "goal", "Goal");
            List<ReadingPlanStep> steps = ReadSteps(root, localIds);
            List<Checkpoint> checkpoints = ReadCheckpoints(root);
            return new ReadingPlan(goal, steps, checkpoints);
        }
        catch (JsonException)
        {
            throw new AdvisorParseException("Reading plan response was not valid JSON.");
        }
    }

    private static List<ReadingPlanStep> ReadSteps(JsonElement root, HashSet<string> localIds)
    {
        if (!TryGetProperty(root, out JsonElement stepsElement, "steps", "Steps") ||
            stepsElement.ValueKind != JsonValueKind.Array)
        {
            throw new AdvisorParseException("Reading plan response must include a steps array.");
        }

        List<ReadingPlanStep> steps = [];
        foreach (JsonElement step in stepsElement.EnumerateArray())
        {
            string bookId = GetRequiredString(step, "book_id", "bookId", "BookId");
            if (!localIds.Contains(bookId))
            {
                throw new AdvisorParseException($"Reading plan referenced non-local book id '{bookId}'.");
            }

            string rationale = GetRequiredString(step, "rationale", "Rationale");
            DifficultyLabel difficulty = ParseDifficulty(GetRequiredString(step, "difficulty", "Difficulty"));
            int? estimatedDays = GetOptionalInt(step, "estimated_reading_days", "estimatedReadingDays", "EstimatedReadingDays");
            steps.Add(new ReadingPlanStep(new BookId(bookId), rationale, difficulty, estimatedDays));
        }

        return steps;
    }

    private static List<Checkpoint> ReadCheckpoints(JsonElement root)
    {
        if (!TryGetProperty(root, out JsonElement checkpointsElement, "checkpoints", "Checkpoints"))
        {
            return [];
        }

        if (checkpointsElement.ValueKind != JsonValueKind.Array)
        {
            throw new AdvisorParseException("Reading plan checkpoints must be an array.");
        }

        List<Checkpoint> checkpoints = [];
        foreach (JsonElement checkpoint in checkpointsElement.EnumerateArray())
        {
            int afterStep = GetOptionalInt(checkpoint, "after_step", "afterStep", "AfterStepIndex") ??
                throw new AdvisorParseException("Reading plan checkpoint is missing after_step.");
            string description = GetRequiredString(checkpoint, "description", "Description");
            checkpoints.Add(new Checkpoint(afterStep, description));
        }

        return checkpoints;
    }

    private static DifficultyLabel ParseDifficulty(string value)
    {
        string normalized = value.Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        return normalized.ToUpperInvariant() switch
        {
            "INTRODUCTORY" or "BEGINNER" or "DEBUTANT" => DifficultyLabel.Introductory,
            "FOUNDATIONAL" or "FOUNDATION" => DifficultyLabel.Foundational,
            "INTERMEDIATE" => DifficultyLabel.Intermediate,
            "ADVANCED" or "AVANCE" => DifficultyLabel.Advanced,
            "EXPERT" => DifficultyLabel.Expert,
            _ => throw new AdvisorParseException($"Unknown reading-plan difficulty '{value}'."),
        };
    }

    private static string GetRequiredString(JsonElement element, params string[] names)
    {
        if (!TryGetProperty(element, out JsonElement property, names) || property.ValueKind != JsonValueKind.String)
        {
            throw new AdvisorParseException($"Reading plan response is missing '{names[0]}'.");
        }

        string? value = property.GetString();
        return string.IsNullOrWhiteSpace(value)
            ? throw new AdvisorParseException($"Reading plan response has empty '{names[0]}'.")
            : value;
    }

    private static int? GetOptionalInt(JsonElement element, params string[] names)
    {
        if (!TryGetProperty(element, out JsonElement property, names))
        {
            return null;
        }

        return property.TryGetInt32(out int value)
            ? value
            : throw new AdvisorParseException($"Reading plan response has invalid '{names[0]}'.");
    }

    private static bool TryGetProperty(JsonElement element, out JsonElement property, params string[] names)
    {
        foreach (string name in names)
        {
            if (element.TryGetProperty(name, out property))
            {
                return true;
            }
        }

        property = default;
        return false;
    }
}
