using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Repositories;

/// <summary>Persists reviewed identity grouping with before-image undo history.</summary>
public sealed class IdentityGroupingService : IIdentityGroupingService
{
    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    /// <summary>Test constructor using an existing context.</summary>
    internal IdentityGroupingService(CatalogueDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>DI constructor using independent catalogue contexts.</summary>
    [ActivatorUtilitiesConstructor]
    public IdentityGroupingService(
        IDbContextFactory<CatalogueDbContext> contextFactory,
        IServiceProvider serviceProvider)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        ArgumentNullException.ThrowIfNull(serviceProvider);
    }

    /// <inheritdoc />
    public async Task<IdentityGroupDescriptor> CreateAsync(
        IdentityGroupKind kind,
        IReadOnlyList<string> occurrenceIds,
        string actor,
        CancellationToken cancellationToken = default)
    {
        ValidateMembers(occurrenceIds);
        ValidateActor(actor);
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        string[] members = NormalizeMembers(occurrenceIds);
        bool alreadyGrouped = await context.IdentityGroupMembers
            .AnyAsync(member => members.Contains(member.FileOccurrenceId) && member.IsActive, cancellationToken)
            .ConfigureAwait(false);
        if (alreadyGrouped)
        {
            throw new InvalidOperationException("An occurrence already belongs to an active identity group.");
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        var group = new IdentityGroupRow
        {
            IdentityGroupId = CanonicalIdGenerator.NewId(),
            Kind = (int)kind,
            Version = 1,
            CreatedUtc = now,
            UpdatedUtc = now,
        };
        context.IdentityGroups.Add(group);
        context.IdentityGroupMembers.AddRange(members.Select(member => new IdentityGroupMemberRow
        {
            IdentityGroupId = group.IdentityGroupId,
            FileOccurrenceId = member,
            IsActive = true,
            UpdatedUtc = now,
        }));
        context.IdentityGroupChanges.Add(new IdentityGroupChangeRow
        {
            IdentityGroupId = group.IdentityGroupId,
            Operation = "create",
            BeforeMembersJson = "[]",
            AfterMembersJson = JsonSerializer.Serialize(members),
            Actor = actor.Trim(),
            CreatedUtc = now,
        });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDescriptor(group, members);
    }

    /// <inheritdoc />
    public Task<IdentityGroupDescriptor> MergeAsync(
        string groupId,
        IReadOnlyList<string> occurrenceIds,
        string actor,
        CancellationToken cancellationToken = default) =>
        MutateAsync(groupId, occurrenceIds, actor, "merge", addMembers: true, cancellationToken);

    /// <inheritdoc />
    public Task<IdentityGroupDescriptor> SplitAsync(
        string groupId,
        IReadOnlyList<string> occurrenceIds,
        string actor,
        CancellationToken cancellationToken = default) =>
        MutateAsync(groupId, occurrenceIds, actor, "split", addMembers: false, cancellationToken);

    /// <inheritdoc />
    public async Task<IdentityGroupDescriptor> UndoLastAsync(
        string groupId,
        string actor,
        CancellationToken cancellationToken = default)
    {
        ValidateGroupId(groupId);
        ValidateActor(actor);
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        IdentityGroupRow group = await FindGroupAsync(context, groupId, cancellationToken).ConfigureAwait(false);
        IdentityGroupChangeRow? last = await context.IdentityGroupChanges
            .Where(change => change.IdentityGroupId == groupId && change.Operation != "undo")
            .OrderByDescending(change => change.IdentityGroupChangeId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (last is null)
        {
            throw new InvalidOperationException("The identity group has no reversible mutation.");
        }

        string[] before = DeserializeMembers(last.BeforeMembersJson);
        List<IdentityGroupMemberRow> members = await context.IdentityGroupMembers
            .Where(member => member.IdentityGroupId == groupId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (IdentityGroupMemberRow member in members)
        {
            member.IsActive = before.Contains(member.FileOccurrenceId, StringComparer.Ordinal);
            member.UpdatedUtc = now;
        }

        group.Version++;
        group.UpdatedUtc = now;
        context.IdentityGroupChanges.Add(new IdentityGroupChangeRow
        {
            IdentityGroupId = groupId,
            Operation = "undo",
            BeforeMembersJson = last.AfterMembersJson,
            AfterMembersJson = JsonSerializer.Serialize(before),
            Actor = actor.Trim(),
            CreatedUtc = now,
        });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDescriptor(group, before);
    }

    /// <inheritdoc />
    public async Task<IdentityGroupDescriptor?> FindByOccurrenceAsync(
        string occurrenceId,
        CancellationToken cancellationToken = default)
    {
        ValidateOccurrenceId(occurrenceId);
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        IdentityGroupMemberRow? member = await context.IdentityGroupMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.FileOccurrenceId == occurrenceId && row.IsActive, cancellationToken)
            .ConfigureAwait(false);
        if (member is null)
        {
            return null;
        }

        IdentityGroupRow group = await FindGroupAsync(context, member.IdentityGroupId, cancellationToken).ConfigureAwait(false);
        string[] active = await context.IdentityGroupMembers
            .AsNoTracking()
            .Where(row => row.IdentityGroupId == group.IdentityGroupId && row.IsActive)
            .OrderBy(row => row.FileOccurrenceId)
            .Select(row => row.FileOccurrenceId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return ToDescriptor(group, active);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IdentityGroupBookMembership>> FindBookMembershipsAsync(
        IReadOnlyList<string> bookIds,
        bool includeWorkGroups,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bookIds);
        string[] normalizedBookIds = bookIds
            .Where(bookId => !string.IsNullOrWhiteSpace(bookId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (normalizedBookIds.Length == 0)
        {
            return [];
        }

        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        if (includeWorkGroups)
        {
            var query =
                from alias in lease.Context.LegacyIdentityAliases.AsNoTracking()
                join itemOccurrence in lease.Context.CatalogueItemOccurrences.AsNoTracking()
                    on alias.CatalogueItemId equals itemOccurrence.CatalogueItemId
                join member in lease.Context.IdentityGroupMembers.AsNoTracking()
                    on itemOccurrence.FileOccurrenceId equals member.FileOccurrenceId
                join identityGroup in lease.Context.IdentityGroups.AsNoTracking()
                    on member.IdentityGroupId equals identityGroup.IdentityGroupId
                where normalizedBookIds.Contains(alias.LegacyBookId) && member.IsActive
                select new
                {
                    BookId = alias.LegacyBookId,
                    GroupId = identityGroup.IdentityGroupId,
                    Kind = identityGroup.Kind,
                };

            var rows = await query
                .Distinct()
                .OrderBy(item => item.GroupId)
                .ThenBy(item => item.BookId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            return rows
                .Select(item => new IdentityGroupBookMembership(
                    item.BookId,
                    item.GroupId,
                    (IdentityGroupKind)item.Kind))
                .ToArray();
        }

        var editionQuery =
            from alias in lease.Context.LegacyIdentityAliases.AsNoTracking()
            join itemOccurrence in lease.Context.CatalogueItemOccurrences.AsNoTracking()
                on alias.CatalogueItemId equals itemOccurrence.CatalogueItemId
            join member in lease.Context.IdentityGroupMembers.AsNoTracking()
                on itemOccurrence.FileOccurrenceId equals member.FileOccurrenceId
            join identityGroup in lease.Context.IdentityGroups.AsNoTracking()
                on member.IdentityGroupId equals identityGroup.IdentityGroupId
            where normalizedBookIds.Contains(alias.LegacyBookId) &&
                  member.IsActive &&
                  identityGroup.Kind == (int)IdentityGroupKind.Edition
            select new
            {
                BookId = alias.LegacyBookId,
                GroupId = identityGroup.IdentityGroupId,
                Kind = identityGroup.Kind,
            };
        var editionRows = await editionQuery
            .Distinct()
            .OrderBy(item => item.GroupId)
            .ThenBy(item => item.BookId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return editionRows
            .Select(item => new IdentityGroupBookMembership(
                item.BookId,
                item.GroupId,
                (IdentityGroupKind)item.Kind))
            .ToArray();
    }

    private async Task<IdentityGroupDescriptor> MutateAsync(
        string groupId,
        IReadOnlyList<string> occurrenceIds,
        string actor,
        string operation,
        bool addMembers,
        CancellationToken cancellationToken)
    {
        ValidateGroupId(groupId);
        ValidateMembers(occurrenceIds);
        ValidateActor(actor);
        using ContextLease lease = await CreateLeaseAsync(cancellationToken).ConfigureAwait(false);
        CatalogueDbContext context = lease.Context;
        IdentityGroupRow group = await FindGroupAsync(context, groupId, cancellationToken).ConfigureAwait(false);
        List<IdentityGroupMemberRow> rows = await context.IdentityGroupMembers
            .Where(member => member.IdentityGroupId == groupId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        string[] before = rows.Where(row => row.IsActive)
            .Select(row => row.FileOccurrenceId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        HashSet<string> active = before.ToHashSet(StringComparer.Ordinal);
        string[] requested = NormalizeMembers(occurrenceIds);
        if (addMembers)
        {
            active.UnionWith(requested);
        }
        else
        {
            active.ExceptWith(requested);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (string memberId in requested.Except(rows.Select(row => row.FileOccurrenceId), StringComparer.Ordinal))
        {
            rows.Add(new IdentityGroupMemberRow
            {
                IdentityGroupId = groupId,
                FileOccurrenceId = memberId,
                UpdatedUtc = now,
            });
            context.IdentityGroupMembers.Add(rows[^1]);
        }

        foreach (IdentityGroupMemberRow row in rows)
        {
            row.IsActive = active.Contains(row.FileOccurrenceId);
            row.UpdatedUtc = now;
        }

        string[] after = active.OrderBy(id => id, StringComparer.Ordinal).ToArray();
        group.Version++;
        group.UpdatedUtc = now;
        context.IdentityGroupChanges.Add(new IdentityGroupChangeRow
        {
            IdentityGroupId = groupId,
            Operation = operation,
            BeforeMembersJson = JsonSerializer.Serialize(before),
            AfterMembersJson = JsonSerializer.Serialize(after),
            Actor = actor.Trim(),
            CreatedUtc = now,
        });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDescriptor(group, after);
    }

    private static IdentityGroupDescriptor ToDescriptor(IdentityGroupRow group, IEnumerable<string> members) =>
        new(group.IdentityGroupId, (IdentityGroupKind)group.Kind, members.Order(StringComparer.Ordinal).ToArray(), group.Version, group.UpdatedUtc);

    private static string[] DeserializeMembers(string json) =>
        JsonSerializer.Deserialize<string[]>(json) ?? [];

    private static string[] NormalizeMembers(IEnumerable<string> members) =>
        members.Select(member =>
                new FileOccurrenceId(member).Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(member => member, StringComparer.Ordinal)
            .ToArray();

    private static void ValidateMembers(IReadOnlyList<string> members)
    {
        ArgumentNullException.ThrowIfNull(members);
        if (members.Count == 0)
        {
            throw new ArgumentException("At least one occurrence is required.", nameof(members));
        }
    }

    private static void ValidateOccurrenceId(string value) => _ = new FileOccurrenceId(value);

    private static void ValidateGroupId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 128 || value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new ArgumentException(
                "A stable identity must contain 1 to 128 non-whitespace, non-control characters.",
                nameof(value));
        }
    }

    private static void ValidateActor(string actor) =>
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);

    private static async Task<IdentityGroupRow> FindGroupAsync(
        CatalogueDbContext context,
        string groupId,
        CancellationToken cancellationToken) => await context.IdentityGroups
            .FirstOrDefaultAsync(row => row.IdentityGroupId == groupId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Identity group '{groupId}' was not found.");

    private async Task<ContextLease> CreateLeaseAsync(CancellationToken cancellationToken)
    {
        if (_contextFactory is not null)
        {
            CatalogueDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            return new ContextLease(context, ownsContext: true);
        }

        return new ContextLease(_context!, ownsContext: false);
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
