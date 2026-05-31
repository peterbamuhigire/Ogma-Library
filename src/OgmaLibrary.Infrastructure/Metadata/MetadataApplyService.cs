using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Metadata;

/// <summary>
/// EF Core implementation of <see cref="IMetadataApplyService"/> that upserts
/// <c>BookMetadataFields</c> rows with full provenance, writes an audit event,
/// and delegates quality-score recalculation (FR-META-003, FR-META-004,
/// CTRL-OGMA-018).
/// </summary>
public sealed class MetadataApplyService : IMetadataApplyService
{
    private readonly CatalogueDbContext _context;
    private readonly IMetadataQualityService _qualityService;

    /// <summary>
    /// Initializes a new instance of <see cref="MetadataApplyService"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    /// <param name="qualityService">The quality score service.</param>
    public MetadataApplyService(
        CatalogueDbContext context,
        IMetadataQualityService qualityService)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(qualityService);
        _context = context;
        _qualityService = qualityService;
    }

    /// <inheritdoc />
    public async Task ApplyMergedMetadataAsync(
        string bookId,
        IReadOnlyList<AcceptedFieldProposal> acceptedProposals,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentNullException.ThrowIfNull(acceptedProposals);

        if (acceptedProposals.Count == 0)
        {
            return;
        }

        // Snapshot before state for audit.
        var beforeFields = await _context.BookMetadataFields
            .AsNoTracking()
            .Where(f => f.BookId == bookId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        string beforeJson = JsonSerializer.Serialize(
            beforeFields.Select(f => new { f.FieldName, f.Value, f.Source, f.Confidence }));

        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach (AcceptedFieldProposal proposal in acceptedProposals)
        {
            var existing = await _context.BookMetadataFields
                .FirstOrDefaultAsync(
                    f => f.BookId == bookId && f.FieldName == proposal.FieldName,
                    cancellationToken)
                .ConfigureAwait(false);

            if (existing is null)
            {
                _context.BookMetadataFields.Add(new BookMetadataFieldRow
                {
                    BookId = bookId,
                    FieldName = proposal.FieldName,
                    Value = proposal.AcceptedValue,
                    Source = proposal.Source,
                    SourceTimestamp = now,
                    Confidence = proposal.Confidence,
                    IsOverridden = proposal.IsOverridden,
                });
            }
            else
            {
                existing.Value = proposal.AcceptedValue;
                existing.Source = proposal.Source;
                existing.SourceTimestamp = now;
                existing.Confidence = proposal.Confidence;
                existing.IsOverridden = proposal.IsOverridden;
            }
        }

        // Snapshot after state.
        string afterJson = JsonSerializer.Serialize(
            acceptedProposals.Select(p => new { p.FieldName, p.AcceptedValue, p.Source, p.Confidence }));

        // Audit event.
        _context.AuditEvents.Add(new AuditEventRow
        {
            EventType = "MetadataApplied",
            EntityId = bookId,
            EntityType = "Book",
            BeforeJson = beforeJson,
            AfterJson = afterJson,
            Timestamp = now,
            IsLocalOnly = true,
        });

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Recalculate quality score after metadata write.
        await _qualityService.RecalculateAsync(bookId, cancellationToken).ConfigureAwait(false);
    }
}
