using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAnnotationRepository"/> against
/// <see cref="CatalogueDbContext"/> (FR-READ-008, ADR-0008).
/// Annotations are persisted in the catalogue before being reported saved (NFR-OGMA-008).
/// </summary>
public sealed class AnnotationRepository : IAnnotationRepository
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>
    /// Initializes a new instance of <see cref="AnnotationRepository"/>.
    /// </summary>
    /// <param name="context">The catalogue DB context.</param>
    internal AnnotationRepository(CatalogueDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="AnnotationRepository"/>.
    /// </summary>
    public AnnotationRepository(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Annotation>> ListForFileAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        // Find the book with this relative path via BookFiles.
        List<AnnotationRow> rows = await context.Annotations
            .AsNoTracking()
            .Include(a => a.Body)
            .Where(a => context.BookFiles
                .Any(f => f.RelativePath == relativePath && f.BookId == a.BookId))
            .OrderBy(a => a.Page)
            .ThenBy(a => a.CreatedUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(MapToDomain).ToList();
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        string relativePath,
        Annotation annotation,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(annotation);

        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;

        // Resolve the book from the file path.
        BookFileRow? fileRow = await context.BookFiles
            .FirstOrDefaultAsync(f => f.RelativePath == relativePath, cancellationToken)
            .ConfigureAwait(false);

        if (fileRow is null)
        {
            throw new InvalidOperationException(
                $"No book file found for relative path '{relativePath}'. " +
                "Import the book before saving annotations.");
        }

        AnnotationRow? existing = await context.Annotations
            .Include(a => a.Body)
            .FirstOrDefaultAsync(a => a.AnnotationId == annotation.Id, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            var row = new AnnotationRow
            {
                AnnotationId = annotation.Id,
                BookId = fileRow.BookId,
                Page = annotation.PageIndex,
                Type = (int)annotation.Kind,
                ColorKey = annotation.HighlightColor,
                CreatedUtc = DateTimeOffset.UtcNow,
                ModifiedUtc = DateTimeOffset.UtcNow,
                Body = new AnnotationBodyRow
                {
                    AnnotationId = annotation.Id,
                    NoteText = annotation.NoteText,
                    RectJson = FormatRect(annotation.SelectionStart, annotation.SelectionEnd),
                },
            };
            context.Annotations.Add(row);
        }
        else
        {
            existing.ColorKey = annotation.HighlightColor;
            existing.ModifiedUtc = DateTimeOffset.UtcNow;
            if (existing.Body is not null)
            {
                existing.Body.NoteText = annotation.NoteText;
                existing.Body.RectJson = FormatRect(annotation.SelectionStart, annotation.SelectionEnd);
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Annotation MapToDomain(AnnotationRow row)
    {
        (double x1, double y1, double x2, double y2) = ParseRect(row.Body?.RectJson);
        return new Annotation
        {
            Id = row.AnnotationId,
            Kind = row.Type == 0 ? AnnotationKind.Highlight : AnnotationKind.Note,
            PageIndex = row.Page,
            SelectionStart = (x1, y1),
            SelectionEnd = (x2, y2),
            HighlightColor = row.ColorKey,
            NoteText = row.Body?.NoteText,
        };
    }

    private static string FormatRect((double X, double Y) start, (double X, double Y) end)
        => $"{{\"x1\":{start.X:G},\"y1\":{start.Y:G},\"x2\":{end.X:G},\"y2\":{end.Y:G}}}";

    private static (double, double, double, double) ParseRect(string? rectJson)
    {
        if (string.IsNullOrWhiteSpace(rectJson))
        {
            return (0, 0, 0, 0);
        }

        // Minimal JSON parsing to avoid a full System.Text.Json dependency here.
        try
        {
            double Extract(string key)
            {
                int idx = rectJson.IndexOf($"\"{key}\":", StringComparison.Ordinal);
                if (idx < 0)
                {
                    return 0;
                }
                idx += key.Length + 3;
                int end = rectJson.IndexOfAny([',', '}'], idx);
                return double.Parse(rectJson[idx..end], System.Globalization.CultureInfo.InvariantCulture);
            }

            return (Extract("x1"), Extract("y1"), Extract("x2"), Extract("y2"));
        }
        catch (FormatException)
        {
            return (0, 0, 0, 0);
        }
    }
}
