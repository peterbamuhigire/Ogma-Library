using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>
    /// Initializes a new instance of <see cref="MetadataExtractionService"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    internal MetadataExtractionService(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MetadataExtractionService"/>.
    /// </summary>
    /// <param name="contextFactory">The catalogue DB context factory.</param>
    /// <param name="serviceProvider">The application service provider, used only to make DI constructor selection unambiguous.</param>
    [ActivatorUtilitiesConstructor]
    public MetadataExtractionService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _contextFactory = contextFactory;
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
            using ContextLease lease = await CreateLeaseAsync(cancellationToken)
                .ConfigureAwait(false);
            CatalogueDbContext context = lease.Context;

            var fields = await Task.Run(() => ExtractFields(absoluteFilePath), cancellationToken)
                .ConfigureAwait(false);

            foreach ((string fieldName, string value) in fields)
            {
                cancellationToken.ThrowIfCancellationRequested();

                BookMetadataFieldRow? existing = await context.BookMetadataFields
                    .FirstOrDefaultAsync(
                        f => f.BookId == bookId && f.FieldName == fieldName && f.Source == "PDF",
                        cancellationToken)
                    .ConfigureAwait(false);

                if (existing is null)
                {
                    context.BookMetadataFields.Add(new BookMetadataFieldRow
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

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return (true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (false, "Metadata extraction failed.");
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

    private async ValueTask<ContextLease> CreateLeaseAsync(CancellationToken cancellationToken)
    {
        if (_contextFactory is null)
        {
            return new ContextLease(_context!, ownsContext: false);
        }

        CatalogueDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        return new ContextLease(context, ownsContext: true);
    }

    private readonly struct ContextLease : IDisposable
    {
        public ContextLease(CatalogueDbContext context, bool ownsContext)
        {
            Context = context;
            _ownsContext = ownsContext;
        }

        private readonly bool _ownsContext;

        public CatalogueDbContext Context { get; }

        public void Dispose()
        {
            if (_ownsContext)
            {
                Context.Dispose();
            }
        }
    }
}
