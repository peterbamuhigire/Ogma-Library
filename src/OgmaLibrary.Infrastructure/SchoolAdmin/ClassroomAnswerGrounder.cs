using System.Text.RegularExpressions;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.SchoolAdmin;

namespace OgmaLibrary.Infrastructure.SchoolAdmin;

/// <summary>Verifies classroom AI citations against Host-local catalogue metadata.</summary>
internal static partial class ClassroomAnswerGrounder
{
    public static GroundedAnswer Ground(
        string answer,
        IReadOnlyList<BookSummaryProjection> localCandidates)
    {
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(localCandidates);

        Dictionary<string, BookSummaryProjection> candidates = localCandidates
            .GroupBy(candidate => candidate.BookId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var citations = new List<GroundedCitation>();

        string groundedAnswer = BookCitationMarker().Replace(answer, match =>
        {
            string bookId = match.Groups["id"].Value.Trim();
            if (!candidates.TryGetValue(bookId, out BookSummaryProjection? candidate))
            {
                return string.Empty;
            }

            int? pageNumber = null;
            if (match.Groups["page"].Success &&
                int.TryParse(match.Groups["page"].Value, out int parsedPage) &&
                parsedPage > 0)
            {
                pageNumber = parsedPage;
            }

            if (!citations.Any(citation =>
                    string.Equals(citation.BookId, bookId, StringComparison.Ordinal) &&
                    citation.PageNumber == pageNumber))
            {
                citations.Add(new GroundedCitation(bookId, candidate.Title, pageNumber));
            }

            return string.Empty;
        }).Trim();

        return citations.Count == 0
            ? new GroundedAnswer("No local evidence found.", [])
            : new GroundedAnswer(NormalizeWhitespace(groundedAnswer), citations);
    }

    private static string NormalizeWhitespace(string value) =>
        WhitespaceRun().Replace(value, " ").Trim();

    [GeneratedRegex(@"\[\[book:(?<id>[A-Za-z0-9_-]{1,128})(?::p(?<page>[0-9]{1,6}))?\]\]", RegexOptions.CultureInvariant)]
    private static partial Regex BookCitationMarker();

    [GeneratedRegex(@"\s{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRun();
}

internal sealed record GroundedAnswer(
    string Answer,
    IReadOnlyList<GroundedCitation> Citations);
