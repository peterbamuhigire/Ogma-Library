using OgmaLibrary.Domain;

namespace OgmaLibrary.Domain.Ai;

/// <summary>Catalogue field that justified an AI advisor recommendation.</summary>
public enum RecommendationMatchField
{
    /// <summary>The recommendation matched the book title.</summary>
    Title = 0,

    /// <summary>The recommendation matched an author name.</summary>
    Author = 1,

    /// <summary>The recommendation matched catalogue tags.</summary>
    Tags = 2,

    /// <summary>The recommendation matched a book description or summary.</summary>
    Description = 3,

    /// <summary>The recommendation matched Phase 11 semantic ranking evidence.</summary>
    SemanticScore = 4,
}

/// <summary>Difficulty band used when presenting reading-plan steps.</summary>
public enum DifficultyLabel
{
    /// <summary>Best for first exposure to the subject.</summary>
    Introductory = 0,

    /// <summary>Builds a stable base after the first exposure.</summary>
    Foundational = 1,

    /// <summary>Assumes some background knowledge.</summary>
    Intermediate = 2,

    /// <summary>Requires sustained attention or prior subject fluency.</summary>
    Advanced = 3,

    /// <summary>Targets specialist or expert-level reading.</summary>
    Expert = 4,
}

/// <summary>Local catalogue provenance that supports an AI recommendation.</summary>
public sealed record ProvenanceItem
{
    /// <summary>Creates a local provenance item and validates required evidence text.</summary>
    /// <param name="bookId">The local book identifier that supplied the evidence.</param>
    /// <param name="matchField">The catalogue field that matched.</param>
    /// <param name="fieldValue">The field value or score text used for explanation.</param>
    public ProvenanceItem(BookId bookId, RecommendationMatchField matchField, string fieldValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId.Value);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldValue);

        BookId = bookId;
        MatchField = matchField;
        FieldValue = fieldValue;
    }

    /// <summary>The local book identifier that supplied the evidence.</summary>
    public BookId BookId { get; }

    /// <summary>The catalogue field that matched.</summary>
    public RecommendationMatchField MatchField { get; }

    /// <summary>The field value or score text used for explanation.</summary>
    public string FieldValue { get; }
}

/// <summary>Human-readable explanation and source provenance for a recommendation.</summary>
public sealed record RecommendationExplanation
{
    /// <summary>Creates an explanation and validates structural completeness.</summary>
    /// <param name="summary">Short explanation shown on the recommendation card.</param>
    /// <param name="provenanceItems">Local evidence items that explain the recommendation.</param>
    /// <param name="modelUsed">Provider model used to produce the explanation.</param>
    /// <param name="tier">AI privacy tier used for the recommendation.</param>
    public RecommendationExplanation(
        string summary,
        IReadOnlyList<ProvenanceItem> provenanceItems,
        string modelUsed,
        AiPrivacyTier tier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentNullException.ThrowIfNull(provenanceItems);
        if (provenanceItems.Count == 0)
        {
            throw new ArgumentException("A recommendation explanation must include provenance.", nameof(provenanceItems));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(modelUsed);

        Summary = summary;
        ProvenanceItems = provenanceItems;
        ModelUsed = modelUsed;
        Tier = tier;
    }

    /// <summary>Short explanation shown on the recommendation card.</summary>
    public string Summary { get; }

    /// <summary>Local evidence items that explain the recommendation.</summary>
    public IReadOnlyList<ProvenanceItem> ProvenanceItems { get; }

    /// <summary>Provider model used to produce the explanation.</summary>
    public string ModelUsed { get; }

    /// <summary>AI privacy tier used for the recommendation.</summary>
    public AiPrivacyTier Tier { get; }
}

/// <summary>Ranked recommendation card returned by the AI advisor.</summary>
public sealed record RecommendationCard
{
    /// <summary>Creates a recommendation card and validates structural invariants.</summary>
    /// <param name="bookId">The recommended local book identifier.</param>
    /// <param name="rank">One-based rank in the returned recommendation list.</param>
    /// <param name="confidence">Calibrated confidence score for the recommendation signal.</param>
    /// <param name="explanation">Human-readable explanation and local provenance.</param>
    public RecommendationCard(
        BookId bookId,
        int rank,
        ConfidenceScore confidence,
        RecommendationExplanation explanation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId.Value);
        if (rank < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rank), rank, "Recommendation rank must be one-based.");
        }

        ArgumentNullException.ThrowIfNull(explanation);

        BookId = bookId;
        Rank = rank;
        Confidence = confidence;
        Explanation = explanation;
    }

    /// <summary>The recommended local book identifier.</summary>
    public BookId BookId { get; }

    /// <summary>One-based rank in the returned recommendation list.</summary>
    public int Rank { get; }

    /// <summary>Calibrated confidence score for the recommendation signal.</summary>
    public ConfidenceScore Confidence { get; }

    /// <summary>Human-readable explanation and local provenance.</summary>
    public RecommendationExplanation Explanation { get; }
}

