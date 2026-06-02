using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.SchoolAdmin;
using OgmaLibrary.Infrastructure.SchoolAdmin;

namespace OgmaLibrary.Tests.SchoolAdmin;

/// <summary>Phase 18 classroom answer grounding tests.</summary>
public sealed class ClassroomAnswerGrounderTests
{
    [Fact]
    public void Ground_RemovesFabricatedCitationsAndKeepsLocalEvidence()
    {
        BookSummaryProjection local = CreateBook("01LOCALBOOK000000000001", "Local Evidence");

        GroundedAnswer result = ClassroomAnswerGrounder.Ground(
            "Use the local book [[book:01LOCALBOOK000000000001:p4]] and ignore this [[book:01FAKEBOOK000000000001]].",
            [local]);

        GroundedCitation citation = Assert.Single(result.Citations);
        Assert.Equal("01LOCALBOOK000000000001", citation.BookId);
        Assert.Equal("Local Evidence", citation.Title);
        Assert.Equal(4, citation.PageNumber);
        Assert.DoesNotContain("01FAKEBOOK000000000001", result.Answer, StringComparison.Ordinal);
        Assert.DoesNotContain("[[book:", result.Answer, StringComparison.Ordinal);
    }

    [Fact]
    public void Ground_ReturnsNoLocalEvidenceWhenProviderCitesNothingLocal()
    {
        GroundedAnswer result = ClassroomAnswerGrounder.Ground(
            "This answer has no verified citation [[book:01FAKEBOOK000000000001]].",
            [CreateBook("01LOCALBOOK000000000001", "Local Evidence")]);

        Assert.Equal("No local evidence found.", result.Answer);
        Assert.Empty(result.Citations);
    }

    private static BookSummaryProjection CreateBook(string bookId, string title) =>
        new(
            bookId,
            title,
            Authors: ["A. Author"],
            CoverRelativePath: null,
            Status: 0,
            Rating: null,
            ShelfIds: [],
            ReadingProgressPct: null,
            IsAvailable: true,
            Year: 2026);
}
