using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.Views.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Sept-23 Phase 10 (task 10.5, slice 3): the full filter bar, one removable chip per active
/// criterion, a no-results state that names the active filters, and smart shelves saved from
/// the live filter whose count matches the filtered grid.
/// </summary>
public sealed class Sept23Phase10FilterBarTests
{
    private static readonly string[] AllChipIds =
    [
        "title", "author", "shelf", "status", "rating", "availability", "quality",
        "missing.author", "missing.isbn", "missing.cover", "missing.year", "ocr", "duplicates",
    ];

    [AvaloniaFact]
    public async Task Chips_OnePerActiveCriterion_RemovingOneClearsOnlyThatCriterion()
    {
        (MainShellViewModel shell, _, _) = await CreateShellAsync(new InMemoryLocalizationService());
        CatalogueFilterViewModel filter = shell.Catalogue.Filter;
        ApplyEveryCriterion(filter);

        Assert.Equal(AllChipIds.Order(), shell.ActiveFilterChips.Select(chip => chip.Id).Order());
        Assert.All(shell.ActiveFilterChips, chip =>
        {
            Assert.False(string.IsNullOrWhiteSpace(chip.Label));
            Assert.Contains(chip.Label, chip.RemoveName, StringComparison.Ordinal);
        });

        foreach (string id in AllChipIds)
        {
            CatalogueFilter before = filter.ToCriteria();
            shell.RemoveFilter(id);
            CatalogueFilter after = filter.ToCriteria();

            Assert.DoesNotContain(shell.ActiveFilterChips, chip => chip.Id == id);
            Assert.Equal(Expected(before, id), after);
        }

        Assert.False(filter.HasActiveFilters);
        Assert.Empty(shell.ActiveFilterChips);
        shell.Dispose();
    }

    [AvaloniaFact]
    public async Task FilterBarControls_DriveTheCriteria()
    {
        (MainShellViewModel shell, _, _) = await CreateShellAsync(new InMemoryLocalizationService());
        CatalogueFilterBarViewModel bar = shell.FilterBar;
        CatalogueFilterViewModel filter = shell.Catalogue.Filter;

        bar.SelectedRating = bar.RatingOptions.Single(option => option.Value == 3);
        bar.SelectedAvailability = bar.AvailabilityOptions.Single(option => option.Value == true);
        bar.SelectedQuality = bar.QualityOptions.First(option => option.Value is not null);
        bar.SelectedStatus = bar.StatusOptions.Single(option => option.Value == 0);
        bar.MissingAuthor = true;
        bar.MissingYear = true;

        Assert.Equal(3, filter.MinRating);
        Assert.True(filter.AvailabilityFilter);
        Assert.NotNull(filter.QualityBelow);
        Assert.Equal(0, filter.StatusFilter);
        Assert.Equal(CatalogueMissingFields.Author | CatalogueMissingFields.Year, filter.MissingFields);

        filter.ClearAll();
        Assert.Null(bar.SelectedRating.Value);
        Assert.Null(bar.SelectedAvailability.Value);
        Assert.False(bar.MissingAuthor);
        shell.Dispose();
    }

