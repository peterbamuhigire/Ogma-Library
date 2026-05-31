using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using UglyToad.PdfPig;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Uses PdfPig to extract Title, Author, Subject, and Creator from a PDF's
/// DocumentInformation dictionary (FR-META-001 precursor, Phase 05). Fields are
/// upserted with <c>Source = "PDF"</c> and <c>Confidence = 0.5</c>.
/// </summary>
public sealed class MetadataExtractionService : IMetadataExtractionService
{
    private readonly CatalogueDbContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="MetadataExtractionService"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    public MetadataExtractionService(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<(bool Success, string? ErrorMessage)> ExtractAsync(
        string bookId,
        string absoluteFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentException.ThrowIfNullOrWhiteSpace(absoluteFilePath);

        try
        {
            var fields = await Task.Run(() => ExtractFields(absoluteFilePath), cancellationToken)
                .ConfigureAwait(false);

            foreach ((string fieldName, string value) in fields)
            {
                cancellationToken.ThrowIfCancellationRequested();

                BookMetadataFieldRow? existing = await _context.BookMetadataFields
                    .FirstOrDefaultAsync(
                        f => f.BookId == bookId && f.FieldName == fieldName && f.Source == "PDF",
                        cancellationToken)
                    .ConfigureAwait(false);

                if (existing is null)
                {
                    _context.BookMetadataFields.Add(new BookMetadataFieldRow
                    {
                        BookId = bookId,
                        FieldName = fieldName,
                        Value = value,
                        Source = "PDF",
                        Confidence = 0.5,
                        SourceTimestamp = DateTimeOffset.UtcNow,
                    });
                }
                else
                {
                    existing.Value = value;
                    existing.SourceTimestamp = DateTimeOffset.UtcNow;
                }
            }

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return (true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (false, ex.Message);
        }
    }

    private static List<(string FieldName, string Value)> ExtractFields(string filePath)
    {
        var result = new List<(string, string)>();

        try
        {
            using var document = PdfDocument.Open(filePath, new ParsingOptions { UseLenientParsing = true });
            var info = document.Information;

            if (!string.IsNullOrWhiteSpace(info.Title))
            {
                result.Add(("Title", info.Title.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(info.Author))
            {
                result.Add(("Author", info.Author.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(info.Subject))
            {
                result.Add(("Subject", info.Subject.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(info.Creator))
            {
                result.Add(("Creator", info.Creator.Trim()));
            }
        }
        catch (Exception)
        {
            // Lenient: bad/encrypted PDFs return empty list, never throw.
        }

        return result;
    }
}
