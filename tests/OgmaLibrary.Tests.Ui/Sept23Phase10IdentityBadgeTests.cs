using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.Converters;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.Views.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Sept-23 Phase 10, task 10.9 (CAT-007) headless UI: the copies and editions badge on grid and
/// list cards, its tooltip and its place in the card's accessible name, and the Compare dialog.
/// </summary>
public sealed class Sept23Phase10IdentityBadgeTests
{
    [AvaloniaFact]
    public void Grid_And_List_ShowTheCopiesAndEditionsBadge_OnlyWhereItApplies()
    {
        CatalogueViewModel vm = LoadCatalogue(
            Book("copies", "The Lantern Keeper", occurrences: 2),
            Book("work", "Harbour Lights", editions: 2, workGroupId: "W1"),
            Book("both", "Orchard Notes", occurrences: 2, editions: 3, workGroupId: "W2"),
            Book("isbn", "Quiet Rivers", duplicateCandidate: true),
            Book("plain", "Plain Book"));

        foreach (Control view in new Control[] { new CatalogueGridView { DataContext = vm }, new CatalogueListView { DataContext = vm } })
        {
            var window = new Window { Width = 1280, Height = 800, Content = view };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();

                string[] badges = window.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Where(text => text.IsEffectivelyVisible &&
                                   AutomationProperties.GetAutomationId(text) == "Catalogue.Item.Badge.Identity")
                    .Select(text => text.Text ?? string.Empty)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                Assert.Equal(
                    ["2 copies", "2 copies · 3 editions · 1 work", "2 editions · 1 work", "Possible duplicate"],
                    badges);
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void AccessibleName_IncludesTheBadge_AndTheTooltipExplainsIt_InEnglishAndFrench()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        BookSummaryProjection book = Book("work", "Harbour Lights", occurrences: 2, editions: 2, workGroupId: "W1");

        string name = BookAccessibleNameConverter.ComposeName(book, localization);
        string? tooltip = BookAccessibleNameConverter.ComposeIdentityTooltip(book, localization);

        Assert.EndsWith("2 copies · 2 editions · 1 work", name, StringComparison.Ordinal);
        Assert.NotNull(tooltip);
        Assert.StartsWith("2 copies · 2 editions · 1 work.", tooltip, StringComparison.Ordinal);
        Assert.Contains("never deleted", tooltip, StringComparison.Ordinal);
        Assert.Null(BookAccessibleNameConverter.ComposeIdentityBadge(Book("plain", "Plain Book"), localization));

        localization.SetCulture("fr");
        Assert.EndsWith(
            "2 exemplaires · 2 éditions · 1 œuvre",
            BookAccessibleNameConverter.ComposeName(book, localization),
            StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void CompareDialog_ShowsEveryFileSideBySide_WithNamedColumnsAndClose()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        DateTimeOffset seen = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
        var compare = new CompareCopiesViewModel(
            [
                new BookFileFacts("a", "The Lantern Keeper", "lantern.pdf", 2048, "ab12cd34ef56", FileValidity.Valid, true, seen, seen),
                new BookFileFacts("a", "The Lantern Keeper", "copy/lantern.pdf", 2048, "ab12cd34ef56", FileValidity.Valid, false, seen, seen),
            ],
            localization);
        var dialog = new CompareCopiesDialog { DataContext = compare };
        try
        {
            dialog.Show();
            Dispatcher.UIThread.RunJobs();

            TextBlock[] headings = dialog.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(text => AutomationProperties.GetAutomationId(text)?.StartsWith("Compare.Column.", StringComparison.Ordinal) == true)
                .ToArray();
            Assert.Equal(2, headings.Length);
            Assert.All(headings, heading => Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(heading))));
            Assert.Equal("Compare 2 files", dialog.Title);
            Assert.Equal("These files have identical content.", compare.VerdictText);
            Assert.Equal("Opens normally (file missing)", compare.Columns[1].ValidityText);
            Button close = dialog.GetVisualDescendants().OfType<Button>()
                .Single(button => AutomationProperties.GetAutomationId(button) == "Compare.Close");
            Assert.Equal("Close", AutomationProperties.GetName(close));
        }
        finally
        {
            dialog.Close();
        }
    }

    private static BookSummaryProjection Book(
        string id,
        string title,
        int occurrences = 1,
        int editions = 1,
        string? workGroupId = null,
        bool duplicateCandidate = false) =>
        new(
            BookId: id,
            Title: title,
            Authors: ["Author"],
            CoverRelativePath: null,
            Status: 0,
            Rating: null,
            ShelfIds: [],
            ReadingProgressPct: null,
            IsAvailable: true,
            Year: null,
            CopyCount: occurrences,
            OccurrenceCount: occurrences,
            WorkGroupId: workGroupId,
            EditionCount: editions,
            HasDuplicateCandidate: duplicateCandidate);

    private static CatalogueViewModel LoadCatalogue(params BookSummaryProjection[] books)
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var vm = new CatalogueViewModel(new SummaryReadModel(books), new NullNavigation(), localization);
        Task load = vm.LoadAsync(CancellationToken.None);
        Dispatcher.UIThread.RunJobs();
        load.GetAwaiter().GetResult();
        Dispatcher.UIThread.RunJobs();
        return vm;
    }

    private sealed class SummaryReadModel(IReadOnlyList<BookSummaryProjection> books) : ICatalogueReadModel
    {
        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            foreach (BookSummaryProjection book in books)
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
            yield break;
        }

        public Task<ReadingProgressProjection?> GetProgressAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ReadingProgressProjection?>(null);
    }

    private sealed class NullNavigation : IBookDetailNavigationService
    {
        public Task OpenDetailAsync(string bookId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
