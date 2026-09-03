using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue;
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

    private static string Occurrence(int index) => $"01P09GROUP{index:D16}";
}
