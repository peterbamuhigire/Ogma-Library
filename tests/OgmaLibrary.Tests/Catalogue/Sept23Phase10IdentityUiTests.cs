using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Diagnostics;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Infrastructure.Localization;
using OgmaLibrary.Infrastructure.Metadata;
using OgmaLibrary.Tests.Diagnostics;
using Xunit;

namespace OgmaLibrary.Tests.Catalogue;

/// <summary>
/// Sept-23 Phase 10, task 10.9 (CAT-007): merge, split and undo from the catalogue on SQLite.
/// Merging two books shows one grouped card, undo shows two again, split restores, and a group
/// changed elsewhere surfaces as a localised conflict message instead of a silent overwrite.
/// </summary>
public sealed class Sept23Phase10IdentityUiTests : IDisposable
{
    private const string Lantern = "P10-ID-LANTERN";
    private const string Harbour = "P10-ID-HARBOUR";
    private const string Orchard = "P10-ID-ORCHARD";

    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;
    private readonly IdentityGroupingService _grouping;
    private readonly BookOccurrenceReader _occurrences;
    private readonly CatalogueViewModel _catalogue;
    private readonly CapturingNotifier _notifier = new();
    private readonly InMemoryLocalizationService _localization = new();
    private readonly IdentityGroupingViewModel _identity;