    [AvaloniaFact]
    public async Task NoResults_ListsTheActiveFilters_NextToClear()
    {
        (MainShellViewModel shell, _, _) = await CreateShellAsync(new InMemoryLocalizationService());
        shell.Catalogue.Filter.TitleSearch = "No such title";
        shell.Catalogue.Filter.NeedsOcrOnly = true;
        Dispatcher.UIThread.RunJobs();

        Assert.True(shell.Catalogue.IsFilteredEmpty);
        string text = shell.FilterBar.NoResultsText;
        Assert.All(shell.ActiveFilterChips, chip => Assert.Contains(chip.Label, text, StringComparison.Ordinal));

        var view = new CatalogueShellView { DataContext = shell };
        var window = new Window { Width = 1280, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        TextBlock detail = view.GetVisualDescendants().OfType<TextBlock>()
            .Single(block => Avalonia.Automation.AutomationProperties.GetAutomationId(block) == "Catalogue.FilteredEmpty.Detail");
        Assert.True(detail.IsEffectivelyVisible);
        Assert.Equal(text, detail.Text);
        Assert.Contains(view.GetVisualDescendants().OfType<Button>(), button =>
            Avalonia.Automation.AutomationProperties.GetAutomationId(button) == "Catalogue.FilteredEmpty.Clear" &&
            button.IsEffectivelyVisible);

        window.Close();
        shell.Dispose();
    }

    [AvaloniaFact]
    public async Task FilterDrawer_ExposesEveryCriterionWithAnAutomationId()
    {
        (MainShellViewModel shell, _, _) = await CreateShellAsync(new InMemoryLocalizationService());
        shell.IsFilterPanelOpen = true;
        var view = new CatalogueShellView { DataContext = shell };
        var window = new Window { Width = 1280, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        string[] expected =
        [
            "Catalogue.Filter.Title", "Catalogue.Filter.Author", "Catalogue.Filter.Status",
            "Catalogue.Filter.Rating", "Catalogue.Filter.Availability", "Catalogue.Filter.Quality",
            "Catalogue.Filter.Missing.Author", "Catalogue.Filter.Missing.Isbn", "Catalogue.Filter.Missing.Cover",
            "Catalogue.Filter.Missing.Year", "Catalogue.Filter.NeedsOcr", "Catalogue.Filter.PossibleDuplicates",
            "Catalogue.Filter.Shelf", "Catalogue.Filter.SmartShelfName", "Catalogue.Filter.SaveSmartShelf",
        ];
        List<Control> controls = view.GetVisualDescendants().OfType<Control>().ToList();
        foreach (string id in expected)
        {
            Control control = Assert.Single(controls, c => Avalonia.Automation.AutomationProperties.GetAutomationId(c) == id);
            Assert.False(
                string.IsNullOrWhiteSpace(Avalonia.Automation.AutomationProperties.GetName(control)),
                $"{id} has no accessible name.");
        }

        window.Close();
        shell.Dispose();
    }

    [AvaloniaFact]
    public async Task SmartShelf_FromFilter_ShowsTheSameCountAsTheFilteredGrid()
    {
        (MainShellViewModel shell, FakeReadModel readModel, FakeWriteService writes) =
            await CreateShellAsync(new InMemoryLocalizationService());
        CatalogueFilterViewModel filter = shell.Catalogue.Filter;
        filter.MinRating = 3;
        filter.MissingFields = CatalogueMissingFields.Author;
        Dispatcher.UIThread.RunJobs();
        int gridCount = shell.Catalogue.TotalFilteredCount;
        Assert.InRange(gridCount, 1, readModel.Books.Count - 1);

        Assert.False(shell.FilterBar.CanSaveSmartShelf);
        shell.FilterBar.SmartShelfName = "Rated, no author";
        Assert.True(shell.FilterBar.CanSaveSmartShelf);
        await shell.FilterBar.SaveSmartShelfAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.True(writes.LastIsSmart);
        Assert.True(SmartShelfQueryParser.TryParse(writes.LastQuery, out _));
        ShelfProjection shelf = Assert.Single(shell.ShelfSidebar.Shelves, s => s.IsSmart);
        Assert.Equal("Rated, no author", shelf.Name);
        Assert.Equal(gridCount, shelf.BookCount);
        Assert.Equal(string.Empty, shell.FilterBar.SmartShelfName);
        Assert.False(string.IsNullOrWhiteSpace(shell.FilterBar.SmartShelfStatus));

        // Opening the smart shelf in the Library shows the same books.
        filter.ClearAll();
        shell.ShelfSidebar.SelectedShelf = shelf;
        shell.ShowSelectedCollectionInLibrary();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(shelf.BookCount, shell.Catalogue.TotalFilteredCount);
        shell.Dispose();
    }

    [AvaloniaFact]
    public async Task ShelfCounts_ComeFromTheCountQuery_AndRefreshAfterLoad()
    {
        (MainShellViewModel shell, FakeReadModel readModel, _) = await CreateShellAsync(new InMemoryLocalizationService());

        ShelfProjection manual = Assert.Single(shell.ShelfSidebar.Shelves, s => !s.IsSmart);
        Assert.Equal(2, manual.BookCount); // the fake's projection says 999; the count query wins

        readModel.Books[0] = readModel.Books[0] with { ShelfIds = [] };
        await shell.ShelfSidebar.LoadAsync();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, Assert.Single(shell.ShelfSidebar.Shelves).BookCount);
        shell.Dispose();
    }

    [AvaloniaFact]
    public async Task SmartShelf_NotOfferedWhileACollectionFilterApplies()
    {
        (MainShellViewModel shell, _, _) = await CreateShellAsync(new InMemoryLocalizationService());
        shell.Catalogue.Filter.SelectedShelfId = "shelf-manual";
        shell.Catalogue.Filter.MinRating = 2;
        shell.FilterBar.SmartShelfName = "Mixed";

        Assert.False(shell.FilterBar.CanSaveSmartShelf);
        Assert.False(string.IsNullOrWhiteSpace(shell.FilterBar.SmartShelfHint));
        shell.Dispose();
    }

    [AvaloniaFact]
    public async Task FrenchStrings_ArePresentForEveryChipAndControl()
    {
        var english = new InMemoryLocalizationService();
        english.SetCulture("en");
        var french = new InMemoryLocalizationService();
        french.SetCulture("fr");
        (MainShellViewModel en, _, _) = await CreateShellAsync(english);
        (MainShellViewModel fr, _, _) = await CreateShellAsync(french);
        ApplyEveryCriterion(en.Catalogue.Filter);
        ApplyEveryCriterion(fr.Catalogue.Filter);

        string[] enLabels = en.ActiveFilterChips.Select(chip => chip.Label).ToArray();
        string[] frLabels = fr.ActiveFilterChips.Select(chip => chip.Label).ToArray();
        Assert.Equal(enLabels.Length, frLabels.Length);
        for (int i = 0; i < enLabels.Length; i++)
        {
            // Values typed by the user (title, author, shelf name) are the same in both languages.
            if (en.ActiveFilterChips[i].Id is not ("title" or "author" or "shelf"))
            {
                Assert.NotEqual(enLabels[i], frLabels[i]);
            }
        }

        string[] keys =
        [
            "Filter.Bar.Status", "Filter.Bar.Rating", "Filter.Bar.Availability", "Filter.Bar.Quality",
            "Filter.Bar.Missing", "Filter.Bar.Shelf", "Filter.Bar.SmartShelfName", "Filter.Bar.SaveSmartShelf",
            "Filter.Bar.SmartShelfSavedFormat", "Filter.Bar.SmartShelfNeedsNoCollection",
            "Catalogue.FilteredEmpty.ActiveFormat", "Collections.SmartShelf",
        ];
        foreach (string key in keys)
        {
            Assert.NotEqual(key, english[key]);
            Assert.NotEqual(key, french[key]);
            Assert.NotEqual(english[key], french[key]);
        }

        Assert.NotEqual(en.FilterBar.StatusOptions.Select(o => o.Label), fr.FilterBar.StatusOptions.Select(o => o.Label));
        Assert.NotEqual(en.FilterBar.RatingOptions[0].Label, fr.FilterBar.RatingOptions[0].Label);
        Assert.NotEqual(en.FilterBar.QualityOptions.Select(o => o.Label), fr.FilterBar.QualityOptions.Select(o => o.Label));
        Assert.NotEqual(
            en.FilterBar.AvailabilityOptions.Select(o => o.Label),
            fr.FilterBar.AvailabilityOptions.Select(o => o.Label));
        en.Dispose();
        fr.Dispose();
    }

    private static void ApplyEveryCriterion(CatalogueFilterViewModel filter)
    {
        filter.TitleSearch = "Topic";
        filter.AuthorSearch = "Writer";
        filter.SelectedShelfId = "shelf-manual";
        filter.StatusFilter = 0;
        filter.MinRating = 2;
        filter.AvailabilityFilter = false;
        filter.QualityBelow = 0.5;
        filter.MissingFields = CatalogueMissingFields.Author | CatalogueMissingFields.Isbn |
                               CatalogueMissingFields.Cover | CatalogueMissingFields.Year;
        filter.NeedsOcrOnly = true;
        filter.PossibleDuplicatesOnly = true;
    }

    private static CatalogueFilter Expected(CatalogueFilter before, string id) => id switch
    {
        "title" => before with { TitleContains = null },
        "author" => before with { AuthorContains = null },
        "shelf" => before with { ShelfId = null },
        "status" => before with { Status = null },
        "rating" => before with { MinRating = null, MaxRating = null },
        "availability" => before with { IsAvailable = null },
        "quality" => before with { QualityBelow = null },
        "missing.author" => before with { MissingFields = before.MissingFields & ~CatalogueMissingFields.Author },
        "missing.isbn" => before with { MissingFields = before.MissingFields & ~CatalogueMissingFields.Isbn },
        "missing.cover" => before with { MissingFields = before.MissingFields & ~CatalogueMissingFields.Cover },
        "missing.year" => before with { MissingFields = before.MissingFields & ~CatalogueMissingFields.Year },
        "ocr" => before with { NeedsOcr = false },
        "duplicates" => before with { PossibleDuplicates = false },
        _ => throw new ArgumentOutOfRangeException(nameof(id)),
    };

    private static async Task<(MainShellViewModel Shell, FakeReadModel ReadModel, FakeWriteService Writes)> CreateShellAsync(
        InMemoryLocalizationService localization)
    {
        var readModel = new FakeReadModel();
        var writes = new FakeWriteService(readModel);
        var catalogue = new CatalogueViewModel(readModel, new NullNavigation(), localization);
        var shell = new MainShellViewModel(
            localization,
            catalogue,
            new BookDetailViewModel(readModel, new NullNavigation(), localization),
            new ShelfSidebarViewModel(readModel, writes, localization, catalogue.Filter));
        await shell.InitializeAsync();
        Dispatcher.UIThread.RunJobs();
        return (shell, readModel, writes);
    }

    private sealed class NullNavigation : IBookDetailNavigationService, IReaderNavigationService
    {
        public Task OpenDetailAsync(string bookId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task OpenReaderAsync(string bookId, int? pageHint = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    internal sealed class FakeReadModel : ICatalogueReadModel
    {
        public List<BookSummaryProjection> Books { get; } =
        [
            Book(1, rating: 4, authors: [], shelf: true),
            Book(2, rating: 3, authors: [], shelf: true),
            Book(3, rating: 5, authors: ["Writer Three"]),
            Book(4, rating: 1, authors: []),
            Book(5, rating: null, authors: ["Writer Five"]),
        ];

        public List<(string Id, string Name, bool IsSmart, string? Query)> Shelves { get; } =
            [("shelf-manual", "Favourites", false, null)];

        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            IEnumerable<BookSummaryProjection> source = Books;
            CatalogueFilter criteria = filter;

            // Mirrors the real read model: a smart shelf evaluates its saved conditions.
            if (filter.ShelfId is { } shelfId && Shelves.FirstOrDefault(s => s.Id == shelfId) is { IsSmart: true } smart)
            {
                source = SmartShelfQueryParser.TryParse(smart.Query, out IReadOnlyList<SmartShelfCondition> conditions)
                    ? SmartShelfEvaluator.Evaluate(Books, conditions)
                    : [];
                criteria = filter with { ShelfId = null };
            }

            foreach (BookSummaryProjection book in CatalogueCriteriaEvaluator.Apply(source, criteria))
            {
                yield return book;
            }
        }

        public Task<BookDetailProjection?> GetBookDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<BookDetailProjection?>(null);

        public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            foreach ((string id, string name, bool isSmart, string? query) in Shelves)
            {
                // A deliberately stale count: the sidebar must use the count query instead.
                yield return new ShelfProjection(id, name, isSmart, 999, query);
            }
        }

        public Task<ReadingProgressProjection?> GetProgressAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ReadingProgressProjection?>(null);

        private static BookSummaryProjection Book(int index, int? rating, string[] authors, bool shelf = false) =>
            new(
                $"book-{index:D3}",
                $"Topic {index:D3}",
                authors,
                null,
                0,
                rating,
                shelf ? ["shelf-manual"] : [],
                null,
                true,
                2000 + index);
    }

    internal sealed class FakeWriteService(FakeReadModel readModel) : ICatalogueWriteService
    {
        public bool LastIsSmart { get; private set; }

        public string? LastQuery { get; private set; }

        public Task<string> CreateShelfAsync(
            string name,
            bool isSmart = false,
            string? query = null,
            CancellationToken cancellationToken = default)
        {
            LastIsSmart = isSmart;
            LastQuery = query;
            string id = $"shelf-{readModel.Shelves.Count + 1:D3}";
            readModel.Shelves.Add((id, name, isSmart, query));
            return Task.FromResult(id);
        }

        public Task RenameShelfAsync(string shelfId, string newName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteShelfAsync(string shelfId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task AddBookToShelfAsync(string shelfId, string bookId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveBookFromShelfAsync(string shelfId, string bookId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateMetadataFieldAsync(
            string bookId,
            string fieldName,
            string? value,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task BulkEditAsync(BulkEditCommand command, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<BulkEditSnapshot>> CaptureBulkEditSnapshotAsync(IReadOnlyList<string> bookIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BulkEditSnapshot>>([]);

        public Task RestoreBulkEditSnapshotAsync(IReadOnlyList<BulkEditSnapshot> snapshots, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<MetadataFieldSnapshot?> CaptureMetadataFieldSnapshotAsync(string bookId, string fieldName, CancellationToken cancellationToken = default) =>
            Task.FromResult<MetadataFieldSnapshot?>(null);

        public Task RestoreMetadataFieldSnapshotAsync(MetadataFieldSnapshot snapshot, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
