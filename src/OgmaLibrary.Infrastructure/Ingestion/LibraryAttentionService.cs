using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Reads and resolves the <c>FileIssues</c> "Needs attention" list (Sept-23 Phase 05,
/// T05.6). Issues are shown only for enabled, non-removed roots.
/// </summary>
public sealed class LibraryAttentionService : ILibraryAttentionService
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;
    private readonly IPdfFileValidityClassifier _classifier;

    /// <summary>DI constructor.</summary>
    /// <param name="contextFactory">The catalogue context factory.</param>
    /// <param name="classifier">The validity classifier used by Retry.</param>
    [ActivatorUtilitiesConstructor]
    public LibraryAttentionService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IPdfFileValidityClassifier classifier)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(classifier);
        _contextFactory = contextFactory;
        _classifier = classifier;
    }

    /// <summary>Test constructor using a direct context.</summary>
    internal LibraryAttentionService(CatalogueDbContext context, IPdfFileValidityClassifier classifier)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(classifier);
        _context = context;
        _classifier = classifier;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NeedsAttentionItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyDictionary<string, LibraryRootLocation> roots = await LibraryRootPaths
            .LoadAsync(lease.Context, cancellationToken)
            .ConfigureAwait(false);
        List<FileIssueRow> rows = await VisibleIssues(lease.Context, roots)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .OrderBy(row => row.RelativePath, StringComparer.CurrentCultureIgnoreCase)
            .Select(row => new NeedsAttentionItem(
                row.FileIssueId,
                row.LibraryRootId is null ? null : new LibraryRootId(row.LibraryRootId),
                row.LibraryRootId is not null && roots.TryGetValue(row.LibraryRootId, out LibraryRootLocation? root)
                    ? root.DisplayName
                    : string.Empty,
                Path.GetFileName(row.RelativePath.Replace('/', Path.DirectorySeparatorChar)),
                row.RelativePath,
                (FileValidity)row.Reason,
                row.DetectedUtc))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyDictionary<string, LibraryRootLocation> roots = await LibraryRootPaths
            .LoadAsync(lease.Context, cancellationToken)
            .ConfigureAwait(false);
        return await VisibleIssues(lease.Context, roots)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> RetryAsync(long issueId, CancellationToken cancellationToken = default)
    {
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        FileIssueRow? issue = await lease.Context.FileIssues
            .FirstOrDefaultAsync(row => row.FileIssueId == issueId, cancellationToken)
            .ConfigureAwait(false);
        if (issue is null)
        {
            return false;
        }

        string? path = await ResolveAsync(lease.Context, issue, cancellationToken).ConfigureAwait(false);
        if (path is null || !File.Exists(path))
        {
            // The file is gone: nothing left to fix.
            lease.Context.FileIssues.Remove(issue);
            await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        FileValidity validity = await _classifier.ClassifyAsync(path, cancellationToken).ConfigureAwait(false);
        issue.LastCheckedUtc = DateTimeOffset.UtcNow;
        issue.IsIgnored = false;
        if (validity.IsCataloguable())
        {
            // Valid now: drop the issue; the next scan of its root catalogues the file.
            lease.Context.FileIssues.Remove(issue);
            await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        issue.Reason = (int)validity;
        await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return false;
    }

    /// <inheritdoc />
    public async Task IgnoreAsync(long issueId, CancellationToken cancellationToken = default)
    {
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        FileIssueRow? issue = await lease.Context.FileIssues
            .FirstOrDefaultAsync(row => row.FileIssueId == issueId, cancellationToken)
            .ConfigureAwait(false);
        if (issue is null)
        {
            return;
        }

        issue.IsIgnored = true;
        await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string?> GetAbsolutePathAsync(long issueId, CancellationToken cancellationToken = default)
    {
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);
        FileIssueRow? issue = await lease.Context.FileIssues
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.FileIssueId == issueId, cancellationToken)
            .ConfigureAwait(false);
        return issue is null
            ? null
            : await ResolveAsync(lease.Context, issue, cancellationToken).ConfigureAwait(false);
    }

    private static IQueryable<FileIssueRow> VisibleIssues(
        CatalogueDbContext context,
        IReadOnlyDictionary<string, LibraryRootLocation> roots)
    {
        List<string> active = roots.Values
            .Where(root => root.IsActive)
            .Select(root => root.RootId)
            .ToList();
        return context.FileIssues
            .AsNoTracking()
            .Where(row => !row.IsIgnored)
            .Where(row => row.LibraryRootId == null || active.Contains(row.LibraryRootId));
    }

    private static async Task<string?> ResolveAsync(
        CatalogueDbContext context,
        FileIssueRow issue,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, LibraryRootLocation> roots = await LibraryRootPaths
            .LoadAsync(context, cancellationToken)
            .ConfigureAwait(false);
        return LibraryRootPaths.Resolve(roots, issue.LibraryRootId, issue.RelativePath, legacyRoot: null);
    }
}
