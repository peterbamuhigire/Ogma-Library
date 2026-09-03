using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Catalogue;

/// <summary>Phase 9 acceptance tests for audited reversible grouping.</summary>
public sealed class IdentityGroupingServiceTests : IDisposable
{
    private readonly CatalogueDbContext _context = CatalogueTestHelper.CreateInMemoryContext();
    private readonly IdentityGroupingService _service;

    public IdentityGroupingServiceTests() => _service = new IdentityGroupingService(_context);

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task MergeSplitAndUndo_PreserveBeforeImagesAndMembership()
    {
        string first = Occurrence(1);
        string second = Occurrence(2);
        string third = Occurrence(3);
        IdentityGroupDescriptor group = await _service.CreateAsync(
            IdentityGroupKind.Edition,
            [first, second],
            "local-user");

        IdentityGroupDescriptor merged = await _service.MergeAsync(
            group.GroupId,
            [third],
            "local-user");
        Assert.Equal(3, merged.ActiveOccurrenceIds.Count);
        Assert.Equal(2, merged.Version);

        IdentityGroupDescriptor split = await _service.SplitAsync(
            group.GroupId,
            [second],
            "local-user");
        Assert.Equal([first, third], split.ActiveOccurrenceIds);

        IdentityGroupDescriptor undone = await _service.UndoLastAsync(group.GroupId, "local-user");
        Assert.Equal([first, second, third], undone.ActiveOccurrenceIds);
        Assert.Equal(4, await _context.IdentityGroupChanges.CountAsync());
        Assert.Equal(
            undone.ActiveOccurrenceIds,
            (await _service.FindByOccurrenceAsync(second))!.ActiveOccurrenceIds);
    }

    [Fact]
    public async Task Create_RejectsActiveDuplicateMembership()
    {
        string occurrence = Occurrence(4);
        await _service.CreateAsync(IdentityGroupKind.Work, [occurrence], "local-user");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(
            IdentityGroupKind.Edition,
            [occurrence],
            "local-user"));
    }

    [Fact]
    public async Task FindBookMemberships_ResolvesEditionGroupsThroughCanonicalAliases()
    {
        string firstBook = "legacy-phase09-first";
        string secondBook = "legacy-phase09-second";
        string workId = IdentityId("work");
        string editionId = IdentityId("edition");
        string itemOne = IdentityId("item-one");
        string itemTwo = IdentityId("item-two");
        string rootId = IdentityId("root");
        string firstOccurrence = Occurrence(5);
        string secondOccurrence = Occurrence(6);

        _context.LibraryRoots.Add(new LibraryRootRow
        {
            LibraryRootId = rootId,
            DisplayName = "Phase 09 test root",
            RootStatus = 0,
            PermissionStatus = 0,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        _context.CanonicalWorks.Add(new CanonicalWorkRow
        {
            WorkId = workId,
            ResolutionState = 0,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        _context.CanonicalEditions.Add(new CanonicalEditionRow
        {
            EditionId = editionId,
            WorkId = workId,
            ResolutionState = 0,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        _context.CatalogueItems.AddRange(
            new CatalogueItemRow
            {
                CatalogueItemId = itemOne,
                WorkId = workId,
                EditionId = editionId,
                CreatedUtc = DateTimeOffset.UtcNow,
            },
            new CatalogueItemRow
            {
                CatalogueItemId = itemTwo,
                WorkId = workId,
                EditionId = editionId,
                CreatedUtc = DateTimeOffset.UtcNow,
            });
        _context.FileOccurrences.AddRange(
            new FileOccurrenceRow
            {
                FileOccurrenceId = firstOccurrence,
                LibraryRootId = rootId,
                RelativePath = "first.pdf",
                NormalizedRelativePath = "first.pdf",
                AvailabilityStatus = 0,
            },
            new FileOccurrenceRow
            {
                FileOccurrenceId = secondOccurrence,
                LibraryRootId = rootId,
                RelativePath = "second.pdf",
                NormalizedRelativePath = "second.pdf",
                AvailabilityStatus = 0,
            });
        _context.LegacyIdentityAliases.AddRange(
            new LegacyIdentityAliasRow
            {
                LegacyBookId = firstBook,
                CatalogueItemId = itemOne,
                WorkId = workId,
                EditionId = editionId,
                MigrationVersion = 1,
                CreatedUtc = DateTimeOffset.UtcNow,
            },
            new LegacyIdentityAliasRow
            {
                LegacyBookId = secondBook,
                CatalogueItemId = itemTwo,
                WorkId = workId,
                EditionId = editionId,
                MigrationVersion = 1,
                CreatedUtc = DateTimeOffset.UtcNow,
            });
        _context.CatalogueItemOccurrences.AddRange(
            new CatalogueItemOccurrenceRow { CatalogueItemId = itemOne, FileOccurrenceId = firstOccurrence },
            new CatalogueItemOccurrenceRow { CatalogueItemId = itemTwo, FileOccurrenceId = secondOccurrence });
        await _context.SaveChangesAsync();

        await _service.CreateAsync(IdentityGroupKind.Edition, [firstOccurrence, secondOccurrence], "local-user");

        IReadOnlyList<IdentityGroupBookMembership> memberships = await _service
            .FindBookMembershipsAsync([firstBook, secondBook], includeWorkGroups: false);

        Assert.Equal(2, memberships.Count);
        Assert.All(memberships, membership =>
        {
            Assert.Equal(IdentityGroupKind.Edition, membership.Kind);
            Assert.Equal(memberships[0].GroupId, membership.GroupId);
        });
        Assert.Equal([firstBook, secondBook], memberships.Select(item => item.BookId));
    }

    private static string Occurrence(int index) => $"01P09GROUP{index:D16}";

    private static string IdentityId(string suffix) =>
        $"01P09{suffix}{Guid.NewGuid():N}"[..26];
}