/// <summary>One ordered step in a generated reading plan.</summary>
public sealed record ReadingPlanStep
{
    /// <summary>Creates a reading-plan step and validates required fields.</summary>
    /// <param name="bookId">The local book identifier to read at this step.</param>
    /// <param name="rationale">Why this book belongs at this point in the plan.</param>
    /// <param name="difficulty">Difficulty label for the step.</param>
    /// <param name="estimatedReadingDays">Optional estimated reading time in days.</param>
    public ReadingPlanStep(
        BookId bookId,
        string rationale,
        DifficultyLabel difficulty,
        int? estimatedReadingDays)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId.Value);
        ArgumentException.ThrowIfNullOrWhiteSpace(rationale);
        if (estimatedReadingDays is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(estimatedReadingDays),
                estimatedReadingDays,
                "Estimated reading days must be positive when provided.");
        }

        BookId = bookId;
        Rationale = rationale;
        Difficulty = difficulty;
        EstimatedReadingDays = estimatedReadingDays;
    }

    /// <summary>The local book identifier to read at this step.</summary>
    public BookId BookId { get; }

    /// <summary>Why this book belongs at this point in the plan.</summary>
    public string Rationale { get; }

    /// <summary>Difficulty label for the step.</summary>
    public DifficultyLabel Difficulty { get; }

    /// <summary>Optional estimated reading time in days.</summary>
    public int? EstimatedReadingDays { get; }
}

/// <summary>Checkpoint prompt shown after a reading-plan step.</summary>
public sealed record Checkpoint
{
    /// <summary>Creates a checkpoint and validates its placement.</summary>
    /// <param name="afterStepIndex">Zero-based index after which the checkpoint appears.</param>
    /// <param name="description">Checkpoint description.</param>
    public Checkpoint(int afterStepIndex, string description)
    {
        if (afterStepIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(afterStepIndex), afterStepIndex, "Checkpoint index cannot be negative.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        AfterStepIndex = afterStepIndex;
        Description = description;
    }

    /// <summary>Zero-based index after which the checkpoint appears.</summary>
    public int AfterStepIndex { get; }

    /// <summary>Checkpoint description.</summary>
    public string Description { get; }
}

/// <summary>Structured reading plan generated from the user's local catalogue.</summary>
public sealed record ReadingPlan
{
    /// <summary>Creates a reading plan and validates structural completeness.</summary>
    /// <param name="goal">The user's reading or learning objective.</param>
    /// <param name="steps">Ordered local-book steps.</param>
    /// <param name="checkpoints">Optional checkpoints between steps.</param>
    public ReadingPlan(
        string goal,
        IReadOnlyList<ReadingPlanStep> steps,
        IReadOnlyList<Checkpoint> checkpoints)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goal);
        ArgumentNullException.ThrowIfNull(steps);
        if (steps.Count == 0)
        {
            throw new ArgumentException("A reading plan must include at least one step.", nameof(steps));
        }

        ArgumentNullException.ThrowIfNull(checkpoints);

        Goal = goal;
        Steps = steps;
        Checkpoints = checkpoints;
    }

    /// <summary>The user's reading or learning objective.</summary>
    public string Goal { get; }

    /// <summary>Ordered local-book steps.</summary>
    public IReadOnlyList<ReadingPlanStep> Steps { get; }

    /// <summary>Optional checkpoints between steps.</summary>
    public IReadOnlyList<Checkpoint> Checkpoints { get; }
}

/// <summary>Local evidence citation used by the V2 answer-mode scaffold.</summary>
public sealed record AnswerCitation
{
    /// <summary>Creates an answer citation and validates required local evidence.</summary>
    /// <param name="bookId">The local book identifier that supplied the citation.</param>
    /// <param name="pageNumber">Optional one-based page number.</param>
    /// <param name="chunkId">Optional local text chunk identifier.</param>
    /// <param name="relevantText">Short citation excerpt from the local source.</param>
    /// <param name="confidence">Retrieval confidence for this citation.</param>
    public AnswerCitation(
        BookId bookId,
        int? pageNumber,
        string? chunkId,
        string relevantText,
        ConfidenceScore confidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId.Value);
        if (pageNumber is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber), pageNumber, "Page number must be positive when provided.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(relevantText);

        BookId = bookId;
        PageNumber = pageNumber;
        ChunkId = chunkId;
        RelevantText = relevantText;
        Confidence = confidence;
    }

    /// <summary>The local book identifier that supplied the citation.</summary>
    public BookId BookId { get; }

    /// <summary>Optional one-based page number.</summary>
    public int? PageNumber { get; }

    /// <summary>Optional local text chunk identifier.</summary>
    public string? ChunkId { get; }

    /// <summary>Short citation excerpt from the local source.</summary>
    public string RelevantText { get; }

    /// <summary>Retrieval confidence for this citation.</summary>
    public ConfidenceScore Confidence { get; }
}