    /// <summary>Creates a fresh SQLite catalogue with three books, one of them held twice.</summary>
    public Sept23Phase10IdentityUiTests()
    {
        (_context, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
        _context.Database.EnsureCreated();
        Seed();
        _grouping = new IdentityGroupingService(_context);
        _occurrences = new BookOccurrenceReader(_context);
        var readModel = new CatalogueReadModel(_context, identityGrouping: _grouping);
        _catalogue = new CatalogueViewModel(readModel, new NullNavigation(), _localization);
        _identity = new IdentityGroupingViewModel(_catalogue, _grouping, _occurrences, _localization, _notifier);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _context.Dispose();
        CatalogueTestHelper.DeleteTempDb(_dbPath);
    }

    [Fact]
    public async Task Merge_ShowsOneGroupedCard_AndUndoShowsTwo()
    {
        await _catalogue.LoadAsync(CancellationToken.None);
        Assert.Equal(3, _catalogue.FilteredItems.Count);
        Select(Harbour, Orchard);
        Assert.True(_identity.CanMerge);

        await _identity.MergeSelectionAsync();

        BookSummaryProjection grouped = Assert.Single(_catalogue.FilteredItems, book => book.EditionCount > 1);
        Assert.Equal(2, _catalogue.FilteredItems.Count);
        Assert.Equal(2, grouped.EditionCount);
        Assert.NotNull(grouped.WorkGroupId);
        UserNotification merged = _notifier.Notifications[^1];
        Assert.Equal("Catalogue.Identity.Merged", merged.MessageKey);
        Assert.Equal(UserNotificationActionKind.Undo, merged.ActionKind);

        await merged.Action!(CancellationToken.None);

        Assert.Equal(3, _catalogue.FilteredItems.Count);
        Assert.All(_catalogue.FilteredItems, book => Assert.Equal(1, book.EditionCount));
        Assert.Equal("Catalogue.Identity.Undone", _notifier.Notifications[^1].MessageKey);
    }

    [Fact]
    public async Task Split_RestoresSeparateCards_AndUndoRegroups()
    {
        await _catalogue.LoadAsync(CancellationToken.None);
        Select(Harbour, Orchard);
        await _identity.MergeSelectionAsync();
        Select(_catalogue.FilteredItems.Single(book => book.EditionCount > 1).BookId);
        Assert.True(_identity.CanSplit);

        await _identity.SplitSelectionAsync();

        Assert.Equal(3, _catalogue.FilteredItems.Count);
        Assert.All(_catalogue.FilteredItems, book => Assert.Null(book.WorkGroupId));
        UserNotification split = _notifier.Notifications[^1];
        Assert.Equal("Catalogue.Identity.Split", split.MessageKey);

        await split.Action!(CancellationToken.None);

        Assert.Equal(2, _catalogue.FilteredItems.Count);
    }

    [Fact]
    public async Task Undo_AfterTheGroupChangedElsewhere_ShowsLocalisedConflict()
    {
        await _catalogue.LoadAsync(CancellationToken.None);
        Select(Harbour, Orchard);
        await _identity.MergeSelectionAsync();
        UserNotification merged = _notifier.Notifications[^1];
        string groupId = _catalogue.FilteredItems.Single(book => book.EditionCount > 1).WorkGroupId!;
        IReadOnlyDictionary<string, IReadOnlyList<string>> lantern =
            await _occurrences.ResolveOccurrenceIdsAsync([Lantern]);
        await _grouping.MergeAsync(groupId, lantern[Lantern], "another-window");

        await merged.Action!(CancellationToken.None);

        Assert.Equal("Catalogue.Identity.Conflict", _notifier.Notifications[^1].MessageKey);
        IdentityGroupDescriptor? group = await _grouping.FindByIdAsync(groupId);
        // Harbour and Orchard, plus both files of Lantern: the other window's merge is kept.
        Assert.Equal(4, group!.ActiveOccurrenceIds.Count);
        AssertLocalised("Catalogue.Identity.Conflict");
    }

    [Fact]
    public async Task Merge_OfBooksInTwoDifferentWorks_IsRefusedWithAMessage()
    {
        await _catalogue.LoadAsync(CancellationToken.None);
        IReadOnlyDictionary<string, IReadOnlyList<string>> ids =
            await _occurrences.ResolveOccurrenceIdsAsync([Lantern, Harbour, Orchard]);
        await _grouping.CreateAsync(IdentityGroupKind.Work, [.. ids[Harbour]], "test");
        await _grouping.CreateAsync(IdentityGroupKind.Work, [.. ids[Orchard]], "test");
        await _catalogue.LoadAsync(CancellationToken.None);
        Select(Harbour, Orchard);

        await _identity.MergeSelectionAsync();

        Assert.Equal("Catalogue.Identity.MergeTwoWorks", _notifier.Notifications[^1].MessageKey);
        AssertLocalised("Catalogue.Identity.MergeTwoWorks");
    }

    [Fact]
    public async Task Compare_ListsEveryFileOfTheSelection_WithoutPaths()
    {
        await _catalogue.LoadAsync(CancellationToken.None);
        Select(Lantern);
        Assert.True(_identity.CanCompare);

        CompareCopiesViewModel? compare = await _identity.CreateCompareAsync();

        Assert.NotNull(compare);
        Assert.Equal(2, compare!.Columns.Count);
        Assert.All(compare.Columns, column => Assert.Equal("The Lantern Keeper", column.Title));
        Assert.Contains(compare.Columns, column => column.PathText == "copy/lantern.pdf");
        Assert.All(compare.Columns, column => Assert.Equal("ab12cd34ef56", column.HashText));
        Assert.True(compare.IsIdenticalContent);
    }

    [Fact]
    public async Task Compare_WithNoStoredTitle_NamesEachFileByItsFileName()
    {
        BookRow lantern = _context.Books.Single(book => book.BookId == Lantern);
        lantern.Title = string.Empty;
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        IReadOnlyList<BookFileFacts> files = await _occurrences.GetFileFactsAsync([Lantern]);

        Assert.All(files, file => Assert.Equal("lantern", file.Title));
    }

    [Fact]
    public async Task PossibleDuplicates_MatchesTheLibraryHealthRules()
    {
        var health = new LibraryHealthService(_context);
        LibraryHealthSnapshot snapshot = await health.GetHealthSnapshotAsync();
        var readModel = new CatalogueReadModel(_context, identityGrouping: _grouping);

        var flagged = new List<string>();
        await foreach (BookSummaryProjection book in readModel.GetBookSummariesAsync(
                           new CatalogueFilter(PossibleDuplicates: true)))
        {
            flagged.Add(book.BookId);
        }

        // Harbour and Orchard share an ISBN (health rule); Lantern has two copies.
        Assert.Equal(
            snapshot.Duplicates.Select(entry => entry.BookId).Append(Lantern).Distinct().Order(),
            flagged.Order());
    }

    private void Select(params string[] bookIds)
    {
        _catalogue.SelectedBooks.Clear();
        foreach (string id in bookIds)
        {
            _catalogue.SelectedBooks.Add(_catalogue.FilteredItems.Single(book => book.BookId == id));
        }
    }

    private void AssertLocalised(string key)
    {
        string english = _localization[key];
        _localization.SetCulture("fr");
        string french = _localization[key];
        _localization.SetCulture("en");
        Assert.NotEqual(key, english);
        Assert.NotEqual(key, french);
        Assert.NotEqual(english, french);
    }

    private void Seed()
    {
        DateTimeOffset seen = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        _context.Books.AddRange(
            Book(Lantern, "The Lantern Keeper", "ab12cd34ef56aa00", isbn: null),
            Book(Harbour, "Harbour Lights", "bb12cd34ef56aa01", isbn: "9780000000019"),
            Book(Orchard, "Harbour Lights (revised)", "cb12cd34ef56aa02", isbn: "9780000000019"));
        _context.BookFiles.AddRange(
            File(Lantern, "lantern.pdf", seen),
            File(Lantern, "copy/lantern.pdf", seen),
            File(Harbour, "harbour.pdf", seen),
            File(Orchard, "orchard.pdf", seen));
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }

    private static BookRow Book(string id, string title, string hash, string? isbn) => new()
    {
        BookId = id,
        Title = title,
        Status = 0,
        Sha256Hash = hash,
        SizeBytes = 2048,
        IsbnNormalized = isbn,
        AddedUtc = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
    };

    private static BookFileRow File(string bookId, string path, DateTimeOffset seen) => new()
    {
        BookId = bookId,
        RelativePath = path,
        FileStatus = 0,
        FileValidity = (int)FileValidity.Valid,
        LastSeenUtc = seen,
    };

    private sealed class NullNavigation : IBookDetailNavigationService
    {
        public Task OpenDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
