using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Diagnostics;
using OgmaLibrary.Infrastructure.Pdf;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Uses PdfPig to extract Title, Author, Subject, and Creator from a PDF's
/// DocumentInformation dictionary (FR-META-001 precursor, Phase 05). Fields are
/// upserted with <c>Source = "PDF"</c> and <c>Confidence = 0.5</c>.
/// </summary>
public sealed class MetadataExtractionService : IMetadataExtractionService
{
    private readonly ILogger _logger = NullLogger.Instance;
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;
    private readonly IPdfRendererFactory _rendererFactory;

    /// <summary>
    /// Initializes a new instance of <see cref="MetadataExtractionService"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    /// <param name="rendererFactory">The PDF renderer factory used for metadata reads.</param>
    internal MetadataExtractionService(
        CatalogueDbContext context,
        IPdfRendererFactory? rendererFactory = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _rendererFactory = rendererFactory ?? new PdfiumAdapterFactory();
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
        _rendererFactory = serviceProvider.GetService<IPdfRendererFactory>()
            ?? new PdfiumAdapterFactory();
        _logger = serviceProvider.GetService<ILogger<MetadataExtractionService>>() ??
            (ILogger)NullLogger.Instance;
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

    private List<(string FieldName, string Value)> ExtractFields(string filePath)
    {
        var result = new List<(string, string)>();

        try
        {
            using IPdfRenderer renderer = _rendererFactory.Open(filePath);
            PdfDocumentMetadata metadata = renderer.ReadDocumentMetadata();

            if (!string.IsNullOrWhiteSpace(metadata.Title))
            {
                result.Add(("Title", metadata.Title.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(metadata.Author))
            {
                result.Add(("Author", metadata.Author.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(metadata.Subject))
            {
                result.Add(("Subject", metadata.Subject.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(metadata.Creator))
            {
                result.Add(("Creator", metadata.Creator.Trim()));
            }
        }
        catch (Exception exception)
        {
            // Intentionally ignored: lenient extraction; bad or encrypted PDFs return an empty list, never throw.
            InfrastructureLog.BestEffortStepFailed(_logger, exception, nameof(MetadataExtractionService), "ingestion.extract_fields");
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
